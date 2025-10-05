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
  /// CodeRewriter com duas fases:
  /// 1️⃣ Mapeia todos os pontos de injeção (métodos e loops).
  /// 2️⃣ Aplica as modificações de forma consolidada.
  /// 
  /// - Sem blocos { } extras
  /// - Sem duplicações
  /// - Com suporte total a loops acumulativos
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

        var rewriter = new StableRewriter(model, stopwatchByMethod, entryMethod, filePath, OnLog);
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
      private readonly string _filePath;
      private readonly Action<string>? _logCallback;

      private readonly HashSet<INamedTypeSymbol> _classesInGraph;
      private INamedTypeSymbol? _currentClass;
      private IMethodSymbol? _currentMethod;
      private bool _isInEntryPublic;

      // armazenam os pontos de injeção detectados
      private readonly List<(StatementSyntax Target, StatementSyntax Start, StatementSyntax Stop)> _inserts = new();
      private readonly List<(StatementSyntax LoopNode, StatementSyntax Start, StatementSyntax Stop, string LoopName)> _loopInserts = new();

      private readonly List<LogLine> _log = new();
      private readonly List<string> _loopFields = new();

      public StableRewriter(
          SemanticModel semantic,
          Dictionary<IMethodSymbol, StopwatchInfo> stopwatches,
          IMethodSymbol entryMethod,
          string filePath,
          Action<string>? onLog)
      {
        _semantic = semantic;
        _stopwatches = stopwatches;
        _entryMethod = entryMethod;
        _filePath = filePath;
        _logCallback = onLog;

        _classesInGraph = new HashSet<INamedTypeSymbol>(
            stopwatches.Keys.Select(k => k.ContainingType).OfType<INamedTypeSymbol>(),
            SymbolEqualityComparer.Default);
      }

      // Cria stopwatches no topo da classe base
      public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
      {
        _currentClass = _semantic.GetDeclaredSymbol(node);
        if (_currentClass == null || !_classesInGraph.Contains(_currentClass))
          return base.VisitClassDeclaration(node);

        var visitedMembers = node.Members.Select(m => (MemberDeclarationSyntax)Visit(m) ?? m).ToList();

        if (!SymbolEqualityComparer.Default.Equals(_currentClass, _entryMethod.ContainingType))
          return node.WithMembers(SyntaxFactory.List(visitedMembers));

        var fieldDecls = new List<MemberDeclarationSyntax>();

        foreach (var kv in _stopwatches)
        {
          var fieldDecl = CreateField(kv.Value.FieldName);
          fieldDecls.Add(fieldDecl);
        }

        foreach (var loop in _loopFields.Distinct())
        {
          var fieldDecl = CreateField(loop);
          fieldDecls.Add(fieldDecl);
        }

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
        _inserts.Clear();
        _loopInserts.Clear();
        _log.Clear();

        var newBody = node.Body;
        if (newBody == null)
          return node;

        // 1️⃣ Fase de mapeamento
        _ = base.Visit(newBody);

        // 2️⃣ Fase de aplicação - loops primeiro
        var stmts = newBody.Statements.ToList();

        foreach (var insert in _loopInserts.OrderByDescending(i => stmts.IndexOf(i.LoopNode)))
        {
          var idx = stmts.IndexOf(insert.LoopNode);
          if (idx >= 0)
          {
            stmts.Insert(idx, insert.Start);
            stmts.Insert(idx + 2, insert.Stop);
          }
        }

        // 3️⃣ Aplicação normal de chamadas
        foreach (var ins in _inserts.OrderByDescending(t => stmts.IndexOf(t.Target)))
        {
          var idx = stmts.IndexOf(ins.Target);
          if (idx >= 0)
          {
            // Insere Start antes do alvo e Stop depois do alvo
            stmts.Insert(idx, ins.Start);
            stmts.Insert(idx + 2, ins.Stop);
          }
        }

        newBody = newBody.WithStatements(SyntaxFactory.List(stmts));

        if (_isInEntryPublic)
        {
          var stmtsLog = BuildLogStatements(node.Identifier.Text);
          newBody = newBody.AddStatements(stmtsLog.ToArray());
        }

        return node.WithBody(newBody);
      }

      // --- Detecção de loops com chamadas internas
      public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
      {
        if (ContainsTrackedInvocation(node.Statement))
        {
          var loopName = $"swLoop_{Guid.NewGuid().ToString("N")[..6]}";
          _loopFields.Add(loopName);

          var start = SyntaxFactory.ParseStatement($"{loopName}.Start();");
          var stop = SyntaxFactory.ParseStatement($"{loopName}.Stop();");
          _loopInserts.Add((node, start, stop, loopName));

          _logCallback?.Invoke($"[MODIFY] Loop foreach ({Path.GetFileName(_filePath)})");
          if (_isInEntryPublic)
            _log.Add(LogLine.Loop(1, loopName));
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
          _loopInserts.Add((node, start, stop, loopName));

          _logCallback?.Invoke($"[MODIFY] Loop for ({Path.GetFileName(_filePath)})");
          if (_isInEntryPublic)
            _log.Add(LogLine.Loop(1, loopName));
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
          _loopInserts.Add((node, start, stop, loopName));

          _logCallback?.Invoke($"[MODIFY] Loop while ({Path.GetFileName(_filePath)})");
          if (_isInEntryPublic)
            _log.Add(LogLine.Loop(1, loopName));
        }

        return base.VisitWhileStatement(node);
      }

      private bool ContainsTrackedInvocation(StatementSyntax stmt)
      {
        return stmt.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(inv => ResolveTarget(inv) != null);
      }

      // --- Detecção normal de chamadas
      public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
      {
        if (node.Expression is not InvocationExpressionSyntax invocation)
          return base.VisitExpressionStatement(node);

        var target = ResolveTarget(invocation);
        if (target == null)
          return base.VisitExpressionStatement(node);

        CreateInsert(node, target, "Expression");
        return base.VisitExpressionStatement(node);
      }

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

        CreateInsert(node, target, "LocalDeclaration");
        return base.VisitLocalDeclarationStatement(node);
      }

      public override SyntaxNode? VisitReturnStatement(ReturnStatementSyntax node)
      {
        if (node.Expression is not InvocationExpressionSyntax invocation)
          return base.VisitReturnStatement(node);

        var target = ResolveTarget(invocation);
        if (target == null)
          return base.VisitReturnStatement(node);

        CreateInsert(node, target, "Return");
        return base.VisitReturnStatement(node);
      }

      private IMethodSymbol? ResolveTarget(InvocationExpressionSyntax invocation)
      {
        var info = _semantic.GetSymbolInfo(invocation);
        var target = info.Symbol as IMethodSymbol ?? info.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
        if (target == null)
          return null;

        var ns = target.ContainingNamespace?.ToString() ?? "";
        if (ns.StartsWith("System") || ns.StartsWith("Microsoft"))
          return null;

        return _stopwatches.ContainsKey(target.OriginalDefinition) || _stopwatches.ContainsKey(target)
            ? target
            : null;
      }

      private void CreateInsert(StatementSyntax node, IMethodSymbol targetSymbol, string context)
      {
        if (!_stopwatches.TryGetValue(targetSymbol.OriginalDefinition, out var swInfo) &&
            !_stopwatches.TryGetValue(targetSymbol, out swInfo))
          return;

        var qualified = $"{_entryMethod.ContainingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}.{swInfo.FieldName}";
        var start = SyntaxFactory.ParseStatement($"{qualified}.Start();");
        var stop = SyntaxFactory.ParseStatement($"{qualified}.Stop();");

        _inserts.Add((node, start, stop));

        _logCallback?.Invoke($"[MODIFY] {targetSymbol.ContainingType.Name}.{targetSymbol.Name} ({context} - {Path.GetFileName(_filePath)})");
        if (_isInEntryPublic)
          _log.Add(LogLine.Method(1, targetSymbol.Name, qualified));
      }

      private IEnumerable<StatementSyntax> BuildLogStatements(string entryName)
      {
        var stmts = new List<StatementSyntax>
                {
                    SyntaxFactory.ParseStatement("var logStopwatch = new System.Text.StringBuilder();"),
                    SyntaxFactory.ParseStatement($"logStopwatch.AppendLine(\"[Dentro do método: {entryName}]\");")
                };

        foreach (var l in _log)
        {
          var bars = new string('|', l.Depth * 2);
          if (l.IsLoop)
            stmts.Add(SyntaxFactory.ParseStatement(
                $"logStopwatch.AppendLine($\"{bars}[Loop: {l.StopwatchRef}] Tempo: {{{l.StopwatchRef}.ElapsedMilliseconds}} ms - {{{l.StopwatchRef}.Elapsed}}\");"));
          else
            stmts.Add(SyntaxFactory.ParseStatement(
                $"logStopwatch.AppendLine($\"{bars}[Metodo: {l.MethodName}] Tempo: {{{l.StopwatchRef}.ElapsedMilliseconds}} ms - {{{l.StopwatchRef}.Elapsed}}\");"));
        }

        return stmts;
      }

      private sealed class LogLine
      {
        public int Depth { get; set; }
        public string MethodName { get; set; } = "";
        public string StopwatchRef { get; set; } = "";
        public bool IsLoop { get; set; }

        public static LogLine Method(int depth, string name, string sw)
            => new() { Depth = depth, MethodName = name, StopwatchRef = sw };
        public static LogLine Loop(int depth, string sw)
            => new() { Depth = depth, StopwatchRef = sw, IsLoop = true };
      }
    }
  }
}
