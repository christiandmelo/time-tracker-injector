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

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
      menuStrip1 = new MenuStrip();
      configuraçãoToolStripMenuItem = new ToolStripMenuItem();
      menuStrip1.SuspendLayout();
      SuspendLayout();
      // 
      // menuStrip1
      // 
      menuStrip1.ImageScalingSize = new Size(20, 20);
      menuStrip1.Items.AddRange(new ToolStripItem[] { configuraçãoToolStripMenuItem });
      menuStrip1.Location = new Point(0, 0);
      menuStrip1.Name = "menuStrip1";
      menuStrip1.Size = new Size(800, 28);
      menuStrip1.TabIndex = 0;
      menuStrip1.Text = "menuStrip1";
      // 
      // configuraçãoToolStripMenuItem
      // 
      configuraçãoToolStripMenuItem.Name = "configuraçãoToolStripMenuItem";
      configuraçãoToolStripMenuItem.Size = new Size(112, 24);
      configuraçãoToolStripMenuItem.Text = "Configuração";
      configuraçãoToolStripMenuItem.Click += configuraçãoToolStripMenuItem_Click;
      // 
      // MainForm
      // 
      AutoScaleDimensions = new SizeF(8F, 20F);
      AutoScaleMode = AutoScaleMode.Font;
      ClientSize = new Size(800, 450);
      Controls.Add(menuStrip1);
      MainMenuStrip = menuStrip1;
      Name = "MainForm";
      Text = "Form1";
      menuStrip1.ResumeLayout(false);
      menuStrip1.PerformLayout();
      ResumeLayout(false);
      PerformLayout();
    }

    #endregion

    private MenuStrip menuStrip1;
    private ToolStripMenuItem configuraçãoToolStripMenuItem;
  }
}
