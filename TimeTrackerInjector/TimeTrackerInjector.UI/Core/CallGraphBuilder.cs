using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TimeTrackerInjector.UI.Core
{
  /// <summary>
  /// Responsável por mapear recursivamente todas as chamadas de método
  /// a partir de um ponto de entrada (método inicial configurado).
  /// </summary>
  public class CallGraphBuilder
  {
    private readonly Compilation _compilation;
    private readonly HashSet<IMethodSymbol> _visitedMethods = new(SymbolEqualityComparer.Default);
    private readonly List<AnalyzedMethod> _methodsFound = new();

    public CallGraphBuilder(Compilation compilation)
    {
      _compilation = compilation ?? throw new ArgumentNullException(nameof(compilation));
    }

    /// <summary>
    /// Constrói o grafo de chamadas a partir do método informado.
    /// </summary>
    /// <param name="rootMethod">Símbolo do método inicial.</param>
    public async Task<IReadOnlyList<AnalyzedMethod>> BuildAsync(IMethodSymbol rootMethod)
    {
      if (rootMethod == null)
        throw new ArgumentNullException(nameof(rootMethod));

      await AnalyzeMethodAsync(rootMethod);
      return _methodsFound;
    }

    /// <summary>
    /// Analisa um método e identifica todas as chamadas diretas que ele faz.
    /// </summary>
    private async Task AnalyzeMethodAsync(IMethodSymbol methodSymbol)
    {
      if (_visitedMethods.Contains(methodSymbol))
        return;

      _visitedMethods.Add(methodSymbol);

      var syntaxRef = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault();
      if (syntaxRef == null)
        return;

      var syntaxNode = await syntaxRef.GetSyntaxAsync();
      if (syntaxNode is not MethodDeclarationSyntax methodDecl)
        return;

      var semanticModel = _compilation.GetSemanticModel(syntaxNode.SyntaxTree);
      var invocations = methodDecl.DescendantNodes().OfType<InvocationExpressionSyntax>();

      foreach (var invocation in invocations)
      {
        var symbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        if (symbol == null)
          continue;

        // Filtra apenas métodos do mesmo assembly/projeto (evita System.*, etc.)
        if (symbol.ContainingAssembly?.Name != _compilation.AssemblyName)
          continue;

        // Registra o método encontrado
        var filePath = symbol.Locations.FirstOrDefault()?.SourceTree?.FilePath ?? "";
        _methodsFound.Add(new AnalyzedMethod
        {
          ProjectName = _compilation.AssemblyName,
          ClassName = symbol.ContainingType?.Name ?? "(Desconhecida)",
          MethodName = symbol.Name,
          FilePath = filePath
        });

        // Recursão: segue analisando o método chamado
        await AnalyzeMethodAsync(symbol);
      }
    }
  }
}

