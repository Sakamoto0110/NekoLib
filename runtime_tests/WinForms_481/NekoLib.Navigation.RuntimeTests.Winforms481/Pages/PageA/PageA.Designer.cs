namespace NekoLib.Navigation.RuntimeTests.Winforms481.Pages.PageA
{
    partial class PageA
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
            this.btnToB = new System.Windows.Forms.Button();
            this.btnToC = new System.Windows.Forms.Button();
            this.btnToD = new System.Windows.Forms.Button();
            this.btnToE = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // lblTitle
            //
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 24F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Location = new System.Drawing.Point(40, 40);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Text = "PAGE A  (hub)";
            //
            // btnBack
            //
            this.btnBack.Location = new System.Drawing.Point(40, 100);
            this.btnBack.Name = "btnBack";
            this.btnBack.Size = new System.Drawing.Size(160, 34);
            this.btnBack.Text = "← Back";
            this.btnBack.UseVisualStyleBackColor = true;
            //
            // btnToB
            //
            this.btnToB.Location = new System.Drawing.Point(40, 154);
            this.btnToB.Name = "btnToB";
            this.btnToB.Size = new System.Drawing.Size(160, 34);
            this.btnToB.Text = "→ PAGE B";
            this.btnToB.UseVisualStyleBackColor = true;
            //
            // btnToC
            //
            this.btnToC.Location = new System.Drawing.Point(40, 198);
            this.btnToC.Name = "btnToC";
            this.btnToC.Size = new System.Drawing.Size(160, 34);
            this.btnToC.Text = "→ PAGE C";
            this.btnToC.UseVisualStyleBackColor = true;
            //
            // btnToD
            //
            this.btnToD.Location = new System.Drawing.Point(40, 242);
            this.btnToD.Name = "btnToD";
            this.btnToD.Size = new System.Drawing.Size(160, 34);
            this.btnToD.Text = "→ PAGE D";
            this.btnToD.UseVisualStyleBackColor = true;
            //
            // btnToE
            //
            this.btnToE.Location = new System.Drawing.Point(40, 286);
            this.btnToE.Name = "btnToE";
            this.btnToE.Size = new System.Drawing.Size(160, 34);
            this.btnToE.Text = "→ PAGE E";
            this.btnToE.UseVisualStyleBackColor = true;
            //
            // PageA
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.LightBlue;
            this.Controls.Add(this.btnToE);
            this.Controls.Add(this.btnToD);
            this.Controls.Add(this.btnToC);
            this.Controls.Add(this.btnToB);
            this.Controls.Add(this.btnBack);
            this.Controls.Add(this.lblTitle);
            this.Name = "PageA";
            this.Size = new System.Drawing.Size(1055, 400);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Button btnBack;
        private System.Windows.Forms.Button btnToB;
        private System.Windows.Forms.Button btnToC;
        private System.Windows.Forms.Button btnToD;
        private System.Windows.Forms.Button btnToE;
    }
}
