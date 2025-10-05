using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeTrackerInjector.UI.Config;
using TimeTrackerInjector.UI.Core;

namespace TimeTrackerInjector.UI.Core
{
  /// <summary>
  /// CodeRewriter com:
  /// - Fase de mapeamento (coleta pontos de inserção)
  /// - Fase de aplicação (injeta Start/Stop no bloco correto, incluindo blocos internos)
  /// - Loops acumulativos (Start antes e Stop depois do loop)
  /// - Tratamento de return Foo(): vira Start/Stop + var __ret + return __ret;
  /// - Log hierárquico deduplicado a partir do CallGraph (exclui Write/LoadItems/Main do log)
  /// - Ignora instrumentar chamadas System/Microsoft e Write/LoadItems/Main
  /// </summary>
  public class CodeRewriter
  {
    private readonly TimeTrackerConfig _config;
    public event Action<string>? OnLog;

    public CodeRewriter(TimeTrackerConfig config)
    {
      _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public async Task RewriteAsync(
        Compilation compilation,
        CallGraphNode callTreeRoot,
        IReadOnlyList<AnalyzedMethod> methods,
        IMethodSymbol entryMethod)
    {
      if (methods == null || methods.Count == 0)
        return;

      var entryClass = entryMethod.ContainingType;
      var involvedClasses = new HashSet<string>(
          methods.Select(m => m.ClassName),
          StringComparer.Ordinal);

      var involvedFiles = methods
          .Where(m => involvedClasses.Contains(m.ClassName))
          .Select(m => m.FilePath)
          .Where(f => !string.IsNullOrWhiteSpace(f))
          .Distinct()
          .ToList();

      var stopwatchByMethod = BuildStopwatchRegistry(callTreeRoot, entryClass);

      foreach (var filePath in involvedFiles)
      {
        if (!File.Exists(filePath))
          continue;

        var tree = compilation.SyntaxTrees.FirstOrDefault(t => t.FilePath == filePath);
        if (tree == null)
          continue;

        var model = compilation.GetSemanticModel(tree);
        var root = (await tree.GetRootAsync()) as CompilationUnitSyntax;
        if (root == null)
          continue;

        var rewriter = new StableRewriter(model, stopwatchByMethod, entryMethod, callTreeRoot, filePath, OnLog);
        var newRoot = (CompilationUnitSyntax)rewriter.Visit(root);

        var newCode = newRoot.NormalizeWhitespace().ToFullString();
        await File.WriteAllTextAsync(filePath, newCode, Encoding.UTF8);
      }
    }

    private static Dictionary<IMethodSymbol, StopwatchInfo> BuildStopwatchRegistry(CallGraphNode root, INamedTypeSymbol? entryClass)
    {
      var dict = new Dictionary<IMethodSymbol, StopwatchInfo>(SymbolEqualityComparer.Default);
      int counter = 1;
      var entryClassName = entryClass?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) ?? "EntryClass";

      void Walk(CallGraphNode node)
      {
        var sym = node.Symbol;
        if (!dict.ContainsKey(sym))
        {
          var clean = new string(sym.Name.Where(char.IsLetterOrDigit).ToArray());
          dict[sym] = new StopwatchInfo
          {
            DeclaringClassName = entryClassName,
            FieldName = $"sw{counter++}_{clean}"
          };
        }

        foreach (var c in node.Children)
          Walk(c);
      }

      Walk(root);
      return dict;
    }

    private sealed class StableRewriter : CSharpSyntaxRewriter
    {
      private readonly SemanticModel _semantic;
      private readonly Dictionary<IMethodSymbol, StopwatchInfo> _stopwatches;
      private readonly IMethodSymbol _entryMethod;
      private readonly CallGraphNode _callTreeRoot;
      private readonly string _filePath;
      private readonly Action<string>? _logCallback;

      private readonly HashSet<INamedTypeSymbol> _classesInGraph;
      private INamedTypeSymbol? _currentClass;
      private IMethodSymbol? _currentMethod;
      private bool _isInEntryPublic;

      // Mapeamentos de inserção (aplicados por bloco)
      private readonly Dictionary<StatementSyntax, (StatementSyntax Start, StatementSyntax Stop)> _wrapMap = new();
      private readonly Dictionary<ReturnStatementSyntax, (StatementSyntax Start, string TempName, ExpressionSyntax Invocation, StatementSyntax Stop)> _returnMap = new();
      private int _retCounter = 0;

      // Loops (nomes para criação de campos + wrap no statement do loop)
      private readonly List<string> _loopFields = new();

      public StableRewriter(
          SemanticModel semantic,
          Dictionary<IMethodSymbol, StopwatchInfo> stopwatches,
          IMethodSymbol entryMethod,
          CallGraphNode callTreeRoot,
          string filePath,
          Action<string>? onLog)
      {
        _semantic = semantic;
        _stopwatches = stopwatches;
        _entryMethod = entryMethod;
        _callTreeRoot = callTreeRoot;
        _filePath = filePath;
        _logCallback = onLog;

        _classesInGraph = new HashSet<INamedTypeSymbol>(
            stopwatches.Keys.Select(k => k.ContainingType).OfType<INamedTypeSymbol>(),
            SymbolEqualityComparer.Default);
      }

      public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
      {
        _currentClass = _semantic.GetDeclaredSymbol(node);
        if (_currentClass == null || !_classesInGraph.Contains(_currentClass))
          return base.VisitClassDeclaration(node);

        var visitedMembers = node.Members.Select(m => (MemberDeclarationSyntax)Visit(m) ?? m).ToList();

        // Declara fields de Stopwatch SOMENTE na classe do método de entrada
        if (!SymbolEqualityComparer.Default.Equals(_currentClass, _entryMethod.ContainingType))
          return node.WithMembers(SyntaxFactory.List(visitedMembers));

        var fieldDecls = new List<MemberDeclarationSyntax>();

        // Fields de métodos rastreados
        foreach (var kv in _stopwatches)
          fieldDecls.Add(CreateField(kv.Value.FieldName));

        // Fields de loops detectados
        foreach (var loop in _loopFields.Distinct())
          fieldDecls.Add(CreateField(loop));

        var finalMembers = new List<MemberDeclarationSyntax>();
        finalMembers.AddRange(fieldDecls);
        finalMembers.AddRange(visitedMembers);
        return node.WithMembers(SyntaxFactory.List(finalMembers));
      }

      private static FieldDeclarationSyntax CreateField(string name)
      {
        return SyntaxFactory.FieldDeclaration(
            SyntaxFactory.VariableDeclaration(
                SyntaxFactory.ParseTypeName("System.Diagnostics.Stopwatch"),
                SyntaxFactory.SeparatedList(new[]
                {
                            SyntaxFactory.VariableDeclarator(
                                SyntaxFactory.Identifier(name),
                                null,
                                SyntaxFactory.EqualsValueClause(
                                    SyntaxFactory.ObjectCreationExpression(
                                        SyntaxFactory.ParseTypeName("System.Diagnostics.Stopwatch"))
                                        .WithArgumentList(SyntaxFactory.ArgumentList())
                                ))
                })
            ))
        .WithModifiers(SyntaxFactory.TokenList(
            SyntaxFactory.Token(SyntaxKind.InternalKeyword),
            SyntaxFactory.Token(SyntaxKind.StaticKeyword),
            SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)));
      }

      public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
      {
        _currentMethod = _semantic.GetDeclaredSymbol(node);
        if (_currentMethod == null)
          return base.VisitMethodDeclaration(node);

        if (_currentClass == null || !_classesInGraph.Contains(_currentClass))
          return base.VisitMethodDeclaration(node);

        _isInEntryPublic = SymbolEqualityComparer.Default.Equals(_currentMethod, _entryMethod);

        _wrapMap.Clear();
        _returnMap.Clear();

        var body = node.Body;
        if (body == null)
          return node;

        // 1) Mapeamento (coleta tudo sem modificar árvore)
        _ = base.Visit(body);

        // 2) Aplicação (injeta no bloco correto, inclusive blocos internos)
        var injector = new BlockInjector(_wrapMap, _returnMap);
        var newBody = (BlockSyntax)injector.Visit(body);

        // 3) Acrescenta log hierárquico no método de entrada
        if (_isInEntryPublic)
        {
          var stmtsLog = BuildLogStatements(node.Identifier.Text);
          newBody = newBody.AddStatements(stmtsLog.ToArray());
        }

        return node.WithBody(newBody);
      }

      // -----------------------
      // LOOPs: mapear para wrap fora do corpo (Start antes / Stop depois)
      // -----------------------
      public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
      {
        if (ContainsTrackedInvocation(node.Statement))
        {
          var loopName = $"swLoop_{Guid.NewGuid().ToString("N")[..6]}";
          _loopFields.Add(loopName);

          var start = SyntaxFactory.ParseStatement($"{loopName}.Start();");
          var stop = SyntaxFactory.ParseStatement($"{loopName}.Stop();");
          _wrapMap[node] = (start, stop);

          _logCallback?.Invoke($"[MODIFY] Loop foreach ({Path.GetFileName(_filePath)})");
        }
        return base.VisitForEachStatement(node);
      }

      public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
      {
        if (ContainsTrackedInvocation(node.Statement))
        {
          var loopName = $"swLoop_{Guid.NewGuid().ToString("N")[..6]}";
          _loopFields.Add(loopName);

          var start = SyntaxFactory.ParseStatement($"{loopName}.Start();");
          var stop = SyntaxFactory.ParseStatement($"{loopName}.Stop();");
          _wrapMap[node] = (start, stop);

          _logCallback?.Invoke($"[MODIFY] Loop for ({Path.GetFileName(_filePath)})");
        }
        return base.VisitForStatement(node);
      }

      public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node)
      {
        if (ContainsTrackedInvocation(node.Statement))
        {
          var loopName = $"swLoop_{Guid.NewGuid().ToString("N")[..6]}";
          _loopFields.Add(loopName);

          var start = SyntaxFactory.ParseStatement($"{loopName}.Start();");
          var stop = SyntaxFactory.ParseStatement($"{loopName}.Stop();");
          _wrapMap[node] = (start, stop);

          _logCallback?.Invoke($"[MODIFY] Loop while ({Path.GetFileName(_filePath)})");
        }
        return base.VisitWhileStatement(node);
      }

      private bool ContainsTrackedInvocation(StatementSyntax stmt)
          => stmt.DescendantNodes()
                 .OfType<InvocationExpressionSyntax>()
                 .Any(inv => ResolveTarget(inv) != null);

      // -----------------------
      // Chamadas normais: Foo();
      // -----------------------
      public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
      {
        if (node.Expression is not InvocationExpressionSyntax invocation)
          return base.VisitExpressionStatement(node);

        var target = ResolveTarget(invocation);
        if (target == null)
          return base.VisitExpressionStatement(node);

        var (start, stop) = BuildStartStop(target);
        _wrapMap[node] = (start, stop);

        _logCallback?.Invoke($"[MODIFY] {target.ContainingType.Name}.{target.Name} ({Path.GetFileName(_filePath)})");
        return base.VisitExpressionStatement(node);
      }

      // -----------------------
      // var x = Foo();
      // -----------------------
      public override SyntaxNode? VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
      {
        var decl = node.Declaration;
        if (decl?.Variables.Count != 1)
          return base.VisitLocalDeclarationStatement(node);

        var variable = decl.Variables[0];
        if (variable.Initializer?.Value is not InvocationExpressionSyntax invocation)
          return base.VisitLocalDeclarationStatement(node);

        var target = ResolveTarget(invocation);
        if (target == null)
          return base.VisitLocalDeclarationStatement(node);

        var (start, stop) = BuildStartStop(target);
        _wrapMap[node] = (start, stop);

        _logCallback?.Invoke($"[MODIFY] {target.ContainingType.Name}.{target.Name} (LocalDeclaration - {Path.GetFileName(_filePath)})");
        return base.VisitLocalDeclarationStatement(node);
      }

      // -----------------------
      // return Foo();
      // -----------------------
      public override SyntaxNode? VisitReturnStatement(ReturnStatementSyntax node)
      {
        if (node.Expression is not InvocationExpressionSyntax invocation)
          return base.VisitReturnStatement(node);

        var target = ResolveTarget(invocation);
        if (target == null)
          return base.VisitReturnStatement(node);

        var (start, stop) = BuildStartStop(target);

        var tempName = $"__ret{++_retCounter}";
        _returnMap[node] = (start, tempName, invocation, stop);

        _logCallback?.Invoke($"[MODIFY] {target.ContainingType.Name}.{target.Name} (Return - {Path.GetFileName(_filePath)})");
        return base.VisitReturnStatement(node);
      }

      private (StatementSyntax Start, StatementSyntax Stop) BuildStartStop(IMethodSymbol targetSymbol)
      {
        if (!_stopwatches.TryGetValue(targetSymbol.OriginalDefinition, out var swInfo) &&
            !_stopwatches.TryGetValue(targetSymbol, out swInfo))
          throw new InvalidOperationException("Stopwatch não encontrado para o método.");

        var qualified = $"{_entryMethod.ContainingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}.{swInfo.FieldName}";
        var start = SyntaxFactory.ParseStatement($"{qualified}.Start();");
        var stop = SyntaxFactory.ParseStatement($"{qualified}.Stop();");
        return (start, stop);
      }

      private IMethodSymbol? ResolveTarget(InvocationExpressionSyntax invocation)
      {
        var info = _semantic.GetSymbolInfo(invocation);
        var target = info.Symbol as IMethodSymbol
                     ?? info.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
        if (target == null) return null;

        // pular BCL/framework
        var ns = target.ContainingNamespace?.ToString() ?? "";
        if (ns.StartsWith("System", StringComparison.Ordinal) ||
            ns.StartsWith("Microsoft", StringComparison.Ordinal))
          return null;

        // pular escrita de log e folhas irrelevantes
        var name = target.Name;
        var typeName = target.ContainingType?.Name ?? "";
        if (name.Equals("Write", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("LoadItems", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Main", StringComparison.OrdinalIgnoreCase) ||
            typeName.IndexOf("LogService", StringComparison.OrdinalIgnoreCase) >= 0)
          return null;

        // Só instrumenta se houver stopwatch registrado (faz parte do grafo)
        return _stopwatches.ContainsKey(target.OriginalDefinition) || _stopwatches.ContainsKey(target)
            ? target
            : null;
      }

      // -----------------------
      // Injeta substituindo statements no bloco certo (recursivo)
      // -----------------------
      private sealed class BlockInjector : CSharpSyntaxRewriter
      {
        private readonly Dictionary<StatementSyntax, (StatementSyntax Start, StatementSyntax Stop)> _wrapMap;
        private readonly Dictionary<ReturnStatementSyntax, (StatementSyntax Start, string TempName, ExpressionSyntax Invocation, StatementSyntax Stop)> _returnMap;

        public BlockInjector(
            Dictionary<StatementSyntax, (StatementSyntax Start, StatementSyntax Stop)> wrapMap,
            Dictionary<ReturnStatementSyntax, (StatementSyntax Start, string TempName, ExpressionSyntax Invocation, StatementSyntax Stop)> returnMap)
        {
          _wrapMap = wrapMap;
          _returnMap = returnMap;
        }

        public override SyntaxNode? VisitBlock(BlockSyntax node)
        {
          var newStatements = new List<StatementSyntax>();

          foreach (var s in node.Statements)
          {
            // Caso especial: return Foo();
            if (s is ReturnStatementSyntax r && _returnMap.TryGetValue(r, out var rinfo))
            {
              // Não visitamos r (vamos gerar novo conjunto)
              var decl = SyntaxFactory.ParseStatement($"var {rinfo.TempName} = {rinfo.Invocation.ToFullString()};");
              var ret = SyntaxFactory.ParseStatement($"return {rinfo.TempName};");

              newStatements.Add(rinfo.Start);
              newStatements.Add(decl);
              newStatements.Add(rinfo.Stop);
              newStatements.Add(ret);
              continue;
            }

            // Visita normal (para permitir instrumentação em blocos internos)
            var visited = (StatementSyntax?)base.Visit(s) ?? s;

            if (_wrapMap.TryGetValue(s, out var pair))
            {
              newStatements.Add(pair.Start);
              newStatements.Add(visited); // statement (ou loop) já visitado internamente
              newStatements.Add(pair.Stop);
            }
            else
            {
              newStatements.Add(visited);
            }
          }

          return node.WithStatements(SyntaxFactory.List(newStatements));
        }
      }

      private IEnumerable<StatementSyntax> BuildLogStatements(string entryName)
      {
        var stmts = new List<StatementSyntax>
                {
                    SyntaxFactory.ParseStatement("var logStopwatch = new System.Text.StringBuilder();"),
                    SyntaxFactory.ParseStatement($"logStopwatch.AppendLine(\"[Dentro do método: {entryName}]\");")
                };

        var visited = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
        var baseType = _entryMethod.ContainingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        static bool IsExcludedForLog(IMethodSymbol m)
        {
          var n = m.Name;
          return n.Equals("Write", StringComparison.OrdinalIgnoreCase)
              || n.Equals("LoadItems", StringComparison.OrdinalIgnoreCase)
              || n.Equals("Main", StringComparison.OrdinalIgnoreCase);
        }

        bool TryGetSw(IMethodSymbol symbol, out string swRef)
        {
          if (_stopwatches.TryGetValue(symbol, out var sw) ||
              _stopwatches.TryGetValue(symbol.OriginalDefinition, out sw))
          {
            swRef = $"{baseType}.{sw.FieldName}";
            return true;
          }
          swRef = "";
          return false;
        }

        string Bars(int depth) => string.Concat(Enumerable.Repeat("| ", depth));

        void Walk(CallGraphNode node, int depth)
        {
          foreach (var child in node.Children)
          {
            var sym = child.Symbol;

            if (IsExcludedForLog(sym))
            {
              Walk(child, depth);
              continue;
            }

            if (!visited.Add(sym))
            {
              Walk(child, depth);
              continue;
            }

            if (TryGetSw(sym, out var swRef))
            {
              var bars = Bars(depth + 1);
              stmts.Add(SyntaxFactory.ParseStatement(
                  $"logStopwatch.AppendLine($\"{bars}[Metodo: {sym.Name}] Tempo: {{{swRef}.ElapsedMilliseconds}} ms - {{{swRef}.Elapsed}}\");"));
              stmts.Add(SyntaxFactory.ParseStatement(
                  $"logStopwatch.AppendLine($\"{bars}[Dentro do método: {sym.Name}]\");"));
            }

            Walk(child, depth + 1);
          }
        }

        // monta hierarquia a partir do nó raiz
        Walk(_callTreeRoot, 0);

        // loops detectados (nível 1)
        foreach (var loop in _loopFields.Distinct())
        {
          var bars = Bars(1);
          stmts.Add(SyntaxFactory.ParseStatement(
              $"logStopwatch.AppendLine($\"{bars}[Loop: {loop}] Tempo: {{{loop}.ElapsedMilliseconds}} ms - {{{loop}.Elapsed}}\");"));
        }

        return stmts;
      }
    }
  }
}
