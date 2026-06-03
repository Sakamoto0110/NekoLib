namespace NavigationDemo
{
    partial class TestToolsForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Unsubscribe from the static A-5 probe so the form doesn't keep a
                // GC root after close.
                NavigationDemo.Pages.HeavyPageBackground.HeavyPageBackground.ApplyFired -= OnApplyFired;

                if (components != null)
                    components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.rootTable = new System.Windows.Forms.TableLayoutPanel();
            this.scrollPanel = new System.Windows.Forms.Panel();
            this.buttonPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.logBox = new System.Windows.Forms.RichTextBox();
            this.rootTable.SuspendLayout();
            this.scrollPanel.SuspendLayout();
            this.SuspendLayout();
            //
            // rootTable
            //
            this.rootTable.ColumnCount = 1;
            this.rootTable.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.rootTable.Controls.Add(this.scrollPanel, 0, 0);
            this.rootTable.Controls.Add(this.logBox, 0, 1);
            this.rootTable.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rootTable.Name = "rootTable";
            this.rootTable.Padding = new System.Windows.Forms.Padding(10);
            this.rootTable.RowCount = 2;
            this.rootTable.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 65F));
            this.rootTable.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 35F));
            //
            // scrollPanel
            //
            this.scrollPanel.AutoScroll = true;
            this.scrollPanel.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.scrollPanel.Controls.Add(this.buttonPanel);
            this.scrollPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.scrollPanel.Name = "scrollPanel";
            //
            // buttonPanel
            //
            this.buttonPanel.AutoSize = true;
            this.buttonPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.buttonPanel.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.buttonPanel.Margin = new System.Windows.Forms.Padding(0);
            this.buttonPanel.Name = "buttonPanel";
            this.buttonPanel.WrapContents = false;
            //
            // logBox
            //
            this.logBox.BackColor = System.Drawing.Color.FromArgb(245, 245, 245);
            this.logBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.logBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.logBox.Font = new System.Drawing.Font("Consolas", 8.5F);
            this.logBox.Margin = new System.Windows.Forms.Padding(0, 10, 0, 0);
            this.logBox.Name = "logBox";
            this.logBox.ReadOnly = true;
            //
            // TestToolsForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(380, 720);
            this.Controls.Add(this.rootTable);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.MinimumSize = new System.Drawing.Size(320, 500);
            this.Name = "TestToolsForm";
            this.ShowInTaskbar = false;
            this.Text = "Navigation demo — tools";
            this.rootTable.ResumeLayout(false);
            this.scrollPanel.ResumeLayout(false);
            this.scrollPanel.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel rootTable;
        private System.Windows.Forms.Panel scrollPanel;
        private System.Windows.Forms.FlowLayoutPanel buttonPanel;
        private System.Windows.Forms.RichTextBox logBox;
    }
}
