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
  /// Responsável por modificar o código-fonte C# para injetar:
  ///  - Stopwatches em torno de chamadas de método relevantes
  ///  - Contadores de tempo para loops (for, foreach, while)
  ///  - Bloco de logStopwatch no final do método público principal
  /// 
  /// Ele respeita:
  ///  - A classe base e método principal configurados (ex: ProcessService.ProcessAll)
  ///  - Mantém a estrutura original dos loops
  ///  - Ignora métodos externos, como System.* e LogService.*
  /// </summary>
  public class CodeRewriter
  {
    private readonly TimeTrackerConfig _config;

    public CodeRewriter(TimeTrackerConfig config)
    {
      _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Método principal que recebe o Compilation, a árvore de chamadas e a lista de métodos analisados.
    /// Reescreve os arquivos injetando os blocos de Stopwatch e log.
    /// </summary>
    public async Task RewriteAsync(
        Compilation compilation,
        CallGraphNode callTreeRoot,
        IReadOnlyList<AnalyzedMethod> methods,
        IMethodSymbol entryMethod)
    {
      if (methods == null || methods.Count == 0)
        return;

      // Cria um registro de Stopwatches únicos para cada método do grafo
      var stopwatchByMethod = BuildStopwatchRegistry(callTreeRoot);

      foreach (var group in methods
                   .Where(m => !string.IsNullOrWhiteSpace(m.FilePath))
                   .GroupBy(m => m.FilePath))
      {
        var filePath = group.Key;
        if (!File.Exists(filePath))
          continue;

        var tree = compilation.SyntaxTrees.FirstOrDefault(t => t.FilePath == filePath);
        if (tree == null)
          continue;

        var model = compilation.GetSemanticModel(tree);
        var root = (await tree.GetRootAsync()) as CompilationUnitSyntax;
        if (root == null)
          continue;

        // Instancia o rewriter com o contexto atual
        var rewriter = new DeepHierarchyRewriter(model, stopwatchByMethod, entryMethod);
        var newRoot = (CompilationUnitSyntax)rewriter.Visit(root);

        // Gera código final formatado
        var newCode = newRoot.NormalizeWhitespace().ToFullString();

        // Cria backup antes de sobrescrever
        /*var backupPath = filePath + ".bak";
        if (!_config.OverwriteOriginal && !File.Exists(backupPath))
          File.Copy(filePath, backupPath);*/

        await File.WriteAllTextAsync(filePath, newCode, Encoding.UTF8);
      }
    }

    /// <summary>
    /// Cria um dicionário com todos os métodos do grafo de chamadas e seus nomes de Stopwatch.
    /// </summary>
    private static Dictionary<IMethodSymbol, StopwatchInfo> BuildStopwatchRegistry(CallGraphNode root)
    {
      var dict = new Dictionary<IMethodSymbol, StopwatchInfo>(SymbolEqualityComparer.Default);
      int counter = 1;

      void Walk(CallGraphNode n)
      {
        var sym = n.Symbol;
        if (!dict.ContainsKey(sym))
        {
          var className = sym.ContainingType?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                           ?? "UnknownClass";
          var cleanMethod = new string(sym.Name.Where(char.IsLetterOrDigit).ToArray());
          dict[sym] = new StopwatchInfo
          {
            DeclaringClassName = className,
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
    /// Classe interna responsável por visitar e modificar a árvore sintática do Roslyn.
    /// </summary>
    private sealed class DeepHierarchyRewriter : CSharpSyntaxRewriter
    {
      private readonly SemanticModel _semantic;
      private readonly Dictionary<IMethodSymbol, StopwatchInfo> _stopwatches;
      private readonly IMethodSymbol _entryMethod;
      private INamedTypeSymbol? _currentClass;
      private IMethodSymbol? _currentMethod;
      private bool _isInEntryPublic;
      private int _depth;
      private const int MaxDepth = 50;
      private readonly List<LogLine> _log = new();

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
      /// Ao visitar uma classe, cria os Stopwatches somente na classe base configurada.
      /// </summary>
      public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
      {
        _currentClass = _semantic.GetDeclaredSymbol(node);

        // Se não for a classe configurada (ex: ProcessService), pula.
        if (!SymbolEqualityComparer.Default.Equals(_currentClass, _entryMethod.ContainingType))
          return base.VisitClassDeclaration(node);

        var visitedMembers = node.Members.Select(m => (MemberDeclarationSyntax)Visit(m) ?? m).ToList();
        var fieldsToAdd = new List<MemberDeclarationSyntax>();

        // Cria apenas os campos Stopwatch dessa classe
        foreach (var kv in _stopwatches)
        {
          var owner = kv.Key.ContainingType;
          if (owner == null) continue;
          if (!SymbolEqualityComparer.Default.Equals(owner, _currentClass)) continue;

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
        }

        // Adiciona os novos fields no topo da classe
        if (fieldsToAdd.Count == 0)
          return node.WithMembers(SyntaxFactory.List(visitedMembers));

        var finalMembers = new List<MemberDeclarationSyntax>();
        finalMembers.AddRange(fieldsToAdd);
        finalMembers.AddRange(visitedMembers);
        return node.WithMembers(SyntaxFactory.List(finalMembers));
      }

      /// <summary>
      /// Para cada método, detecta se é o método de entrada configurado.
      /// Caso seja, injeta o bloco de logStopwatch ao final.
      /// </summary>
      public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
      {
        _currentMethod = _semantic.GetDeclaredSymbol(node);
        _isInEntryPublic = SymbolEqualityComparer.Default.Equals(_currentMethod, _entryMethod);

        _depth = 0;
        _log.Clear();

        var newBody = (BlockSyntax?)Visit(node.Body);
        if (newBody == null)
          return node;

        // Apenas o método principal recebe o bloco de log ao final
        if (_isInEntryPublic)
        {
          var statements = BuildLogStatements(node.Identifier.Text);
          newBody = newBody.AddStatements(statements.ToArray());
        }

        return node.WithBody(newBody);
      }

      /// <summary>
      /// Visita cada statement de expressão e adiciona Start/Stop apenas em chamadas de método relevantes.
      /// Ignora métodos de LogService e namespaces System/Microsoft.
      /// </summary>
      public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
      {
        if (_depth >= MaxDepth)
          return node;

        if (node.Expression is InvocationExpressionSyntax invocation)
        {
          var targetSymbol = _semantic.GetSymbolInfo(invocation).Symbol as IMethodSymbol;

          if (targetSymbol == null)
            return base.VisitExpressionStatement(node);

          // 🔸 Ignora métodos irrelevantes
          if (targetSymbol.ContainingType == null ||
              targetSymbol.ContainingType.Name.Contains("LogService") ||
              targetSymbol.ContainingNamespace.ToString().StartsWith("System") ||
              targetSymbol.ContainingNamespace.ToString().StartsWith("Microsoft"))
          {
            return base.VisitExpressionStatement(node);
          }

          // 🔸 Apenas métodos do grafo serão instrumentados
          if (_stopwatches.TryGetValue(targetSymbol, out var swInfo))
          {
            var qualified = $"{swInfo.DeclaringClassName}.{swInfo.FieldName}";
            var startStmt = SyntaxFactory.ParseStatement($"{qualified}.Start();");
            var stopStmt = SyntaxFactory.ParseStatement($"{qualified}.Stop();");

            if (_isInEntryPublic)
              _log.Add(LogLine.Method(_depth + 1, targetSymbol.Name, qualified));

            // Retorna um bloco Start/Chamada/Stop sem recursão
            return SyntaxFactory.Block(startStmt, node, stopStmt);
          }
        }

        return base.VisitExpressionStatement(node);
      }

      /// <summary>
      /// Modifica loops sem alterar a estrutura (mantém for, foreach, while),
      /// apenas injeta Start() e Stop() dentro do corpo.
      /// </summary>
      public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
          => RewriteLoop(node, node.Statement);
      public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
          => RewriteLoop(node, node.Statement);
      public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node)
          => RewriteLoop(node, node.Statement);

      private SyntaxNode RewriteLoop(SyntaxNode loopNode, StatementSyntax body)
      {
        if (_depth >= MaxDepth)
          return loopNode;

        string loopSw = $"swLoop_{_depth + 1}_{Guid.NewGuid():N}[..6]";
        _log.Add(LogLine.Loop(_depth + 1, loopSw));

        _depth++;
        var visitedBody = (StatementSyntax?)base.Visit(body) ?? body;
        _depth--;

        var start = SyntaxFactory.ParseStatement($"{loopSw}.Start();");
        var stop = SyntaxFactory.ParseStatement($"{loopSw}.Stop();");

        var block = visitedBody as BlockSyntax ?? SyntaxFactory.Block(visitedBody);
        var newBlock = block.WithStatements(block.Statements.Insert(0, start).Add(stop));

        // Reconstrói o loop original preservando cabeçalho
        return loopNode switch
        {
          ForStatementSyntax f => f.WithStatement(newBlock),
          ForEachStatementSyntax fe => fe.WithStatement(newBlock),
          WhileStatementSyntax w => w.WithStatement(newBlock),
          _ => loopNode
        };
      }

      /// <summary>
      /// Monta as linhas do logStopwatch hierárquico no final do método principal.
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
      /// Representa uma linha de log hierárquico (método ou loop).
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
            => new() { Depth = depth, StopwatchRef = swRef, IsLoop = true };
      }
    }
  }
}
