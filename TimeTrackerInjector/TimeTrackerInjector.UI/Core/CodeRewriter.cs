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
  /// CodeRewriter: responsável por modificar os arquivos C# e injetar medições de tempo.
  /// 
  /// 🔧 Funcionalidades principais:
  /// - Cria Stopwatches (campos) no topo da classe base.
  /// - Injeta Start/Stop em torno de chamadas relevantes em todas as classes do grafo.
  /// - Mantém loops originais, adicionando Start/Stop dentro do corpo.
  /// - Adiciona bloco de log hierárquico no método público de entrada.
  /// - Gera eventos de log (para exibição no MainForm).
  /// </summary>
  public class CodeRewriter
  {
    private readonly TimeTrackerConfig _config;

    /// <summary>
    /// Evento para notificar o MainForm sobre modificações realizadas nos arquivos.
    /// </summary>
    public event Action<string>? OnLog;

    public CodeRewriter(TimeTrackerConfig config)
    {
      _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Executa a instrumentação em todos os arquivos pertencentes à classe base e às classes do grafo de chamadas.
    /// </summary>
    public async Task RewriteAsync(
        Compilation compilation,
        CallGraphNode callTreeRoot,
        IReadOnlyList<AnalyzedMethod> methods,
        IMethodSymbol entryMethod)
    {
      if (methods == null || methods.Count == 0)
        return;

      var entryClass = entryMethod.ContainingType;
      var entryClassName = entryClass?.Name ?? string.Empty;

      // Cria lista de classes e arquivos envolvidos no grafo
      var involvedClasses = new HashSet<string>(
          methods.Select(m => m.ClassName),
          StringComparer.Ordinal);

      var involvedFiles = methods
          .Where(m => involvedClasses.Contains(m.ClassName))
          .Select(m => m.FilePath)
          .Where(f => !string.IsNullOrWhiteSpace(f))
          .Distinct()
          .ToList();

      // Cria stopwatches (campos) – sempre atribuídos à classe base
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

        var rewriter = new DeepHierarchyRewriter(model, stopwatchByMethod, entryMethod, filePath, OnLog);
        var newRoot = (CompilationUnitSyntax)rewriter.Visit(root);

        // Salva o arquivo diretamente (sem backup)
        var newCode = newRoot.NormalizeWhitespace().ToFullString();
        await File.WriteAllTextAsync(filePath, newCode, Encoding.UTF8);
      }
    }

    /// <summary>
    /// Cria dicionário de Stopwatches (um por método) — os campos são sempre declarados na classe base.
    /// </summary>
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

    private sealed class DeepHierarchyRewriter : CSharpSyntaxRewriter
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

      private int _depth;
      private const int MaxDepth = 50;

      private readonly List<LogLine> _log = new();
      private readonly List<string> _loopFieldsToAdd = new();
      private int _loopCounter = 0;

      public DeepHierarchyRewriter(
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

        // Todas as classes que contêm métodos do grafo (ex: ProcessService, DataService, etc.)
        _classesInGraph = new HashSet<INamedTypeSymbol>(
            stopwatches.Keys
                .Select(k => k.ContainingType)
                .OfType<INamedTypeSymbol>(),
            SymbolEqualityComparer.Default);
      }

      public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
      {
        _currentClass = _semantic.GetDeclaredSymbol(node);

        // Se a classe não faz parte do grafo → não altera
        if (_currentClass == null || !_classesInGraph.Contains(_currentClass))
          return base.VisitClassDeclaration(node);

        var visitedMembers = node.Members.Select(m => (MemberDeclarationSyntax)Visit(m) ?? m).ToList();

        // Adiciona campos de Stopwatch apenas na classe base
        if (!SymbolEqualityComparer.Default.Equals(_currentClass, _entryMethod.ContainingType))
          return node.WithMembers(SyntaxFactory.List(visitedMembers));

        var fieldDecls = new List<MemberDeclarationSyntax>();

        // Campos de métodos
        foreach (var kv in _stopwatches)
        {
          var fieldDecl =
              SyntaxFactory.FieldDeclaration(
                  SyntaxFactory.VariableDeclaration(
                      SyntaxFactory.ParseTypeName("System.Diagnostics.Stopwatch"),
                      SyntaxFactory.SeparatedList(new[]
                      {
                                    SyntaxFactory.VariableDeclarator(
                                        SyntaxFactory.Identifier(kv.Value.FieldName),
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

          fieldDecls.Add(fieldDecl);
        }

        // Campos de loops (descobertos durante visita)
        foreach (var loop in _loopFieldsToAdd.Distinct())
        {
          var loopDecl =
              SyntaxFactory.FieldDeclaration(
                  SyntaxFactory.VariableDeclaration(
                      SyntaxFactory.ParseTypeName("System.Diagnostics.Stopwatch"),
                      SyntaxFactory.SeparatedList(new[]
                      {
                                    SyntaxFactory.VariableDeclarator(
                                        SyntaxFactory.Identifier(loop),
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

          fieldDecls.Add(loopDecl);
        }

        var finalMembers = new List<MemberDeclarationSyntax>(fieldDecls.Count + visitedMembers.Count);
        finalMembers.AddRange(fieldDecls);
        finalMembers.AddRange(visitedMembers);

        return node.WithMembers(SyntaxFactory.List(finalMembers));
      }

      public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
      {
        _currentMethod = _semantic.GetDeclaredSymbol(node);

        if (_currentMethod == null)
          return base.VisitMethodDeclaration(node);

        // Só processa métodos que estão dentro das classes do grafo
        if (_currentClass == null || !_classesInGraph.Contains(_currentClass))
          return base.VisitMethodDeclaration(node);

        _isInEntryPublic = SymbolEqualityComparer.Default.Equals(_currentMethod, _entryMethod);

        _depth = 0;
        _log.Clear();

        var newBody = (BlockSyntax?)Visit(node.Body);
        if (newBody == null)
          return node;

        if (_isInEntryPublic)
        {
          var stmts = BuildLogStatements(node.Identifier.Text);
          newBody = newBody.AddStatements(stmts.ToArray());
        }

        return node.WithBody(newBody);
      }

      public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
      {
        if (_depth >= MaxDepth)
          return node;

        if (node.Expression is not InvocationExpressionSyntax invocation)
          return base.VisitExpressionStatement(node);

        var info = _semantic.GetSymbolInfo(invocation);
        var targetSymbol = info.Symbol as IMethodSymbol
                        ?? info.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();

        if (targetSymbol == null)
          return base.VisitExpressionStatement(node);

        // Ignora chamadas externas irrelevantes
        var ns = targetSymbol.ContainingNamespace?.ToString() ?? "";
        var type = targetSymbol.ContainingType?.Name ?? "";

        if (ns.StartsWith("System") || ns.StartsWith("Microsoft") || type.Contains("LogService"))
          return base.VisitExpressionStatement(node);

        // Apenas métodos que estão no grafo são instrumentados
        if (_stopwatches.TryGetValue(targetSymbol.OriginalDefinition, out var swInfo) ||
            _stopwatches.TryGetValue(targetSymbol, out swInfo))
        {
          var qualified = $"{_entryMethod.ContainingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}.{swInfo.FieldName}";
          var start = SyntaxFactory.ParseStatement($"{qualified}.Start();");
          var stop = SyntaxFactory.ParseStatement($"{qualified}.Stop();");

          _logCallback?.Invoke($"[MODIFY] {targetSymbol.ContainingType.Name}.{targetSymbol.Name} ({Path.GetFileName(_filePath)})");

          if (_isInEntryPublic)
            _log.Add(LogLine.Method(_depth + 1, targetSymbol.Name, qualified));

          return SyntaxFactory.Block(start, node, stop);
        }

        return base.VisitExpressionStatement(node);
      }

      public override SyntaxNode? VisitForStatement(ForStatementSyntax node) => RewriteLoop(node, node.Statement);
      public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node) => RewriteLoop(node, node.Statement);
      public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node) => RewriteLoop(node, node.Statement);

      private SyntaxNode RewriteLoop(SyntaxNode loopNode, StatementSyntax body)
      {
        if (_depth >= MaxDepth)
          return loopNode;

        _loopCounter++;
        var loopSw = $"swLoop{_loopCounter}";
        _loopFieldsToAdd.Add(loopSw);

        if (_isInEntryPublic)
          _log.Add(LogLine.Loop(_depth + 1, loopSw));

        var start = SyntaxFactory.ParseStatement($"{loopSw}.Start();");
        var stop = SyntaxFactory.ParseStatement($"{loopSw}.Stop();");

        var block = body as BlockSyntax ?? SyntaxFactory.Block(body);
        var newBlock = block.WithStatements(block.Statements.Insert(0, start).Add(stop));

        return loopNode switch
        {
          ForStatementSyntax f => f.WithStatement(newBlock),
          ForEachStatementSyntax fe => fe.WithStatement(newBlock),
          WhileStatementSyntax w => w.WithStatement(newBlock),
          _ => loopNode
        };
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
          var bars = string.Concat(Enumerable.Repeat("| ", l.Depth));
          if (l.IsLoop)
            stmts.Add(SyntaxFactory.ParseStatement(
                $"logStopwatch.AppendLine($\"{bars}[Loop: {l.StopwatchRef}] Tempo: {{{l.StopwatchRef}.ElapsedMilliseconds}} ms - {{{l.StopwatchRef}.Elapsed}}\");"));
          else
          {
            stmts.Add(SyntaxFactory.ParseStatement(
                $"logStopwatch.AppendLine($\"{bars}[Metodo: {l.MethodName}] Tempo: {{{l.StopwatchRef}.ElapsedMilliseconds}} ms - {{{l.StopwatchRef}.Elapsed}}\");"));
            stmts.Add(SyntaxFactory.ParseStatement(
                $"logStopwatch.AppendLine($\"{bars}[Dentro do método: {l.MethodName}]\");"));
          }
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
            => new() { Depth = depth, MethodName = name, StopwatchRef = sw, IsLoop = false };
        public static LogLine Loop(int depth, string sw)
            => new() { Depth = depth, StopwatchRef = sw, IsLoop = true };
      }
    }
  }
}
