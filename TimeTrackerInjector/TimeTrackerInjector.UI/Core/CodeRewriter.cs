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
// ReSharper disable ConstantConditionalAccessQualifier

namespace TimeTrackerInjector.UI.Core
{
  /// <summary>
  /// Reescreve o código C# injetando Stopwatches e um bloco único de logs hierárquicos
  /// ao final do primeiro método público (entry). Também envolve chamadas de método com Start/Stop
  /// e envolve loops (for/foreach/while) com Start antes e Stop depois, acumulando o tempo total.
  /// </summary>
  public class CodeRewriter
  {
    private readonly TimeTrackerConfig _config;

    public CodeRewriter(TimeTrackerConfig config)
    {
      _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Aplica a instrumentação nos arquivos dos métodos analisados.
    /// O parâmetro entryMethodName define qual é o primeiro método público (onde o log será inserido).
    /// </summary>
    public async Task RewriteAsync(IEnumerable<AnalyzedMethod> methods, string entryMethodName)
    {
      if (methods == null || !methods.Any())
        return;

      var byFile = methods
          .Where(m => !string.IsNullOrWhiteSpace(m.FilePath))
          .GroupBy(m => m.FilePath);

      foreach (var group in byFile)
      {
        var filePath = group.Key;
        if (!File.Exists(filePath))
          continue;

        var code = await File.ReadAllTextAsync(filePath);
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = await tree.GetRootAsync() as CompilationUnitSyntax;
        if (root is null) continue;

        var rewriter = new StopwatchInjectorRewriter(group.ToList(), entryMethodName);
        var newRoot = (CompilationUnitSyntax)rewriter.Visit(root);

        if (newRoot != null)
        {
          var newCode = newRoot.NormalizeWhitespace().ToFullString();

          // Backup seguro
          var backupPath = filePath + ".bak";
          if (!_config.OverwriteOriginal && !File.Exists(backupPath))
            File.Copy(filePath, backupPath);

          await File.WriteAllTextAsync(filePath, newCode, Encoding.UTF8);
        }
      }
    }

    /// <summary>
    /// Rewriter que injeta campos de Stopwatch, Start/Stop em chamadas/loops,
    /// e o bloco de logStopwatch ao final do método público de entrada.
    /// </summary>
    private sealed class StopwatchInjectorRewriter : CSharpSyntaxRewriter
    {
      private readonly List<AnalyzedMethod> _targetMethods;
      private readonly string _entryMethodName;

      // Mapa: MethodName -> field stopwatch name
      private readonly Dictionary<string, string> _methodStopwatchByName = new(StringComparer.Ordinal);
      // Lista de fields extras criados para loops (um por laço encontrado)
      private readonly List<string> _loopStopwatchFields = new();
      private int _swCounter = 1;
      private int _loopCounter = 1;

      // Contexto para geração do log hierárquico no método raiz
      private string? _currentMethod;             // nome do método que está sendo visitado
      private bool _isInEntryPublicMethod;        // true enquanto visitamos o método público de entrada
      private int _depth;                         // profundidade para barras " | "
      private readonly List<LogEntry> _logEntries = new();      // entradas coletadas no entry method
      private readonly HashSet<string> _loggedInsideHeader = new(StringComparer.Ordinal); // para evitar "Dentro do método" duplicado

      public StopwatchInjectorRewriter(List<AnalyzedMethod> methods, string entryMethodName)
      {
        _targetMethods = methods;
        _entryMethodName = entryMethodName ?? string.Empty;
      }

      public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
      {
        // 1) Descobrir todos os métodos alvo desta classe
        var classMethodNames = new HashSet<string>(
            _targetMethods
                .Where(m => string.Equals(m.ClassName, node.Identifier.Text, StringComparison.Ordinal))
                .Select(m => m.MethodName),
            StringComparer.Ordinal);

        // 2) Criar fields (Stopwatch) para cada método alvo da classe (tipo explícito válido em C#)
        //    Ex.: private readonly System.Diagnostics.Stopwatch sw1_Metodo = new System.Diagnostics.Stopwatch();
        var newMembers = new List<MemberDeclarationSyntax>();

        foreach (var mName in classMethodNames)
        {
          var fieldName = CreateMethodStopwatchName(mName);
          _methodStopwatchByName[mName] = fieldName;

          var fieldDecl = SyntaxFactory.FieldDeclaration(
                  SyntaxFactory.VariableDeclaration(
                      SyntaxFactory.ParseTypeName("System.Diagnostics.Stopwatch"),
                      SyntaxFactory.SeparatedList(new[]
                      {
                                    SyntaxFactory.VariableDeclarator(
                                        SyntaxFactory.Identifier(fieldName),
                                        null,
                                        SyntaxFactory.EqualsValueClause(
                                            SyntaxFactory.ObjectCreationExpression(
                                                    SyntaxFactory.ParseTypeName("System.Diagnostics.Stopwatch"))
                                                .WithArgumentList(SyntaxFactory.ArgumentList())
                                        ))
                      })
                  ))
              .WithModifiers(SyntaxFactory.TokenList(
                  SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
                  SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)));

          newMembers.Add(fieldDecl);
        }

        // 3) Visitar os membros (métodos, etc.)
        var visitedMembers = node.Members.Select(m => (MemberDeclarationSyntax)Visit(m) ?? m).ToList();

        // 4) Acrescentar os novos fields no topo da classe
        var finalMembers = new List<MemberDeclarationSyntax>(newMembers.Count + visitedMembers.Count);
        finalMembers.AddRange(newMembers);
        finalMembers.AddRange(visitedMembers);

        return node.WithMembers(SyntaxFactory.List(finalMembers));
      }

      public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
      {
        var methodName = node.Identifier.Text;
        _currentMethod = methodName;

        var isPublic = node.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
        _isInEntryPublicMethod = isPublic && string.Equals(methodName, _entryMethodName, StringComparison.Ordinal);

        // Visita o corpo do método com controle de profundidade para loops
        var newBody = (BlockSyntax?)Visit(node.Body);
        if (newBody == null)
          return node;

        // 5) Se este é o entry public method → inserir o bloco de logs no final do método
        if (_isInEntryPublicMethod)
        {
          var logStatements = BuildLogStatementsForEntryMethod(methodName);
          var augmentedBody = newBody.AddStatements(logStatements.ToArray());
          _isInEntryPublicMethod = false;  // encerra escopo
          _loggedInsideHeader.Clear();
          _logEntries.Clear();
          _depth = 0;
          return node.WithBody(augmentedBody);
        }

        return node.WithBody(newBody);
      }

      // ------------------------ INVOCATIONS ------------------------

      public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
      {
        // Obter nome da chamada (identifier ou MemberAccess final)
        var calledName = ExtractInvocationName(node);
        if (string.IsNullOrEmpty(calledName))
          return base.VisitInvocationExpression(node);

        // Apenas instrumenta se esse método está entre os alvos do arquivo/classe
        if (!_methodStopwatchByName.TryGetValue(calledName, out var swName))
          return base.VisitInvocationExpression(node);

        // Nós precisamos transformar a statement que contém a invocação:
        // sw.Start(); <original>; sw.Stop();
        var originalStmt = node.Parent as ExpressionStatementSyntax;
        if (originalStmt == null)
          return base.VisitInvocationExpression(node);

        var startStmt = SyntaxFactory.ParseStatement($"{swName}.Start();");
        var stopStmt = SyntaxFactory.ParseStatement($"{swName}.Stop();");

        // Se estamos no método de entrada, registrar entrada de log hierárquico
        if (_isInEntryPublicMethod)
        {
          _logEntries.Add(LogEntry.Method(calledName, swName, _depth + 1));
          // A linha "[Dentro do método: X]" só é útil uma vez por método no escopo atual
          if (_loggedInsideHeader.Add($"{_depth + 1}|{calledName}"))
          {
            _logEntries.Add(LogEntry.InsideHeader(calledName, _depth + 1));
          }
        }

        // Visitar nós internos primeiro (garantir reescritas internas)
        var visitedOriginalStmt = (ExpressionStatementSyntax?)base.Visit(node)?.Parent as ExpressionStatementSyntax
                                  ?? originalStmt;

        // Cria bloco contendo start, invocação e stop
        var newBlock = SyntaxFactory.Block(startStmt, visitedOriginalStmt, stopStmt);
        return newBlock;
      }

      // ------------------------ LOOPS ------------------------

      public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
      {
        return RewriteLoopWithStartStop(node, node);
      }

      public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
      {
        return RewriteLoopWithStartStop(node, node);
      }

      public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node)
      {
        return RewriteLoopWithStartStop(node, node);
      }

      private SyntaxNode RewriteLoopWithStartStop(SyntaxNode loopNode, StatementSyntax loopStmt)
      {
        // Nome/field do Stopwatch deste loop (global pra classe)
        var loopSwName = CreateLoopStopwatchFieldIfNeeded();

        // Registrar log hierárquico se estamos no método de entrada
        if (_isInEntryPublicMethod)
        {
          _logEntries.Add(LogEntry.Loop(loopSwName, _depth + 1));
        }

        // Aumenta profundidade para o conteúdo do loop
        _depth++;
        var visitedLoop = (StatementSyntax?)base.Visit(loopStmt) ?? loopStmt;
        _depth--;

        // Criar statements start/stop do loop abrangendo o laço inteiro
        var startStmt = SyntaxFactory.ParseStatement($"{loopSwName}.Start();");
        var stopStmt = SyntaxFactory.ParseStatement($"{loopSwName}.Stop();");

        // Retorna bloco: swLoop.Start(); <loop>; swLoop.Stop();
        return SyntaxFactory.Block(startStmt, visitedLoop, stopStmt);
      }

      // ------------------------ HELPERS ------------------------

      private string ExtractInvocationName(InvocationExpressionSyntax node)
      {
        // Pode ser simples "Foo()" (IdentifierName) ou "obj.Foo()" (MemberAccessExpression)
        switch (node.Expression)
        {
          case IdentifierNameSyntax id:
            return id.Identifier.Text;
          case MemberAccessExpressionSyntax maes:
            return maes.Name.Identifier.Text;
          default:
            return string.Empty;
        }
      }

      private string CreateMethodStopwatchName(string methodName)
      {
        var clean = new string(methodName.Where(char.IsLetterOrDigit).ToArray());
        return $"sw{_swCounter++}_{clean}";
      }

      private string CreateLoopStopwatchFieldIfNeeded()
      {
        var name = $"swLoop_{_loopCounter++}";
        if (_loopStopwatchFields.Contains(name))
          return name;

        _loopStopwatchFields.Add(name);
        return name;
      }

      private IEnumerable<StatementSyntax> BuildLogStatementsForEntryMethod(string entryMethod)
      {
        var stmts = new List<StatementSyntax>
                {
                    // var logStopwatch = new StringBuilder();
                    SyntaxFactory.ParseStatement("var logStopwatch = new System.Text.StringBuilder();")
                };

        // Cabeçalho do método raiz
        stmts.Add(SyntaxFactory.ParseStatement(
            $"logStopwatch.AppendLine(\"[Dentro do método: {entryMethod}]\");"));

        foreach (var e in _logEntries)
        {
          var bars = string.Concat(Enumerable.Repeat("| ", e.Depth));
          switch (e.Kind)
          {
            case LogKind.Method:
              stmts.Add(SyntaxFactory.ParseStatement(
                  $"logStopwatch.AppendLine($\"{bars}[Metodo: {e.MethodName}] Tempo: {{{e.StopwatchName}.ElapsedMilliseconds}} ms - {{{e.StopwatchName}.Elapsed}}\");"));
              break;

            case LogKind.Loop:
              stmts.Add(SyntaxFactory.ParseStatement(
                  $"logStopwatch.AppendLine($\"{bars}[Loop: {e.StopwatchName}] Tempo: {{{e.StopwatchName}.ElapsedMilliseconds}} ms - {{{e.StopwatchName}.Elapsed}}\");"));
              break;

            case LogKind.InsideHeader:
              stmts.Add(SyntaxFactory.ParseStatement(
                  $"logStopwatch.AppendLine(\"{bars}[Dentro do método: {e.MethodName}]\");"));
              break;
          }
        }

        return stmts;
      }

      // Representa uma linha de log a ser gerada no método de entrada
      private sealed class LogEntry
      {
        public int Depth { get; init; }
        public string StopwatchName { get; init; } = string.Empty;
        public string MethodName { get; init; } = string.Empty;
        public LogKind Kind { get; init; }

        public static LogEntry Method(string methodName, string swName, int depth)
            => new() { Depth = depth, MethodName = methodName, StopwatchName = swName, Kind = LogKind.Method };

        public static LogEntry Loop(string swName, int depth)
            => new() { Depth = depth, StopwatchName = swName, Kind = LogKind.Loop };

        public static LogEntry InsideHeader(string methodName, int depth)
            => new() { Depth = depth, MethodName = methodName, Kind = LogKind.InsideHeader };
      }

      private enum LogKind { Method, Loop, InsideHeader }
    }
  }
}
