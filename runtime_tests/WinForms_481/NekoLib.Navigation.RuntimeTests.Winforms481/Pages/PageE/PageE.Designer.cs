namespace NekoLib.Navigation.RuntimeTests.Winforms481.Pages.PageE
{
    partial class PageE
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
            this.lblCounter = new System.Windows.Forms.Label();
            this.btnIncrement = new System.Windows.Forms.Button();
            this.btnPageF = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // lblTitle
            //
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 28F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Location = new System.Drawing.Point(40, 40);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Text = "PAGE E";
            //
            // btnBack
            //
            this.btnBack.Location = new System.Drawing.Point(40, 100);
            this.btnBack.Name = "btnBack";
            this.btnBack.Size = new System.Drawing.Size(140, 34);
            this.btnBack.Text = "← Back";
            this.btnBack.UseVisualStyleBackColor = true;
            //
            // lblCounter
            //
            this.lblCounter.AutoSize = true;
            this.lblCounter.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F);
            this.lblCounter.Location = new System.Drawing.Point(40, 160);
            this.lblCounter.Name = "lblCounter";
            this.lblCounter.Text = "Counter: 0";
            //
            // btnIncrement
            //
            this.btnIncrement.Location = new System.Drawing.Point(40, 188);
            this.btnIncrement.Name = "btnIncrement";
            this.btnIncrement.Size = new System.Drawing.Size(140, 34);
            this.btnIncrement.Text = "Increment +";
            this.btnIncrement.UseVisualStyleBackColor = true;
            //
            // btnPageF
            //
            this.btnPageF.Location = new System.Drawing.Point(40, 250);
            this.btnPageF.Name = "btnPageF";
            this.btnPageF.Size = new System.Drawing.Size(140, 34);
            this.btnPageF.Text = "Go to PageF";
            this.btnPageF.UseVisualStyleBackColor = true;
            //
            // PageE
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Plum;
            this.Controls.Add(this.btnPageF);
            this.Controls.Add(this.btnIncrement);
            this.Controls.Add(this.lblCounter);
            this.Controls.Add(this.btnBack);
            this.Controls.Add(this.lblTitle);
            this.Name = "PageE";
            this.Size = new System.Drawing.Size(800, 600);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Button btnBack;
        private System.Windows.Forms.Label lblCounter;
        private System.Windows.Forms.Button btnIncrement;
        private System.Windows.Forms.Button btnPageF;
    }
}
