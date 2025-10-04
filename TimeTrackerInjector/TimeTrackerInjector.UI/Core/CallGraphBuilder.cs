using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TimeTrackerInjector.UI.Core;

namespace TimeTrackerInjector.UI.Core
{
  /// <summary>
  /// Constrói a árvore de chamadas (CallGraphNode) a partir de um método raiz.
  /// </summary>
  public class CallGraphBuilder
  {
    private readonly Compilation _compilation;
    private readonly HashSet<IMethodSymbol> _visited = new(SymbolEqualityComparer.Default);

    public CallGraphBuilder(Compilation compilation)
    {
      _compilation = compilation ?? throw new ArgumentNullException(nameof(compilation));
    }

    public async Task<CallGraphNode> BuildTreeAsync(IMethodSymbol root)
    {
      var rootNode = new CallGraphNode(root);
      await ExpandAsync(rootNode);
      return rootNode;
    }

    private async Task ExpandAsync(CallGraphNode node)
    {
      var method = node.Symbol;
      if (_visited.Contains(method)) return;
      _visited.Add(method);

      var syntaxRef = method.DeclaringSyntaxReferences.FirstOrDefault();
      if (syntaxRef == null) return;

      var syntax = await syntaxRef.GetSyntaxAsync();
      if (syntax is not MethodDeclarationSyntax mds) return;

      var model = _compilation.GetSemanticModel(mds.SyntaxTree);
      var invocations = mds.DescendantNodes().OfType<InvocationExpressionSyntax>();

      foreach (var inv in invocations)
      {
        var info = model.GetSymbolInfo(inv);
        IMethodSymbol? target = null;

        if (info.Symbol is IMethodSymbol s)
          target = s;
        else if (info.CandidateSymbols.Length > 0)
          target = info.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();

        if (target == null) continue;

        // Limita a métodos do mesmo assembly analisado
        if (!SymbolEqualityComparer.Default.Equals(target.ContainingAssembly, _compilation.Assembly))
          continue;

        var child = new CallGraphNode(target);
        node.Children.Add(child);
        await ExpandAsync(child);
      }
    }
  }
}
