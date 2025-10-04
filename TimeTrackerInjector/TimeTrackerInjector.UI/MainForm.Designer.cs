namespace TimeTrackerInjector.UI
{
  partial class MainForm
  {
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null))
        components.Dispose();
      base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
      this.menuStrip1 = new MenuStrip();
      this.configuraçãoToolStripMenuItem = new ToolStripMenuItem();
      this.btnExecutar = new Button();
      this.tabControlMain = new TabControl();
      this.tabArquivos = new TabPage();
      this.tabLog = new TabPage();
      this.gridArquivos = new DataGridView();
      this.btnConfirmarAlteracoes = new Button();
      this.txtLog = new TextBox();

      this.menuStrip1.SuspendLayout();
      this.tabControlMain.SuspendLayout();
      this.tabArquivos.SuspendLayout();
      this.tabLog.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)(this.gridArquivos)).BeginInit();
      this.SuspendLayout();

      // === MENU ===
      this.menuStrip1.ImageScalingSize = new Size(20, 20);
      this.menuStrip1.Items.AddRange(new ToolStripItem[] { this.configuraçãoToolStripMenuItem });
      this.menuStrip1.Location = new Point(0, 0);
      this.menuStrip1.Name = "menuStrip1";
      this.menuStrip1.Size = new Size(1000, 28);

      this.configuraçãoToolStripMenuItem.Name = "configuraçãoToolStripMenuItem";
      this.configuraçãoToolStripMenuItem.Size = new Size(112, 24);
      this.configuraçãoToolStripMenuItem.Text = "Configuração";
      this.configuraçãoToolStripMenuItem.Click += new EventHandler(this.configuraçãoToolStripMenuItem_Click);

      // === BOTÃO EXECUTAR ===
      this.btnExecutar.Text = "Executar";
      this.btnExecutar.Location = new Point(12, 40);
      this.btnExecutar.Size = new Size(120, 35);
      this.btnExecutar.Click += new EventHandler(this.btnExecutar_Click);

      // === TAB CONTROL ===
      this.tabControlMain.Controls.Add(this.tabArquivos);
      this.tabControlMain.Controls.Add(this.tabLog);
      this.tabControlMain.Location = new Point(12, 90);
      this.tabControlMain.Size = new Size(960, 340);
      this.tabControlMain.SelectedIndex = 0;

      // === TAB ARQUIVOS ===
      this.tabArquivos.Text = "Arquivos que serão alterados";

      // Grid
      this.gridArquivos.Dock = DockStyle.Fill;
      this.gridArquivos.AllowUserToAddRows = false;
      this.gridArquivos.AllowUserToDeleteRows = false;
      this.gridArquivos.ReadOnly = true;
      this.gridArquivos.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
      this.gridArquivos.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
      this.gridArquivos.Columns.Add("Projeto", "Projeto");
      this.gridArquivos.Columns.Add("Classe", "Classe");
      this.gridArquivos.Columns.Add("Método", "Método");
      this.gridArquivos.Columns.Add("Arquivo", "Arquivo");

      // Botão Confirmar Alterações
      this.btnConfirmarAlteracoes.Text = "Confirmar Alterações";
      this.btnConfirmarAlteracoes.Dock = DockStyle.Bottom;
      this.btnConfirmarAlteracoes.Height = 40;
      this.btnConfirmarAlteracoes.Enabled = false;
      this.btnConfirmarAlteracoes.Click += new EventHandler(this.btnConfirmarAlteracoes_Click);

      // Adiciona grid e botão na aba
      this.tabArquivos.Controls.Add(this.gridArquivos);
      this.tabArquivos.Controls.Add(this.btnConfirmarAlteracoes);

      // === TAB LOG ===
      this.tabLog.Text = "Log de Execução";
      this.txtLog.Dock = DockStyle.Fill;
      this.txtLog.Multiline = true;
      this.txtLog.ScrollBars = ScrollBars.Vertical;
      this.txtLog.Font = new Font("Consolas", 10);
      this.txtLog.ReadOnly = true;
      this.tabLog.Controls.Add(this.txtLog);

      // === FORM PRINCIPAL ===
      this.AutoScaleDimensions = new SizeF(8F, 20F);
      this.AutoScaleMode = AutoScaleMode.Font;
      this.ClientSize = new Size(1000, 480);
      this.Controls.Add(this.tabControlMain);
      this.Controls.Add(this.btnExecutar);
      this.Controls.Add(this.menuStrip1);
      this.MainMenuStrip = this.menuStrip1;
      this.Name = "MainForm";
      this.StartPosition = FormStartPosition.CenterScreen;
      this.Text = "Time Tracker Injector";

      this.menuStrip1.ResumeLayout(false);
      this.menuStrip1.PerformLayout();
      this.tabControlMain.ResumeLayout(false);
      this.tabArquivos.ResumeLayout(false);
      this.tabLog.ResumeLayout(false);
      this.tabLog.PerformLayout();
      ((System.ComponentModel.ISupportInitialize)(this.gridArquivos)).EndInit();
      this.ResumeLayout(false);
      this.PerformLayout();
    }

    #endregion

    private MenuStrip menuStrip1;
    private ToolStripMenuItem configuraçãoToolStripMenuItem;
    private Button btnExecutar;
    private TabControl tabControlMain;
    private TabPage tabArquivos;
    private TabPage tabLog;
    private DataGridView gridArquivos;
    private Button btnConfirmarAlteracoes;
    private TextBox txtLog;
  }
}
