namespace NavigationDemo.Pages.BottomLeftToast
{
    partial class BottomLeftToastView
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            // Forward to the user-code partial so positioning hooks unhook before
            // the framework disposes the base.
            DisposeOverrides(disposing);

            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        partial void DisposeOverrides(bool disposing);

        #region Component Designer generated code

        private void InitializeComponent()
        {
            this.lblMessage = new System.Windows.Forms.Label();
            this.SuspendLayout();
            //
            // lblMessage
            //
            this.lblMessage.BackColor = System.Drawing.Color.Transparent;
            this.lblMessage.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblMessage.Font = new System.Drawing.Font("Microsoft Sans Serif", 11F, System.Drawing.FontStyle.Bold);
            this.lblMessage.ForeColor = System.Drawing.Color.White;
            this.lblMessage.Name = "lblMessage";
            this.lblMessage.Text = "Toast";
            this.lblMessage.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // BottomLeftToastView
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            this.Controls.Add(this.lblMessage);
            this.Name = "BottomLeftToastView";
            this.Size = new System.Drawing.Size(280, 56);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Label lblMessage;
    }
}
