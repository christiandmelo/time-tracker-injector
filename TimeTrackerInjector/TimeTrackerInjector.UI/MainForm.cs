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

    private void configuraçãoToolStripMenuItem_Click(object sender, EventArgs e)
    {
      using var configForm = new ConfigForm(_configManager);
      var result = configForm.ShowDialog(this);

      if (result == DialogResult.OK)
      {
        // Recarrega a configuração após salvar
        _configManager.Load();
        MessageBox.Show("Configuração atualizada com sucesso!", "Time Tracker Injector",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
      }
    }
  }
}
