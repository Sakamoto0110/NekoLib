using System.Drawing;
using System.Windows.Forms;

namespace NavigationDemo.Pages.SimpleToast
{
    partial class SimpleToastView
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
            this.lblMessage = new System.Windows.Forms.Label();
            this.SuspendLayout();
            //
            // lblMessage
            //
            this.lblMessage.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblMessage.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold);
            this.lblMessage.ForeColor = System.Drawing.Color.White;
            this.lblMessage.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblMessage.Text = "Toast";
            this.lblMessage.BackColor = System.Drawing.Color.Transparent;
            //
            // SimpleToastView
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            this.Controls.Add(this.lblMessage);
            this.Name = "SimpleToastView";
            this.Size = new System.Drawing.Size(320, 64);
            this.ResumeLayout(false);
        }

        #endregion

        private Label lblMessage;
    }
}
