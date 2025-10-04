using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TimeTrackerInjector.UI.Config;

namespace TimeTrackerInjector.UI.Core
{
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

      var solution = await workspace.OpenSolutionAsync(_config.SolutionFile);
      var project = GetTargetProject(solution, _config.ProjectName);
      if (project == null)
        throw new InvalidOperationException($"Projeto '{_config.ProjectName}' não encontrado na solution.");

      var compilation = await project.GetCompilationAsync();
      if (compilation == null)
        throw new InvalidOperationException("Falha ao compilar o projeto.");

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

      var entryMethod = FindEntryMethod(compilation, _config.ClassName, _config.MethodName);
      if (entryMethod == null)
      {
        Console.WriteLine($"[WARN] Método de entrada '{_config.ClassName}.{_config.MethodName}' não encontrado.");
      }

      return new AnalyzeResult
      {
        Compilation = compilation,
        EntryMethod = entryMethod,
        Methods = analyzedMethods
      };
    }

    private Project? GetTargetProject(Solution solution, string? projectName)
    {
      if (string.IsNullOrWhiteSpace(projectName))
        return solution.Projects.FirstOrDefault();

      return solution.Projects.FirstOrDefault(p =>
          string.Equals(p.Name, projectName, StringComparison.OrdinalIgnoreCase));
    }

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
  }

  public class AnalyzeResult
  {
    public Compilation? Compilation { get; set; }
    public IMethodSymbol? EntryMethod { get; set; }
    public List<AnalyzedMethod> Methods { get; set; } = new();
  }

  public class AnalyzedMethod
  {
    public string ClassName { get; set; } = "";
    public string MethodName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public IMethodSymbol? Symbol { get; set; }
  }
}
