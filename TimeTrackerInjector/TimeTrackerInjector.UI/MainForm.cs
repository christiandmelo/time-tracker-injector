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

    //abre o form de configura��o
    private void configura��oToolStripMenuItem_Click(object sender, EventArgs e)
    {
      using var form = new ConfigForm(_configManager);
      form.ShowDialog();
    }

    //executa an�lise da solution
    private async void btnExecutar_Click(object sender, EventArgs e)
    {
      try
      {
        AppendLog("Iniciando an�lise da solution...");

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
          AppendLog("Nenhum m�todo encontrado.");
          return;
        }

        _methods = result.Methods;

        gridArquivos.Rows.Clear();
        foreach (var method in _methods.OrderBy(m => m.FilePath))
        {
          gridArquivos.Rows.Add(method.FilePath, method.MethodName, "Aguardando");
        }

        AppendLog($"An�lise conclu�da. {result.Methods.Count} m�todos encontrados.");
        AppendLog("Voc� pode revisar os arquivos e clicar em 'Confirmar Altera��es'.");
        btnConfirmarAlteracoes.Enabled = true;
      }
      catch (Exception ex)
      {
        AppendLog($"Erro: {ex.Message}");
      }
    }

    // confirma e aplica instrumenta��o
    private async void btnConfirmar_Click(object sender, EventArgs e)
    {
      try
      {
        if (_methods == null || _methods.Count == 0)
        {
          AppendLog("Nenhum m�todo analisado para instrumentar.");
          return;
        }

        var cfg = _configManager.Current;
        AppendLog("Iniciando instrumenta��o profunda...");

        // 1?Analisar novamente para obter Compilation e EntrySymbol
        var analyzer = new SolutionAnalyzer(cfg);
        var result = await analyzer.AnalyzeAsync();

        var compilation = result.Compilation;
        var entryMethod = result.EntryMethod;
        if (compilation == null || entryMethod == null)
        {
          AppendLog("N�o foi poss�vel localizar o m�todo de entrada configurado.");
          return;
        }

        // 2?Construir a �rvore de chamadas
        var graphBuilder = new CallGraphBuilder(compilation);
        var rootNode = await graphBuilder.BuildTreeAsync(entryMethod);
        AppendLog("�rvore de chamadas constru�da com sucesso.");

        // 3?Executar o CodeRewriter com hierarquia profunda
        var rewriter = new CodeRewriter(cfg);
        await rewriter.RewriteAsync(compilation, rootNode, result.Methods, entryMethod);

        // 4?Atualizar a grid
        foreach (DataGridViewRow row in gridArquivos.Rows)
          row.Cells[2].Value = "Modificado";

        AppendLog("Instrumenta��o conclu�da com sucesso!");
        tabControlMain.SelectedTab = tabLog;
      }
      catch (Exception ex)
      {
        AppendLog($"Erro durante instrumenta��o: {ex.Message}");
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
