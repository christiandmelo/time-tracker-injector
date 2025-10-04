namespace TimeTrackerInjector.UI
{
  partial class ConfigForm
  {
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null))
      {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
      this.lblSolutionFile = new System.Windows.Forms.Label();
      this.txtSolutionFile = new System.Windows.Forms.TextBox();
      this.lblProjectName = new System.Windows.Forms.Label();
      this.txtProjectName = new System.Windows.Forms.TextBox();
      this.lblClassName = new System.Windows.Forms.Label();
      this.txtClassName = new System.Windows.Forms.TextBox();
      this.lblMethodName = new System.Windows.Forms.Label();
      this.txtMethodName = new System.Windows.Forms.TextBox();
      this.lblLogVariable = new System.Windows.Forms.Label();
      this.txtLogVariable = new System.Windows.Forms.TextBox();
      this.chkRecursive = new System.Windows.Forms.CheckBox();
      this.chkGlobalStopwatch = new System.Windows.Forms.CheckBox();
      this.chkOverwriteOriginal = new System.Windows.Forms.CheckBox();
      this.chkGenerateReport = new System.Windows.Forms.CheckBox();
      this.btnSalvar = new System.Windows.Forms.Button();
      this.btnCancelar = new System.Windows.Forms.Button();
      this.SuspendLayout();
      // 
      // lblSolutionFile
      // 
      this.lblSolutionFile.AutoSize = true;
      this.lblSolutionFile.Location = new System.Drawing.Point(20, 22);
      this.lblSolutionFile.Name = "lblSolutionFile";
      this.lblSolutionFile.Size = new System.Drawing.Size(92, 15);
      this.lblSolutionFile.TabIndex = 0;
      this.lblSolutionFile.Text = "Solution (.sln):";
      // 
      // txtSolutionFile
      // 
      this.txtSolutionFile.Location = new System.Drawing.Point(180, 18);
      this.txtSolutionFile.Name = "txtSolutionFile";
      this.txtSolutionFile.Size = new System.Drawing.Size(550, 23);
      this.txtSolutionFile.TabIndex = 1;
      // 
      // lblProjectName
      // 
      this.lblProjectName.AutoSize = true;
      this.lblProjectName.Location = new System.Drawing.Point(20, 62);
      this.lblProjectName.Name = "lblProjectName";
      this.lblProjectName.Size = new System.Drawing.Size(47, 15);
      this.lblProjectName.TabIndex = 2;
      this.lblProjectName.Text = "Projeto:";
      // 
      // txtProjectName
      // 
      this.txtProjectName.Location = new System.Drawing.Point(180, 58);
      this.txtProjectName.Name = "txtProjectName";
      this.txtProjectName.Size = new System.Drawing.Size(550, 23);
      this.txtProjectName.TabIndex = 3;
      // 
      // lblClassName
      // 
      this.lblClassName.AutoSize = true;
      this.lblClassName.Location = new System.Drawing.Point(20, 102);
      this.lblClassName.Name = "lblClassName";
      this.lblClassName.Size = new System.Drawing.Size(45, 15);
      this.lblClassName.TabIndex = 4;
      this.lblClassName.Text = "Classe:";
      // 
      // txtClassName
      // 
      this.txtClassName.Location = new System.Drawing.Point(180, 98);
      this.txtClassName.Name = "txtClassName";
      this.txtClassName.Size = new System.Drawing.Size(550, 23);
      this.txtClassName.TabIndex = 5;
      // 
      // lblMethodName
      // 
      this.lblMethodName.AutoSize = true;
      this.lblMethodName.Location = new System.Drawing.Point(20, 142);
      this.lblMethodName.Name = "lblMethodName";
      this.lblMethodName.Size = new System.Drawing.Size(53, 15);
      this.lblMethodName.TabIndex = 6;
      this.lblMethodName.Text = "Método:";
      // 
      // txtMethodName
      // 
      this.txtMethodName.Location = new System.Drawing.Point(180, 138);
      this.txtMethodName.Name = "txtMethodName";
      this.txtMethodName.Size = new System.Drawing.Size(550, 23);
      this.txtMethodName.TabIndex = 7;
      // 
      // lblLogVariable
      // 
      this.lblLogVariable.AutoSize = true;
      this.lblLogVariable.Location = new System.Drawing.Point(20, 182);
      this.lblLogVariable.Name = "lblLogVariable";
      this.lblLogVariable.Size = new System.Drawing.Size(96, 15);
      this.lblLogVariable.TabIndex = 8;
      this.lblLogVariable.Text = "Variável de log:";
      // 
      // txtLogVariable
      // 
      this.txtLogVariable.Location = new System.Drawing.Point(180, 178);
      this.txtLogVariable.Name = "txtLogVariable";
      this.txtLogVariable.Size = new System.Drawing.Size(550, 23);
      this.txtLogVariable.TabIndex = 9;
      // 
      // chkRecursive
      // 
      this.chkRecursive.AutoSize = true;
      this.chkRecursive.Location = new System.Drawing.Point(180, 218);
      this.chkRecursive.Name = "chkRecursive";
      this.chkRecursive.Size = new System.Drawing.Size(227, 19);
      this.chkRecursive.TabIndex = 10;
      this.chkRecursive.Text = "Instrumentar métodos recursivamente";
      this.chkRecursive.UseVisualStyleBackColor = true;
      // 
      // chkGlobalStopwatch
      // 
      this.chkGlobalStopwatch.AutoSize = true;
      this.chkGlobalStopwatch.Location = new System.Drawing.Point(180, 247);
      this.chkGlobalStopwatch.Name = "chkGlobalStopwatch";
      this.chkGlobalStopwatch.Size = new System.Drawing.Size(244, 19);
      this.chkGlobalStopwatch.TabIndex = 11;
      this.chkGlobalStopwatch.Text = "Usar Stopwatch global (acumular tempo)";
      this.chkGlobalStopwatch.UseVisualStyleBackColor = true;
      // 
      // chkOverwriteOriginal
      // 
      this.chkOverwriteOriginal.AutoSize = true;
      this.chkOverwriteOriginal.Location = new System.Drawing.Point(180, 276);
      this.chkOverwriteOriginal.Name = "chkOverwriteOriginal";
      this.chkOverwriteOriginal.Size = new System.Drawing.Size(174, 19);
      this.chkOverwriteOriginal.TabIndex = 12;
      this.chkOverwriteOriginal.Text = "Sobrescrever arquivos originais";
      this.chkOverwriteOriginal.UseVisualStyleBackColor = true;
      // 
      // chkGenerateReport
      // 
      this.chkGenerateReport.AutoSize = true;
      this.chkGenerateReport.Location = new System.Drawing.Point(180, 305);
      this.chkGenerateReport.Name = "chkGenerateReport";
      this.chkGenerateReport.Size = new System.Drawing.Size(190, 19);
      this.chkGenerateReport.TabIndex = 13;
      this.chkGenerateReport.Text = "Gerar relatório de instrumentação";
      this.chkGenerateReport.UseVisualStyleBackColor = true;
      // 
      // btnSalvar
      // 
      this.btnSalvar.Location = new System.Drawing.Point(520, 355);
      this.btnSalvar.Name = "btnSalvar";
      this.btnSalvar.Size = new System.Drawing.Size(100, 35);
      this.btnSalvar.TabIndex = 14;
      this.btnSalvar.Text = "Salvar";
      this.btnSalvar.UseVisualStyleBackColor = true;
      this.btnSalvar.Click += new System.EventHandler(this.btnSalvar_Click);
      // 
      // btnCancelar
      // 
      this.btnCancelar.Location = new System.Drawing.Point(630, 355);
      this.btnCancelar.Name = "btnCancelar";
      this.btnCancelar.Size = new System.Drawing.Size(100, 35);
      this.btnCancelar.TabIndex = 15;
      this.btnCancelar.Text = "Cancelar";
      this.btnCancelar.UseVisualStyleBackColor = true;
      this.btnCancelar.Click += new System.EventHandler(this.btnCancelar_Click);
      // 
      // ConfigForm
      // 
      this.AcceptButton = this.btnSalvar;
      this.CancelButton = this.btnCancelar;
      this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.ClientSize = new System.Drawing.Size(770, 420);
      this.Controls.Add(this.btnCancelar);
      this.Controls.Add(this.btnSalvar);
      this.Controls.Add(this.chkGenerateReport);
      this.Controls.Add(this.chkOverwriteOriginal);
      this.Controls.Add(this.chkGlobalStopwatch);
      this.Controls.Add(this.chkRecursive);
      this.Controls.Add(this.txtLogVariable);
      this.Controls.Add(this.lblLogVariable);
      this.Controls.Add(this.txtMethodName);
      this.Controls.Add(this.lblMethodName);
      this.Controls.Add(this.txtClassName);
      this.Controls.Add(this.lblClassName);
      this.Controls.Add(this.txtProjectName);
      this.Controls.Add(this.lblProjectName);
      this.Controls.Add(this.txtSolutionFile);
      this.Controls.Add(this.lblSolutionFile);
      this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
      this.MaximizeBox = false;
      this.MinimizeBox = false;
      this.Name = "ConfigForm";
      this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
      this.Text = "Configuração do Time Tracker Injector";
      this.Load += new System.EventHandler(this.ConfigForm_Load);
      this.ResumeLayout(false);
      this.PerformLayout();
    }

    #endregion

    private System.Windows.Forms.Label lblSolutionFile;
    private System.Windows.Forms.TextBox txtSolutionFile;
    private System.Windows.Forms.Label lblProjectName;
    private System.Windows.Forms.TextBox txtProjectName;
    private System.Windows.Forms.Label lblClassName;
    private System.Windows.Forms.TextBox txtClassName;
    private System.Windows.Forms.Label lblMethodName;
    private System.Windows.Forms.TextBox txtMethodName;
    private System.Windows.Forms.Label lblLogVariable;
    private System.Windows.Forms.TextBox txtLogVariable;
    private System.Windows.Forms.CheckBox chkRecursive;
    private System.Windows.Forms.CheckBox chkGlobalStopwatch;
    private System.Windows.Forms.CheckBox chkOverwriteOriginal;
    private System.Windows.Forms.CheckBox chkGenerateReport;
    private System.Windows.Forms.Button btnSalvar;
    private System.Windows.Forms.Button btnCancelar;
  }
}