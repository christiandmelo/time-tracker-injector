using System;
using System.Windows.Forms;
using System.Threading.Tasks;
using TimeTrackerInjector.UI.Core;
using TimeTrackerInjector.UI.Config;

namespace TimeTrackerInjector.UI
{
  public partial class MainForm : Form
  {
    private readonly ConfigurationManager _configManager;

    public MainForm()
    {
      InitializeComponent();
      _configManager = new ConfigurationManager();
    }

    private void configura��oToolStripMenuItem_Click(object sender, EventArgs e)
    {
      using var configForm = new ConfigForm(_configManager);
      if (configForm.ShowDialog(this) == DialogResult.OK)
      {
        _configManager.Load();
        AppendLog("Configura��o atualizada com sucesso.");
      }
    }

    private async void btnExecutar_Click(object sender, EventArgs e)
    {
      try
      {
        btnExecutar.Enabled = false;
        btnConfirmarAlteracoes.Enabled = false;

        AppendLog("Iniciando an�lise da solution...");
        var analyzer = new SolutionAnalyzer(_configManager.Current);
        var methods = await analyzer.AnalyzeAsync();

        gridArquivos.Rows.Clear();
        foreach (var m in methods)
        {
          gridArquivos.Rows.Add(m.ProjectName, m.ClassName, m.MethodName, m.FilePath);
          AppendLog($"M�todo encontrado: {m.ClassName}.{m.MethodName}");
        }

        AppendLog($"An�lise conclu�da. {methods.Count} m�todo(s) localizado(s).");
        tabControlMain.SelectedTab = tabArquivos;
        btnConfirmarAlteracoes.Enabled = methods.Count > 0;
      }
      catch (Exception ex)
      {
        AppendLog($"[ERRO] {ex.Message}");
        MessageBox.Show($"Erro durante a an�lise:\n{ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
      finally
      {
        btnExecutar.Enabled = true;
      }
    }

    private void btnConfirmarAlteracoes_Click(object sender, EventArgs e)
    {
      if (gridArquivos.Rows.Count == 0)
      {
        MessageBox.Show("Nenhum arquivo foi identificado para modifica��o.",
            "Time Tracker Injector", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return;
      }

      var confirm = MessageBox.Show(
          "Deseja realmente aplicar as altera��es nos arquivos listados?",
          "Confirmar Altera��es",
          MessageBoxButtons.YesNo,
          MessageBoxIcon.Question
      );

      if (confirm != DialogResult.Yes)
        return;

      btnConfirmarAlteracoes.Enabled = false;
      AppendLog("Iniciando inje��o de Stopwatchs e logs nos m�todos selecionados...");

      // Simula��o - futuramente aqui chamaremos o CodeRewriter
      Task.Run(async () =>
      {
        await Task.Delay(1500); // simula processamento
        AppendLog("Instrumenta��o conclu�da com sucesso (simula��o).");
        AppendLog($"Arquivos atualizados: {gridArquivos.Rows.Count}");
        AppendLog("-------------------------------------------");
        Invoke(() => btnConfirmarAlteracoes.Enabled = true);
        Invoke(() => tabControlMain.SelectedTab = tabLog);
      });
    }

    private void AppendLog(string message)
    {
      if (txtLog.InvokeRequired)
      {
        txtLog.Invoke(new Action(() => AppendLog(message)));
        return;
      }

      txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }
  }
}
