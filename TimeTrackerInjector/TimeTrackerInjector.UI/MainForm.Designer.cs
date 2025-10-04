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
      menuStrip1 = new MenuStrip();
      configuraçãoToolStripMenuItem = new ToolStripMenuItem();
      btnExecutar = new Button();
      tabControlMain = new TabControl();
      tabArquivos = new TabPage();
      gridArquivos = new DataGridView();
      btnConfirmarAlteracoes = new Button();
      dataGridViewTextBoxColumn1 = new DataGridViewTextBoxColumn();
      dataGridViewTextBoxColumn2 = new DataGridViewTextBoxColumn();
      dataGridViewTextBoxColumn3 = new DataGridViewTextBoxColumn();
      dataGridViewTextBoxColumn4 = new DataGridViewTextBoxColumn();
      tabLog = new TabPage();
      txtLog = new TextBox();
      menuStrip1.SuspendLayout();
      tabControlMain.SuspendLayout();
      tabArquivos.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)gridArquivos).BeginInit();
      tabLog.SuspendLayout();
      SuspendLayout();
      // 
      // menuStrip1
      // 
      menuStrip1.ImageScalingSize = new Size(20, 20);
      menuStrip1.Items.AddRange(new ToolStripItem[] { configuraçãoToolStripMenuItem });
      menuStrip1.Location = new Point(0, 0);
      menuStrip1.Name = "menuStrip1";
      menuStrip1.Size = new Size(1000, 28);
      menuStrip1.TabIndex = 2;
      // 
      // configuraçãoToolStripMenuItem
      // 
      configuraçãoToolStripMenuItem.Name = "configuraçãoToolStripMenuItem";
      configuraçãoToolStripMenuItem.Size = new Size(112, 24);
      configuraçãoToolStripMenuItem.Text = "Configuração";
      configuraçãoToolStripMenuItem.Click += configuraçãoToolStripMenuItem_Click;
      // 
      // btnExecutar
      // 
      btnExecutar.Location = new Point(12, 40);
      btnExecutar.Name = "btnExecutar";
      btnExecutar.Size = new Size(120, 35);
      btnExecutar.TabIndex = 1;
      btnExecutar.Text = "Executar";
      btnExecutar.Click += btnExecutar_Click;
      // 
      // tabControlMain
      // 
      tabControlMain.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
      tabControlMain.Controls.Add(tabArquivos);
      tabControlMain.Controls.Add(tabLog);
      tabControlMain.Location = new Point(12, 90);
      tabControlMain.Name = "tabControlMain";
      tabControlMain.SelectedIndex = 0;
      tabControlMain.Size = new Size(960, 340);
      tabControlMain.TabIndex = 0;
      // 
      // tabArquivos
      // 
      tabArquivos.Controls.Add(gridArquivos);
      tabArquivos.Controls.Add(btnConfirmarAlteracoes);
      tabArquivos.Location = new Point(4, 29);
      tabArquivos.Name = "tabArquivos";
      tabArquivos.Size = new Size(952, 307);
      tabArquivos.TabIndex = 0;
      tabArquivos.Text = "Arquivos que serão alterados";
      // 
      // gridArquivos
      // 
      gridArquivos.AllowUserToAddRows = false;
      gridArquivos.AllowUserToDeleteRows = false;
      gridArquivos.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
      gridArquivos.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
      gridArquivos.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
      gridArquivos.Columns.AddRange(new DataGridViewColumn[] { dataGridViewTextBoxColumn1, dataGridViewTextBoxColumn2, dataGridViewTextBoxColumn3, dataGridViewTextBoxColumn4 });
      gridArquivos.Location = new Point(0, 0);
      gridArquivos.Name = "gridArquivos";
      gridArquivos.ReadOnly = true;
      gridArquivos.RowHeadersWidth = 51;
      gridArquivos.Size = new Size(949, 261);
      gridArquivos.TabIndex = 0;
      // 
      // btnConfirmarAlteracoes
      // 
      btnConfirmarAlteracoes.Dock = DockStyle.Bottom;
      btnConfirmarAlteracoes.Enabled = false;
      btnConfirmarAlteracoes.Location = new Point(0, 267);
      btnConfirmarAlteracoes.Name = "btnConfirmarAlteracoes";
      btnConfirmarAlteracoes.Size = new Size(952, 40);
      btnConfirmarAlteracoes.TabIndex = 1;
      btnConfirmarAlteracoes.Text = "Confirmar Alterações";
      btnConfirmarAlteracoes.Click += btnConfirmarAlteracoes_Click;
      // 
      // dataGridViewTextBoxColumn1
      // 
      dataGridViewTextBoxColumn1.HeaderText = "Projeto";
      dataGridViewTextBoxColumn1.MinimumWidth = 6;
      dataGridViewTextBoxColumn1.Name = "dataGridViewTextBoxColumn1";
      dataGridViewTextBoxColumn1.ReadOnly = true;
      // 
      // dataGridViewTextBoxColumn2
      // 
      dataGridViewTextBoxColumn2.HeaderText = "Classe";
      dataGridViewTextBoxColumn2.MinimumWidth = 6;
      dataGridViewTextBoxColumn2.Name = "dataGridViewTextBoxColumn2";
      dataGridViewTextBoxColumn2.ReadOnly = true;
      // 
      // dataGridViewTextBoxColumn3
      // 
      dataGridViewTextBoxColumn3.HeaderText = "Método";
      dataGridViewTextBoxColumn3.MinimumWidth = 6;
      dataGridViewTextBoxColumn3.Name = "dataGridViewTextBoxColumn3";
      dataGridViewTextBoxColumn3.ReadOnly = true;
      // 
      // dataGridViewTextBoxColumn4
      // 
      dataGridViewTextBoxColumn4.HeaderText = "Arquivo";
      dataGridViewTextBoxColumn4.MinimumWidth = 6;
      dataGridViewTextBoxColumn4.Name = "dataGridViewTextBoxColumn4";
      dataGridViewTextBoxColumn4.ReadOnly = true;
      // 
      // tabLog
      // 
      tabLog.Controls.Add(txtLog);
      tabLog.Location = new Point(4, 29);
      tabLog.Name = "tabLog";
      tabLog.Size = new Size(952, 307);
      tabLog.TabIndex = 1;
      tabLog.Text = "Log de Execução";
      // 
      // txtLog
      // 
      txtLog.Dock = DockStyle.Fill;
      txtLog.Font = new Font("Consolas", 10F);
      txtLog.Location = new Point(0, 0);
      txtLog.Multiline = true;
      txtLog.Name = "txtLog";
      txtLog.ReadOnly = true;
      txtLog.ScrollBars = ScrollBars.Vertical;
      txtLog.Size = new Size(952, 307);
      txtLog.TabIndex = 0;
      // 
      // MainForm
      // 
      AutoScaleDimensions = new SizeF(8F, 20F);
      AutoScaleMode = AutoScaleMode.Font;
      ClientSize = new Size(1000, 480);
      Controls.Add(tabControlMain);
      Controls.Add(btnExecutar);
      Controls.Add(menuStrip1);
      MainMenuStrip = menuStrip1;
      Name = "MainForm";
      StartPosition = FormStartPosition.CenterScreen;
      Text = "Time Tracker Injector";
      menuStrip1.ResumeLayout(false);
      menuStrip1.PerformLayout();
      tabControlMain.ResumeLayout(false);
      tabArquivos.ResumeLayout(false);
      ((System.ComponentModel.ISupportInitialize)gridArquivos).EndInit();
      tabLog.ResumeLayout(false);
      tabLog.PerformLayout();
      ResumeLayout(false);
      PerformLayout();
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
    private DataGridViewTextBoxColumn dataGridViewTextBoxColumn1;
    private DataGridViewTextBoxColumn dataGridViewTextBoxColumn2;
    private DataGridViewTextBoxColumn dataGridViewTextBoxColumn3;
    private DataGridViewTextBoxColumn dataGridViewTextBoxColumn4;
  }
}
