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
  /// Reescreve o código criando campos de Stopwatch por método (na classe declaradora),
  /// envolvendo invocações com Start/Stop qualificados pela classe do alvo,
  /// e inserindo um único bloco logStopwatch no final do método público de entrada,
  /// imprimindo toda a árvore de chamadas (hierarquia profunda) + loops detectados no entry.
  /// </summary>
  public class CodeRewriter
  {
    private readonly TimeTrackerConfig _config;

    public CodeRewriter(TimeTrackerConfig config)
    {
      _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <param name="compilation">Compilation do projeto.</param>
    /// <param name="callTreeRoot">Árvore de chamadas do método raiz.</param>
    /// <param name="methods">Lista de métodos analisados (para limitar arquivos).</param>
    /// <param name="entryMethod">Símbolo do método público de entrada (onde o log será inserido).</param>
    public async Task RewriteAsync(
        Compilation compilation,
        CallGraphNode callTreeRoot,
        IReadOnlyList<AnalyzedMethod> methods,
        IMethodSymbol entryMethod)
    {
      if (methods == null || methods.Count == 0) return;

      // 1) Gera um registro Stopwatch por método (nome único e classe declaradora)
      var stopwatchByMethod = BuildStopwatchRegistry(callTreeRoot);

      // 2) Por arquivo: reescrever com semantic model
      foreach (var group in methods
                   .Where(m => !string.IsNullOrWhiteSpace(m.FilePath))
                   .GroupBy(m => m.FilePath))
      {
        var filePath = group.Key;
        if (!File.Exists(filePath)) continue;

        var doc = compilation.SyntaxTrees.FirstOrDefault(t => t.FilePath == filePath);
        if (doc == null) continue;

        var model = compilation.GetSemanticModel(doc);
        var root = (await doc.GetRootAsync()) as CompilationUnitSyntax;
        if (root == null) continue;

        var rewriter = new DeepHierarchyRewriter(model, stopwatchByMethod, entryMethod);
        var newRoot = (CompilationUnitSyntax)rewriter.Visit(root);

        // 3) Backup + persistência
        var newCode = newRoot.NormalizeWhitespace().ToFullString();
        var backupPath = filePath + ".bak";
        if (!_config.OverwriteOriginal && !File.Exists(backupPath))
          File.Copy(filePath, backupPath);
        await File.WriteAllTextAsync(filePath, newCode, Encoding.UTF8);
      }
    }

    private static Dictionary<IMethodSymbol, StopwatchInfo> BuildStopwatchRegistry(CallGraphNode root)
    {
      var dict = new Dictionary<IMethodSymbol, StopwatchInfo>(SymbolEqualityComparer.Default);
      int counter = 1;

      void Walk(CallGraphNode n)
      {
        var sym = n.Symbol;
        if (!dict.ContainsKey(sym))
        {
          var className = sym.ContainingType?.Name ?? "UnknownClass";
          var cleanMethod = new string(sym.Name.Where(char.IsLetterOrDigit).ToArray());
          dict[sym] = new StopwatchInfo
          {
            DeclaringClassName = sym.ContainingType?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                                   ?? className,
            FieldName = $"sw{counter++}_{cleanMethod}"
          };
        }
        foreach (var c in n.Children) Walk(c);
      }

      Walk(root);
      return dict;
    }

    /// <summary>
    /// Rewriter principal: injeta fields nas classes donas dos métodos;
    /// envolve invocações com Start/Stop; adiciona bloco de log no método público raiz.
    /// </summary>
    private sealed class DeepHierarchyRewriter : CSharpSyntaxRewriter
    {
      private readonly SemanticModel _semantic;
      private readonly Dictionary<IMethodSymbol, StopwatchInfo> _stopwatches;
      private readonly IMethodSymbol _entryMethod;

      // Estado por arquivo durante a visita
      private readonly HashSet<string> _fieldsInjectedInThisClass = new(); // ClassName -> fields already inserted
      private INamedTypeSymbol? _currentClass;
      private IMethodSymbol? _currentMethod;
      private bool _isInEntryPublic;
      private int _depth;
      private readonly List<LogLine> _log = new();
      private int _loopCounter = 1;
      private readonly List<string> _loopFieldNames = new();

      public DeepHierarchyRewriter(
          SemanticModel semantic,
          Dictionary<IMethodSymbol, StopwatchInfo> stopwatches,
          IMethodSymbol entryMethod)
      {
        _semantic = semantic;
        _stopwatches = stopwatches;
        _entryMethod = entryMethod;
      }

      public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
      {
        _currentClass = _semantic.GetDeclaredSymbol(node);
        var visitedMembers = node.Members.Select(m => (MemberDeclarationSyntax)Visit(m) ?? m).ToList();

        // Campos Stopwatch que pertencem a esta classe
        var fieldsToAdd = new List<MemberDeclarationSyntax>();

        foreach (var kv in _stopwatches)
        {
          var owner = kv.Key.ContainingType;
          if (owner == null) continue;
          if (!SymbolEqualityComparer.Default.Equals(owner, _currentClass)) continue;

          var classNameKey = owner.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
          var uniqKey = $"{classNameKey}.{kv.Value.FieldName}";
          if (_fieldsInjectedInThisClass.Contains(uniqKey)) continue;

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

          fieldsToAdd.Add(fieldDecl);
          _fieldsInjectedInThisClass.Add(uniqKey);
        }

        if (fieldsToAdd.Count == 0) return node.WithMembers(SyntaxFactory.List(visitedMembers));

        // Insere no topo da classe
        var finalMembers = new List<MemberDeclarationSyntax>(fieldsToAdd.Count + visitedMembers.Count);
        finalMembers.AddRange(fieldsToAdd);
        finalMembers.AddRange(visitedMembers);
        return node.WithMembers(SyntaxFactory.List(finalMembers));
      }

      public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
      {
        _currentMethod = _semantic.GetDeclaredSymbol(node);
        _isInEntryPublic = SymbolEqualityComparer.Default.Equals(_currentMethod, _entryMethod) &&
                           node.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));

        _depth = 0;
        _log.Clear();
        _loopCounter = 1;
        _loopFieldNames.Clear();

        var newBody = (BlockSyntax?)Visit(node.Body);
        if (newBody == null) return node;

        if (_isInEntryPublic)
        {
          // Gera bloco logStopwatch (profundo) com base no que foi coletado
          var statements = BuildLogStatements(node.Identifier.Text);
          newBody = newBody.AddStatements(statements.ToArray());
        }

        return node.WithBody(newBody);
      }

      // ---------- INVOCATIONS ----------
      public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
      {
        // Detecta se o statement é uma chamada de método (InvocationExpression)
        if (node.Expression is InvocationExpressionSyntax invocation)
        {
          var targetSymbol = _semantic.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
          if (targetSymbol != null &&
              (_stopwatches.TryGetValue(targetSymbol.OriginalDefinition, out var swInfo) ||
               _stopwatches.TryGetValue(targetSymbol, out swInfo)))
          {
            // Qualifica o Stopwatch
            var qualified = $"{swInfo.DeclaringClassName}.{swInfo.FieldName}";
            var startStmt = SyntaxFactory.ParseStatement($"{qualified}.Start();");
            var stopStmt = SyntaxFactory.ParseStatement($"{qualified}.Stop();");

            // Adiciona entrada no log, se estivermos no método público de entrada
            if (_isInEntryPublic)
              _log.Add(LogLine.Method(_depth + 1, targetSymbol.Name, qualified));

            // Retorna bloco com Start + chamada original + Stop
            return SyntaxFactory.Block(startStmt, node, stopStmt);
          }
        }

        // Se não for uma chamada de método alvo, segue o fluxo normal
        return base.VisitExpressionStatement(node);
      }


      // ---------- LOOPS ----------

      public override SyntaxNode? VisitForStatement(ForStatementSyntax node) => RewriteLoop(node, node);
      public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node) => RewriteLoop(node, node);
      public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node) => RewriteLoop(node, node);

      private SyntaxNode RewriteLoop(SyntaxNode loopNode, StatementSyntax loopStmt)
      {
        // Apenas coleta logs/fields de loop no método de entrada
        string? loopSw = null;
        if (_isInEntryPublic)
        {
          loopSw = $"swLoop_{_loopCounter++}";
          _loopFieldNames.Add(loopSw);
          _log.Add(LogLine.Loop(_depth + 1, loopSw));
        }

        _depth++;
        var visited = (StatementSyntax?)base.Visit(loopStmt) ?? loopStmt;
        _depth--;

        if (loopSw == null) return visited;

        // Campo de loop precisa existir na classe corrente. Se não existir, criaremos via cabeçalho de classe?
        // Como loops são locais, vamos usar campo estático interno na classe de entrada também:
        // (o campo será adicionado quando visitarmos a ClassDeclaration — aqui apenas usamos)
        // Para garantir, vamos declarar como field no fim do método? Não é válido. Então a criação acontece no topo:
        // --> A estratégia simples: não criar field de loop global aqui; usar var local.
        // Porém precisamos acumular. Para simplificar, faremos var local com new Stopwatch():
        // e acumularemos apenas nesse escopo.
        // Como você quer acumulado geral, manteremos como local do método entry:
        // Start/Stop englobando o laço.

        var start = SyntaxFactory.ParseStatement($"{loopSw}.Start();");
        var stop = SyntaxFactory.ParseStatement($"{loopSw}.Stop();");

        // Declaração local do loopSw no início do bloco (se ainda não existir). Vamos inserir como 'var swLoop_X = new Stopwatch();'
        // Para garantir, transformamos o visited em bloco, e prefixamos com a declaração.
        var block = visited as BlockSyntax ?? SyntaxFactory.Block(visited);
        var decl = SyntaxFactory.ParseStatement($"var {loopSw} = new System.Diagnostics.Stopwatch();");

        // Retorna: { var swLoop_X = new Stopwatch(); swLoop_X.Start(); <loop>; swLoop_X.Stop(); }
        return SyntaxFactory.Block(decl, start, block, stop);
      }

      // ---------- LOG BUILD ----------

      private IEnumerable<StatementSyntax> BuildLogStatements(string entryMethodName)
      {
        var stmts = new List<StatementSyntax>
                {
                    SyntaxFactory.ParseStatement("var logStopwatch = new System.Text.StringBuilder();"),
                    SyntaxFactory.ParseStatement($"logStopwatch.AppendLine(\"[Dentro do método: {entryMethodName}]\");")
                };

        foreach (var l in _log)
        {
          var bars = string.Concat(Enumerable.Repeat("| ", l.Depth));
          if (l.IsLoop)
          {
            stmts.Add(SyntaxFactory.ParseStatement(
                $"logStopwatch.AppendLine($\"{bars}[Loop: {l.StopwatchRef}] Tempo: {{{l.StopwatchRef}.ElapsedMilliseconds}} ms - {{{l.StopwatchRef}.Elapsed}}\");"));
          }
          else
          {
            // Cabeçalho "Dentro do método" para cada método logo após sua primeira aparição
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
        public int Depth { get; private set; }
        public string MethodName { get; private set; } = "";
        public string StopwatchRef { get; private set; } = "";
        public bool IsLoop { get; private set; }

        public static LogLine Method(int depth, string method, string swRef)
            => new() { Depth = depth, MethodName = method, StopwatchRef = swRef, IsLoop = false };

        public static LogLine Loop(int depth, string swRef)
            => new() { Depth = depth, MethodName = "", StopwatchRef = swRef, IsLoop = true };
      }
    }
  }
}
