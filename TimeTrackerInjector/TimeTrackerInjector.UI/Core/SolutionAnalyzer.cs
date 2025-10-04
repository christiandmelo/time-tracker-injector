using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TimeTrackerInjector.UI.Config;
using TimeTrackerInjector.UI.Core;

namespace TimeTrackerInjector.UI.Core
{
  /// <summary>
  /// Analisa a solution C# e identifica os métodos que pertencem ao fluxo
  /// iniciado pelo método principal configurado (ex.: ProcessService.ProcessAll).
  /// 
  /// Ele:
  ///  - Carrega a solution e o projeto via Roslyn.
  ///  - Identifica o método de entrada configurado.
  ///  - Constrói o grafo de chamadas (CallGraphBuilder).
  ///  - Retorna apenas os métodos e arquivos que fazem parte dessa cadeia.
  /// </summary>
  public class SolutionAnalyzer
  {
    private readonly TimeTrackerConfig _config;

    public SolutionAnalyzer(TimeTrackerConfig config)
    {
      _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public async Task<AnalyzeResult> AnalyzeAsync()
    {
      if (string.IsNullOrWhiteSpace(_config.SolutionFile))
        throw new InvalidOperationException("O caminho da solution não foi configurado.");

      if (!File.Exists(_config.SolutionFile))
        throw new FileNotFoundException("Solution não encontrada.", _config.SolutionFile);

      using var workspace = MSBuildWorkspace.Create();
      workspace.WorkspaceFailed += (s, e) =>
      {
        Console.WriteLine($"[MSBuild] {e.Diagnostic}");
      };

      // 🧩 Carrega a solution e o projeto alvo
      var solution = await workspace.OpenSolutionAsync(_config.SolutionFile);
      var project = GetTargetProject(solution, _config.ProjectName);
      if (project == null)
        throw new InvalidOperationException($"Projeto '{_config.ProjectName}' não encontrado na solution.");

      var compilation = await project.GetCompilationAsync();
      if (compilation == null)
        throw new InvalidOperationException("Falha ao compilar o projeto.");

      // 🧠 Lista temporária de todos os métodos (sem filtro)
      var analyzedMethods = new List<AnalyzedMethod>();

      foreach (var doc in project.Documents)
      {
        var syntaxRoot = await doc.GetSyntaxRootAsync();
        if (syntaxRoot == null) continue;

        var model = await doc.GetSemanticModelAsync();
        if (model == null) continue;

        foreach (var methodDecl in syntaxRoot.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
          var declared = model.GetDeclaredSymbol(methodDecl);
          if (declared is not IMethodSymbol methodSymbol)
            continue;

          analyzedMethods.Add(new AnalyzedMethod
          {
            ClassName = methodSymbol.ContainingType?.Name ?? "",
            MethodName = methodSymbol.Name,
            FilePath = doc.FilePath ?? "",
            Symbol = methodSymbol
          });
        }
      }

      // 🧭 Encontra o método de entrada (classe + método definidos no config)
      var entryMethod = FindEntryMethod(compilation, _config.ClassName, _config.MethodName);
      if (entryMethod == null)
        throw new InvalidOperationException($"Método de entrada '{_config.ClassName}.{_config.MethodName}' não encontrado.");

      // 🔗 Constrói o grafo de chamadas
      var builder = new CallGraphBuilder(compilation);
      var rootNode = await builder.BuildTreeAsync(entryMethod);

      // 🔄 Extrai todos os métodos do grafo (recursivamente)
      var methodsInGraph = FlattenCallGraph(rootNode).ToHashSet(SymbolEqualityComparer.Default);

      // 🔍 Filtra os métodos realmente relevantes (que estão no grafo)
      var filtered = analyzedMethods
          .Where(m => m.Symbol != null && methodsInGraph.Contains(m.Symbol))
          .ToList();

      // 🚫 Exclui padrões conhecidos e métodos "folha" que não chamam ninguém
      var excludedPatterns = new[] { "Program", "Main", "LogService", "Write", "LoadItems" };
      filtered = filtered
          .Where(m => !excludedPatterns.Any(ex =>
              m.ClassName.Contains(ex, StringComparison.OrdinalIgnoreCase) ||
              m.MethodName.Contains(ex, StringComparison.OrdinalIgnoreCase)))
          .ToList();

      // 🧩 Retorna o resultado consolidado
      return new AnalyzeResult
      {
        Compilation = compilation,
        EntryMethod = entryMethod,
        Methods = filtered
      };
    }

    /// <summary>
    /// Localiza o projeto configurado dentro da solution.
    /// </summary>
    private Project? GetTargetProject(Solution solution, string? projectName)
    {
      if (string.IsNullOrWhiteSpace(projectName))
        return solution.Projects.FirstOrDefault();

      return solution.Projects.FirstOrDefault(p =>
          string.Equals(p.Name, projectName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Localiza o método de entrada (Classe + Método) dentro da compilação.
    /// </summary>
    private IMethodSymbol? FindEntryMethod(Compilation compilation, string? className, string? methodName)
    {
      if (string.IsNullOrWhiteSpace(className) || string.IsNullOrWhiteSpace(methodName))
        return null;

      foreach (var tree in compilation.SyntaxTrees)
      {
        var model = compilation.GetSemanticModel(tree);
        var methods = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>();

        foreach (var methodDecl in methods)
        {
          var symbol = model.GetDeclaredSymbol(methodDecl);
          if (symbol is not IMethodSymbol methodSymbol)
            continue;

          if (methodSymbol.Name == methodName &&
              string.Equals(methodSymbol.ContainingType?.Name, className, StringComparison.OrdinalIgnoreCase))
          {
            return methodSymbol;
          }
        }
      }

      return null;
    }

    /// <summary>
    /// Retorna todos os métodos do grafo de chamadas (de forma recursiva).
    /// </summary>
    private static IEnumerable<IMethodSymbol> FlattenCallGraph(CallGraphNode root)
    {
      var list = new List<IMethodSymbol>();
      var visited = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);

      void Walk(CallGraphNode node)
      {
        if (visited.Contains(node.Symbol))
          return;

        visited.Add(node.Symbol);
        list.Add(node.Symbol);

        foreach (var child in node.Children)
          Walk(child);
      }

      Walk(root);
      return list;
    }
  }

  /// <summary>
  /// Resultado consolidado da análise da solution.
  /// </summary>
  public class AnalyzeResult
  {
    public Compilation? Compilation { get; set; }
    public IMethodSymbol? EntryMethod { get; set; }
    public List<AnalyzedMethod> Methods { get; set; } = new();
  }

  /// <summary>
  /// Representa um método identificado durante a análise.
  /// </summary>
  public class AnalyzedMethod
  {
    public string ClassName { get; set; } = "";
    public string MethodName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public IMethodSymbol? Symbol { get; set; }
  }
}
