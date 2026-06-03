namespace NekoLib.Navigation.RuntimeTests.Winforms481
{
    partial class TestForm
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
            this.hostPanel = new System.Windows.Forms.Panel();
            this.SuspendLayout();
            //
            // hostPanel
            //
            this.hostPanel.BackColor = System.Drawing.Color.Black;
            this.hostPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.hostPanel.Location = new System.Drawing.Point(0, 0);
            this.hostPanel.Name = "hostPanel";
            this.hostPanel.Size = new System.Drawing.Size(900, 600);
            this.hostPanel.TabIndex = 0;
            //
            // TestForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(900, 600);
            this.Controls.Add(this.hostPanel);
            this.Name = "TestForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Navigation demo — primary";
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Panel hostPanel;
    }
}
