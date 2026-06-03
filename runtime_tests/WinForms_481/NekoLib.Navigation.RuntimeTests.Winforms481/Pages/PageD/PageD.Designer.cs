namespace NekoLib.Navigation.RuntimeTests.Winforms481.Pages.PageD
{
    partial class PageD
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
            this.lblTitle = new System.Windows.Forms.Label();
            this.btnBack = new System.Windows.Forms.Button();
            this.btnToF = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // lblTitle
            //
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 28F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Location = new System.Drawing.Point(40, 40);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Text = "PAGE D";
            //
            // btnBack
            //
            this.btnBack.Location = new System.Drawing.Point(40, 100);
            this.btnBack.Name = "btnBack";
            this.btnBack.Size = new System.Drawing.Size(140, 34);
            this.btnBack.Text = "← Back";
            this.btnBack.UseVisualStyleBackColor = true;
            //
            // btnToF
            //
            this.btnToF.Location = new System.Drawing.Point(40, 144);
            this.btnToF.Name = "btnToF";
            this.btnToF.Size = new System.Drawing.Size(140, 34);
            this.btnToF.Text = "Go to PAGE F →";
            this.btnToF.UseVisualStyleBackColor = true;
            //
            // PageD
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.LightSalmon;
            this.Controls.Add(this.btnToF);
            this.Controls.Add(this.btnBack);
            this.Controls.Add(this.lblTitle);
            this.Name = "PageD";
            this.Size = new System.Drawing.Size(800, 600);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Button btnBack;
        private System.Windows.Forms.Button btnToF;
    }
}
