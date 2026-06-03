namespace NekoLib.Navigation.RuntimeTests.Winforms481.Pages.Home
{
    partial class HomePage
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

        #region Component Designer generated code

        private void InitializeComponent()
        {
            this.label1 = new System.Windows.Forms.Label();
            this.lblClickHint = new System.Windows.Forms.Label();
            this.SuspendLayout();
            //
            // label1
            //
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 24F);
            this.label1.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.label1.Location = new System.Drawing.Point(221, 185);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(185, 37);
            this.label1.TabIndex = 0;
            this.label1.Text = "Home Page";
            //
            // lblClickHint
            //
            this.lblClickHint.AutoSize = true;
            this.lblClickHint.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Italic);
            this.lblClickHint.ForeColor = System.Drawing.Color.DimGray;
            this.lblClickHint.Location = new System.Drawing.Point(225, 240);
            this.lblClickHint.Name = "lblClickHint";
            this.lblClickHint.Text = "(click anywhere to go to PAGE A)";
            //
            // HomePage
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.MistyRose;
            this.Controls.Add(this.lblClickHint);
            this.Controls.Add(this.label1);
            this.Name = "HomePage";
            this.Size = new System.Drawing.Size(632, 433);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label lblClickHint;
    }
}
