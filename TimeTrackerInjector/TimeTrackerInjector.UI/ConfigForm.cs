using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TimeTrackerInjector.UI.Config;

namespace TimeTrackerInjector.UI
{
  public partial class ConfigForm : Form
  {
    private readonly ConfigurationManager _configManager;

    public ConfigForm(ConfigurationManager configManager)
    {
      InitializeComponent();
      _configManager = configManager;
    }

    private void ConfigForm_Load(object sender, EventArgs e)
    {
      var cfg = _configManager.Current;

      txtSolutionFile.Text = cfg.SolutionFile;
      txtProjectName.Text = cfg.ProjectName;
      txtClassName.Text = cfg.ClassName;
      txtMethodName.Text = cfg.MethodName;
      txtLogVariable.Text = cfg.LogVariable;
      chkRecursive.Checked = cfg.Recursive;
      chkGlobalStopwatch.Checked = cfg.GlobalStopwatch;
      chkOverwriteOriginal.Checked = cfg.OverwriteOriginal;
      chkGenerateReport.Checked = cfg.GenerateReport;
    }

    private void btnSalvar_Click(object sender, EventArgs e)
    {
      var cfg = _configManager.Current;

      cfg.SolutionFile = txtSolutionFile.Text.Trim();
      cfg.ProjectName = txtProjectName.Text.Trim();
      cfg.ClassName = txtClassName.Text.Trim();
      cfg.MethodName = txtMethodName.Text.Trim();
      cfg.LogVariable = txtLogVariable.Text.Trim();
      cfg.Recursive = chkRecursive.Checked;
      cfg.GlobalStopwatch = chkGlobalStopwatch.Checked;
      cfg.OverwriteOriginal = chkOverwriteOriginal.Checked;
      cfg.GenerateReport = chkGenerateReport.Checked;

      _configManager.Save();
      DialogResult = DialogResult.OK;
      Close();
    }

    private void btnCancelar_Click(object sender, EventArgs e)
    {
      DialogResult = DialogResult.Cancel;
      Close();
    }
  }
}
