namespace BundlerTool
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

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
            this.btnSelectDir = new System.Windows.Forms.Button();
            this.txtWorkingDir = new System.Windows.Forms.TextBox();
            this.btnCreateBundle = new System.Windows.Forms.Button();
            this.btnOpenFolder = new System.Windows.Forms.Button();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.lblStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnSelectDir
            // 
            this.btnSelectDir.Location = new System.Drawing.Point(12, 12);
            this.btnSelectDir.Name = "btnSelectDir";
            this.btnSelectDir.Size = new System.Drawing.Size(120, 30);
            this.btnSelectDir.TabIndex = 0;
            this.btnSelectDir.Text = "Select Directory";
            this.btnSelectDir.UseVisualStyleBackColor = true;
            this.btnSelectDir.Click += new System.EventHandler(this.btnSelectDir_Click);
            // 
            // txtWorkingDir
            // 
            this.txtWorkingDir.Location = new System.Drawing.Point(138, 17);
            this.txtWorkingDir.Name = "txtWorkingDir";
            this.txtWorkingDir.ReadOnly = true;
            this.txtWorkingDir.Size = new System.Drawing.Size(434, 20);
            this.txtWorkingDir.TabIndex = 1;
            // 
            // btnCreateBundle
            // 
            this.btnCreateBundle.Enabled = false;
            this.btnCreateBundle.Location = new System.Drawing.Point(12, 60);
            this.btnCreateBundle.Name = "btnCreateBundle";
            this.btnCreateBundle.Size = new System.Drawing.Size(120, 30);
            this.btnCreateBundle.TabIndex = 2;
            this.btnCreateBundle.Text = "Create Bundle";
            this.btnCreateBundle.UseVisualStyleBackColor = true;
            this.btnCreateBundle.Click += new System.EventHandler(this.btnCreateBundle_Click);
            // 
            // btnOpenFolder
            // 
            this.btnOpenFolder.Location = new System.Drawing.Point(138, 60);
            this.btnOpenFolder.Name = "btnOpenFolder";
            this.btnOpenFolder.Size = new System.Drawing.Size(150, 30);
            this.btnOpenFolder.TabIndex = 3;
            this.btnOpenFolder.Text = "Open Bundles Folder";
            this.btnOpenFolder.UseVisualStyleBackColor = true;
            this.btnOpenFolder.Click += new System.EventHandler(this.btnOpenFolder_Click);
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblStatus});
            this.statusStrip1.Location = new System.Drawing.Point(0, 119);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(584, 22);
            this.statusStrip1.TabIndex = 4;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // lblStatus
            // 
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(39, 17);
            this.lblStatus.Text = "Ready";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(584, 141);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.btnOpenFolder);
            this.Controls.Add(this.btnCreateBundle);
            this.Controls.Add(this.txtWorkingDir);
            this.Controls.Add(this.btnSelectDir);
            this.Name = "MainForm";
            this.Text = "Source Code Bundler";
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnSelectDir;
        private System.Windows.Forms.TextBox txtWorkingDir;
        private System.Windows.Forms.Button btnCreateBundle;
        private System.Windows.Forms.Button btnOpenFolder;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel lblStatus;
    }
}
