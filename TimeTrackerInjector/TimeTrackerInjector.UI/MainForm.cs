using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TimeTrackerInjector.UI.Config;
using TimeTrackerInjector.UI.Core;

namespace TimeTrackerInjector.UI
{
  public partial class MainForm : Form
  {
    private readonly ConfigurationManager _configManager;
    private List<AnalyzedMethod> _methods = new();

    public MainForm()
    {
      InitializeComponent();
      _configManager = new ConfigurationManager();
      _configManager.Load();
    }

    //abre o form de configuração
    private void configuraçãoToolStripMenuItem_Click(object sender, EventArgs e)
    {
      using var form = new ConfigForm(_configManager);
      form.ShowDialog();
    }

    //executa análise da solution
    private async void btnExecutar_Click(object sender, EventArgs e)
    {
      try
      {
        AppendLog("Iniciando análise da solution...");

        var cfg = _configManager.Current;
        if (string.IsNullOrWhiteSpace(cfg.SolutionFile))
        {
          AppendLog("Nenhuma solution configurada.");
          return;
        }

        var analyzer = new SolutionAnalyzer(cfg);
        var result = await analyzer.AnalyzeAsync();

        if (result.Methods.Count == 0)
        {
          AppendLog("Nenhum método encontrado.");
          return;
        }

        _methods = result.Methods;

        gridArquivos.Rows.Clear();
        foreach (var method in _methods.OrderBy(m => m.FilePath))
        {
          gridArquivos.Rows.Add(method.FilePath, method.MethodName, "Aguardando");
        }

        AppendLog($"Análise concluída. {result.Methods.Count} métodos encontrados.");
        AppendLog("Você pode revisar os arquivos e clicar em 'Confirmar Alterações'.");
        btnConfirmarAlteracoes.Enabled = true;
      }
      catch (Exception ex)
      {
        AppendLog($"Erro: {ex.Message}");
      }
    }

    // confirma e aplica instrumentação
    private async void btnConfirmar_Click(object sender, EventArgs e)
    {
      try
      {
        if (_methods == null || _methods.Count == 0)
        {
          AppendLog("Nenhum método analisado para instrumentar.");
          return;
        }

        var cfg = _configManager.Current;
        AppendLog("Iniciando instrumentação profunda...");

        // 1?Analisar novamente para obter Compilation e EntrySymbol
        var analyzer = new SolutionAnalyzer(cfg);
        var result = await analyzer.AnalyzeAsync();

        var compilation = result.Compilation;
        var entryMethod = result.EntryMethod;
        if (compilation == null || entryMethod == null)
        {
          AppendLog("Não foi possível localizar o método de entrada configurado.");
          return;
        }

        // 2?Construir a árvore de chamadas
        var graphBuilder = new CallGraphBuilder(compilation);
        var rootNode = await graphBuilder.BuildTreeAsync(entryMethod);
        AppendLog("Árvore de chamadas construída com sucesso.");

        // 3?Executar o CodeRewriter com hierarquia profunda
        var rewriter = new CodeRewriter(cfg);
        await rewriter.RewriteAsync(compilation, rootNode, result.Methods, entryMethod);

        // 4?Atualizar a grid
        foreach (DataGridViewRow row in gridArquivos.Rows)
          row.Cells[2].Value = "Modificado";

        AppendLog("Instrumentação concluída com sucesso!");
        tabControlMain.SelectedTab = tabLog;
      }
      catch (Exception ex)
      {
        AppendLog($"Erro durante instrumentação: {ex.Message}");
      }
    }

    //loga mensagens na aba de logs
    private void AppendLog(string message)
    {
      if (InvokeRequired)
      {
        Invoke(new Action(() => AppendLog(message)));
        return;
      }

      txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }
  }
}
