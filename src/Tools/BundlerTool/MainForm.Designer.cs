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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.btnSelectDir = new System.Windows.Forms.Button();
            this.txtWorkingDir = new System.Windows.Forms.TextBox();
            this.btnCreateBundle = new System.Windows.Forms.Button();
            this.btnOpenFolder = new System.Windows.Forms.Button();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.lblStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.btnBundleIgnore = new System.Windows.Forms.Button();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnSelectDir
            // 
            this.btnSelectDir.Location = new System.Drawing.Point(16, 15);
            this.btnSelectDir.Margin = new System.Windows.Forms.Padding(4);
            this.btnSelectDir.Name = "btnSelectDir";
            this.btnSelectDir.Size = new System.Drawing.Size(160, 37);
            this.btnSelectDir.TabIndex = 0;
            this.btnSelectDir.Text = "Select Directory";
            this.btnSelectDir.UseVisualStyleBackColor = true;
            this.btnSelectDir.Click += new System.EventHandler(this.btnSelectDir_Click);
            // 
            // txtWorkingDir
            // 
            this.txtWorkingDir.Location = new System.Drawing.Point(197, 22);
            this.txtWorkingDir.Margin = new System.Windows.Forms.Padding(4);
            this.txtWorkingDir.Name = "txtWorkingDir";
            this.txtWorkingDir.ReadOnly = true;
            this.txtWorkingDir.Size = new System.Drawing.Size(408, 22);
            this.txtWorkingDir.TabIndex = 1;
            // 
            // btnCreateBundle
            // 
            this.btnCreateBundle.Enabled = false;
            this.btnCreateBundle.Location = new System.Drawing.Point(16, 74);
            this.btnCreateBundle.Margin = new System.Windows.Forms.Padding(4);
            this.btnCreateBundle.Name = "btnCreateBundle";
            this.btnCreateBundle.Size = new System.Drawing.Size(160, 37);
            this.btnCreateBundle.TabIndex = 2;
            this.btnCreateBundle.Text = "Create Bundle";
            this.btnCreateBundle.UseVisualStyleBackColor = true;
            this.btnCreateBundle.Click += new System.EventHandler(this.btnCreateBundle_Click);
            // 
            // btnOpenFolder
            // 
            this.btnOpenFolder.Location = new System.Drawing.Point(197, 74);
            this.btnOpenFolder.Margin = new System.Windows.Forms.Padding(4);
            this.btnOpenFolder.Name = "btnOpenFolder";
            this.btnOpenFolder.Size = new System.Drawing.Size(200, 37);
            this.btnOpenFolder.TabIndex = 3;
            this.btnOpenFolder.Text = "Open Bundles Folder";
            this.btnOpenFolder.UseVisualStyleBackColor = true;
            this.btnOpenFolder.Click += new System.EventHandler(this.btnOpenFolder_Click);
            // 
            // statusStrip1
            // 
            this.statusStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblStatus});
            this.statusStrip1.Location = new System.Drawing.Point(0, 148);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Padding = new System.Windows.Forms.Padding(1, 0, 19, 0);
            this.statusStrip1.Size = new System.Drawing.Size(625, 26);
            this.statusStrip1.TabIndex = 4;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // lblStatus
            // 
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(50, 20);
            this.lblStatus.Text = "Ready";
            // 
            // btnBundleIgnore
            // 
            this.btnBundleIgnore.Location = new System.Drawing.Point(405, 74);
            this.btnBundleIgnore.Margin = new System.Windows.Forms.Padding(4);
            this.btnBundleIgnore.Name = "btnBundleIgnore";
            this.btnBundleIgnore.Size = new System.Drawing.Size(200, 37);
            this.btnBundleIgnore.TabIndex = 3;
            this.btnBundleIgnore.Text = "Edit .BundleIgnore";
            this.btnBundleIgnore.UseVisualStyleBackColor = true;
            this.btnBundleIgnore.Click += new System.EventHandler(this.btnBundleIgnore_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(625, 174);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.btnBundleIgnore);
            this.Controls.Add(this.btnOpenFolder);
            this.Controls.Add(this.btnCreateBundle);
            this.Controls.Add(this.txtWorkingDir);
            this.Controls.Add(this.btnSelectDir);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4);
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
        private System.Windows.Forms.Button btnBundleIgnore;
    }
}
