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
    private IReadOnlyList<AnalyzedMethod>? methods;

    public MainForm()
    {
      InitializeComponent();
      _configManager = new ConfigurationManager();
    }

    private void configuraçãoToolStripMenuItem_Click(object sender, EventArgs e)
    {
      using var configForm = new ConfigForm(_configManager);
      if (configForm.ShowDialog(this) == DialogResult.OK)
      {
        _configManager.Load();
        AppendLog("Configuração atualizada com sucesso.");
      }
    }

    private async void btnExecutar_Click(object sender, EventArgs e)
    {
      try
      {
        btnExecutar.Enabled = false;
        btnConfirmarAlteracoes.Enabled = false;

        AppendLog("Iniciando análise da solution...");
        var analyzer = new SolutionAnalyzer(_configManager.Current);
        methods = await analyzer.AnalyzeAsync();

        gridArquivos.Rows.Clear();
        foreach (var m in methods)
        {
          gridArquivos.Rows.Add(m.ProjectName, m.ClassName, m.MethodName, m.FilePath);
          AppendLog($"Método encontrado: {m.ClassName}.{m.MethodName}");
        }

        AppendLog($"Análise concluída. {methods.Count} método(s) localizado(s).");
        tabControlMain.SelectedTab = tabArquivos;
        btnConfirmarAlteracoes.Enabled = methods.Count > 0;
      }
      catch (Exception ex)
      {
        AppendLog($"[ERRO] {ex.Message}");
        MessageBox.Show($"Erro durante a análise:\n{ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        MessageBox.Show("Nenhum arquivo foi identificado para modificação.",
            "Time Tracker Injector", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return;
      }

      var confirm = MessageBox.Show(
          "Deseja realmente aplicar as alterações nos arquivos listados?",
          "Confirmar Alterações",
          MessageBoxButtons.YesNo,
          MessageBoxIcon.Question
      );

      if (confirm != DialogResult.Yes)
        return;

      btnConfirmarAlteracoes.Enabled = false;
      AppendLog("Iniciando injeção de Stopwatchs e logs nos métodos selecionados...");

      // Simulação - futuramente aqui chamaremos o CodeRewriter
      Task.Run(async () =>
      {
        var rewriter = new CodeRewriter(_configManager.Current);
        // 'methods' é a lista retornada pelo SolutionAnalyzer (já preenchendo a aba 1)
        await rewriter.RewriteAsync(methods, _configManager.Current.MethodName);
        AppendLog("Instrumentação concluída com sucesso.");
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
