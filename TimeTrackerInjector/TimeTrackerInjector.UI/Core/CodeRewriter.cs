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
  /// CODE REWRITER (Time Tracker Injector)
  ///
  /// O que este rewriter faz:
  /// - Cria campos de Stopwatch no TOPO da classe base (a classe que contém o método público de entrada).
  ///   * Para CADA MÉTODO do grafo de chamadas, mesmo que o método esteja em OUTRA classe,
  ///     o campo de Stopwatch é criado NA CLASSE BASE (ex.: ProcessService.sw1_CalculateValue).
  /// - Envolve chamadas de método relevantes com Start()/Stop() usando os campos acima.
  /// - Mantém ESTRUTURA dos loops (for/foreach/while) e injeta Start()/Stop() DENTRO do corpo.
  ///   * Os stopwatches de loop são CAMPOS DE CLASSE (swLoop1, swLoop2, ...), criados no topo.
  /// - Apenas o MÉTODO PÚBLICO DE ENTRADA recebe o bloco final "logStopwatch" (hierárquico).
  /// - NÃO mexe em Program.cs, LogService.cs, etc. Apenas na CLASSE BASE.
  ///
  /// Observações:
  /// - NÃO cria backup do arquivo original.
  /// - Ignora métodos de System.*, Microsoft.* e classes tipo LogService.
  /// </summary>
  public class CodeRewriter
  {
    private readonly TimeTrackerConfig _config;

    public CodeRewriter(TimeTrackerConfig config)
    {
      _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Reescreve os arquivos da solution, injetando stopwatchs e o bloco de log no método público de entrada.
    /// </summary>
    public async Task RewriteAsync(
        Compilation compilation,
        CallGraphNode callTreeRoot,
        IReadOnlyList<AnalyzedMethod> methods,
        IMethodSymbol entryMethod)
    {
      if (methods == null || methods.Count == 0)
        return;

      // Nome totalmente qualificado da CLASSE BASE (onde está o método de entrada)
      var entryClass = entryMethod.ContainingType;
      var entryClassName = entryClass?.Name ?? string.Empty;

      // 🔒 Processaremos APENAS os arquivos onde a CLASSE BASE aparece (evita tocar em Program, LogService, etc.)
      var filesForEntryClass = methods
          .Where(m => string.Equals(m.ClassName, entryClassName, StringComparison.Ordinal))
          .Select(m => m.FilePath)
          .Where(p => !string.IsNullOrWhiteSpace(p))
          .Distinct()
          .ToHashSet(StringComparer.Ordinal);

      if (filesForEntryClass.Count == 0)
        return;

      // 🗺️ Mapeia TODOS os métodos do grafo → cada método ganha um Stopwatch,
      // mas o campo será declarado SEMPRE na classe base (não na classe de origem do método).
      var stopwatchByMethod = BuildStopwatchRegistry(callTreeRoot, entryClass);

      // Percorre SOMENTE os arquivos que pertencem à classe base
      foreach (var filePath in filesForEntryClass)
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

        var rewriter = new DeepHierarchyRewriter(model, stopwatchByMethod, entryMethod);
        var newRoot = (CompilationUnitSyntax)rewriter.Visit(root);

        // ⛔ Sem backup: grava direto
        var newCode = newRoot.NormalizeWhitespace().ToFullString();
        await File.WriteAllTextAsync(filePath, newCode, Encoding.UTF8);
      }
    }

    /// <summary>
    /// Cria um dicionário de Stopwatches onde:
    ///  - A chave é o IMethodSymbol (do grafo)
    ///  - O valor contém o NOME DO CAMPO e o NOME DA CLASSE DECLARADORA (SEMPRE a classe base)
    /// </summary>
    private static Dictionary<IMethodSymbol, StopwatchInfo> BuildStopwatchRegistry(CallGraphNode root, INamedTypeSymbol? entryClass)
    {
      var dict = new Dictionary<IMethodSymbol, StopwatchInfo>(SymbolEqualityComparer.Default);
      int counter = 1;

      var entryClassName = entryClass?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                           ?? "EntryClass";

      void Walk(CallGraphNode n)
      {
        var sym = n.Symbol;
        if (!dict.ContainsKey(sym))
        {
          var cleanMethod = new string(sym.Name.Where(char.IsLetterOrDigit).ToArray());
          dict[sym] = new StopwatchInfo
          {
            // ⚠️ Sempre a classe base — mesmo que o método esteja em outra classe
            DeclaringClassName = entryClassName,
            FieldName = $"sw{counter++}_{cleanMethod}"
          };
        }

        foreach (var c in n.Children)
          Walk(c);
      }

      Walk(root);
      return dict;
    }

    /// <summary>
    /// Visitor que insere os campos Stopwatches, Start/Stop em chamadas e o bloco de log no método público de entrada.
    /// Só mexe na CLASSE BASE.
    /// </summary>
    private sealed class DeepHierarchyRewriter : CSharpSyntaxRewriter
    {
      private readonly SemanticModel _semantic;
      private readonly Dictionary<IMethodSymbol, StopwatchInfo> _stopwatches;
      private readonly IMethodSymbol _entryMethod;

      // Contexto corrente da visita
      private INamedTypeSymbol? _currentClass;
      private IMethodSymbol? _currentMethod;
      private bool _isInEntryPublic;

      // Controle para hierarquia e segurança
      private int _depth;
      private const int MaxDepth = 50;

      // Coleta para LOG hierárquico (apenas no método de entrada)
      private readonly List<LogLine> _log = new();

      // Campos de loop a adicionar na classe base (swLoop1, swLoop2, ...)
      private readonly List<string> _loopFieldsToAdd = new();
      private int _loopFieldCounter = 0;

      public DeepHierarchyRewriter(
          SemanticModel semantic,
          Dictionary<IMethodSymbol, StopwatchInfo> stopwatches,
          IMethodSymbol entryMethod)
      {
        _semantic = semantic;
        _stopwatches = stopwatches;
        _entryMethod = entryMethod;
      }

      /// <summary>
      /// Helper: estamos visitando a CLASSE BASE?
      /// (somente ela será alterada)
      /// </summary>
      private bool IsInEntryClass =>
          _currentClass != null &&
          SymbolEqualityComparer.Default.Equals(_currentClass, _entryMethod.ContainingType);

      /// <summary>
      /// Visita a classe. Se for a classe base, injeta:
      ///  - campos de Stopwatch de MÉTODO (um por método do grafo)
      ///  - campos de Stopwatch de LOOP (descobertos durante a visita dos métodos)
      /// </summary>
      public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
      {
        _currentClass = _semantic.GetDeclaredSymbol(node);

        // Se NÃO é a classe base, não toca
        if (!IsInEntryClass)
          return base.VisitClassDeclaration(node);

        // 1) Visitar membros primeiro (para descobrir loops e capturar _loopFieldsToAdd)
        var visitedMembers = node.Members.Select(m => (MemberDeclarationSyntax)Visit(m) ?? m).ToList();

        // 2) Criar campos Stopwatch de MÉTODO (todos pertencentes à classe base)
        var methodFields = new List<MemberDeclarationSyntax>();
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

          methodFields.Add(fieldDecl);
        }

        // 3) Criar campos Stopwatch de LOOP (swLoop1, swLoop2, ...)
        var loopFields = new List<MemberDeclarationSyntax>();
        foreach (var loopName in _loopFieldsToAdd.Distinct())
        {
          var fieldDecl =
              SyntaxFactory.FieldDeclaration(
                  SyntaxFactory.VariableDeclaration(
                      SyntaxFactory.ParseTypeName("System.Diagnostics.Stopwatch"),
                      SyntaxFactory.SeparatedList(new[]
                      {
                                    SyntaxFactory.VariableDeclarator(
                                        SyntaxFactory.Identifier(loopName),
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

          loopFields.Add(fieldDecl);
        }

        // 4) Devolve a classe com os novos fields (métodos + loops) no topo
        var finalMembers = new List<MemberDeclarationSyntax>(methodFields.Count + loopFields.Count + visitedMembers.Count);
        finalMembers.AddRange(methodFields);
        finalMembers.AddRange(loopFields);
        finalMembers.AddRange(visitedMembers);

        return node.WithMembers(SyntaxFactory.List(finalMembers));
      }

      /// <summary>
      /// Visita cada método. Apenas o método de entrada (público configurado) recebe o bloco final de log.
      /// </summary>
      public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
      {
        _currentMethod = _semantic.GetDeclaredSymbol(node);

        // Se não estamos na classe base, não fazemos nada neste método
        if (!IsInEntryClass)
          return base.VisitMethodDeclaration(node);

        // É o método público de entrada?
        _isInEntryPublic = SymbolEqualityComparer.Default.Equals(_currentMethod, _entryMethod);

        _depth = 0;
        _log.Clear();

        var newBody = (BlockSyntax?)Visit(node.Body);
        if (newBody == null)
          return node;

        // Apenas no método de entrada geramos o bloco do log hierárquico
        if (_isInEntryPublic)
        {
          var statements = BuildLogStatements(node.Identifier.Text);
          newBody = newBody.AddStatements(statements.ToArray());
        }

        return node.WithBody(newBody);
      }

      /// <summary>
      /// Visita chamadas de método (ExpressionStatement) e injeta Start/Stop
      /// utilizando SEMPRE os campos da classe base.
      /// Ignora chamadas de System.*, Microsoft.* e tipos "LogService".
      /// </summary>
      public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
      {
        // Fora da classe base → ignora
        if (!IsInEntryClass)
          return base.VisitExpressionStatement(node);

        if (_depth >= MaxDepth)
          return node;

        if (node.Expression is InvocationExpressionSyntax invocation)
        {
          var info = _semantic.GetSymbolInfo(invocation);
          var targetSymbol = info.Symbol as IMethodSymbol
                          ?? info.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();

          if (targetSymbol == null)
            return base.VisitExpressionStatement(node);

          // 🔸 Filtra irrelevantes (System.*, Microsoft.*, LogService, etc.)
          var ns = targetSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
          var typeName = targetSymbol.ContainingType?.Name ?? string.Empty;

          if (ns.StartsWith("System", StringComparison.Ordinal) ||
              ns.StartsWith("Microsoft", StringComparison.Ordinal) ||
              typeName.Contains("LogService", StringComparison.Ordinal))
          {
            return base.VisitExpressionStatement(node);
          }

          // 🔸 Só instrumenta métodos que estão no mapeamento (grafo)
          if (_stopwatches.TryGetValue(targetSymbol.OriginalDefinition, out var swInfo) ||
              _stopwatches.TryGetValue(targetSymbol, out swInfo))
          {
            // Sempre qualificado pela CLASSE BASE
            var qualified = $"{_entryMethod.ContainingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}.{swInfo.FieldName}";
            var startStmt = SyntaxFactory.ParseStatement($"{qualified}.Start();");
            var stopStmt = SyntaxFactory.ParseStatement($"{qualified}.Stop();");

            if (_isInEntryPublic)
              _log.Add(LogLine.Method(_depth + 1, targetSymbol.Name, qualified));

            // Evita reentrância: substitui direto por bloco Start/Chamada/Stop
            return SyntaxFactory.Block(startStmt, node, stopStmt);
          }
        }

        return base.VisitExpressionStatement(node);
      }

      /// <summary>
      /// Preserva a estrutura dos loops e injeta Start()/Stop() no corpo.
      /// Cria campos de loop no topo da CLASSE BASE (swLoop1, swLoop2, ...).
      /// </summary>
      public override SyntaxNode? VisitForStatement(ForStatementSyntax node) => RewriteLoop(node, node.Statement);
      public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node) => RewriteLoop(node, node.Statement);
      public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node) => RewriteLoop(node, node.Statement);

      private SyntaxNode RewriteLoop(SyntaxNode loopNode, StatementSyntax body)
      {
        // Fora da classe base → não toca
        if (!IsInEntryClass)
          return loopNode;

        if (_depth >= MaxDepth)
          return loopNode;

        // Gera um nome estável e simples: swLoop1, swLoop2, ...
        _loopFieldCounter++;
        var loopSw = $"swLoop{_loopFieldCounter}";

        // Marca para criar o CAMPO no topo da classe base
        _loopFieldsToAdd.Add(loopSw);

        // Para LOG do método de entrada
        if (_isInEntryPublic)
          _log.Add(LogLine.Loop(_depth + 1, loopSw));

        // Visita o corpo original, preservando recursões internas
        _depth++;
        var visitedBody = (StatementSyntax?)base.Visit(body) ?? body;
        _depth--;

        // Injeta Start/Stop DENTRO do corpo do loop (preserva "for(...)" original)
        var start = SyntaxFactory.ParseStatement($"{loopSw}.Start();");
        var stop = SyntaxFactory.ParseStatement($"{loopSw}.Stop();");

        // Garante que o corpo seja um bloco e adiciona Start no início e Stop no final
        var block = visitedBody as BlockSyntax ?? SyntaxFactory.Block(visitedBody);
        var newBlock = block.WithStatements(block.Statements.Insert(0, start).Add(stop));

        // Reconstrói o loop com o novo corpo
        return loopNode switch
        {
          ForStatementSyntax f => f.WithStatement(newBlock),
          ForEachStatementSyntax fe => fe.WithStatement(newBlock),
          WhileStatementSyntax w => w.WithStatement(newBlock),
          _ => loopNode
        };
      }

      /// <summary>
      /// Constrói o bloco final "logStopwatch" com hierarquia de métodos e loops.
      /// É adicionado SOMENTE ao final do MÉTODO DE ENTRADA.
      /// </summary>
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
            stmts.Add(SyntaxFactory.ParseStatement(
                $"logStopwatch.AppendLine($\"{bars}[Metodo: {l.MethodName}] Tempo: {{{l.StopwatchRef}.ElapsedMilliseconds}} ms - {{{l.StopwatchRef}.Elapsed}}\");"));
            stmts.Add(SyntaxFactory.ParseStatement(
                $"logStopwatch.AppendLine($\"{bars}[Dentro do método: {l.MethodName}]\");"));
          }
        }

        return stmts;
      }

      /// <summary>
      /// Representa uma linha do log hierárquico (método ou loop).
      /// </summary>
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
