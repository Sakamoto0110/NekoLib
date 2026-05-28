using System.Drawing;
using System.Windows.Forms;

namespace NavigationDemo.Pages.PageA
{
    partial class PageA
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.btnHome = new System.Windows.Forms.Button();
            this.btnDialog = new System.Windows.Forms.Button();
            this.btnToast = new System.Windows.Forms.Button();
            this.rtbDialogResult = new System.Windows.Forms.RichTextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.btnClear = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // btnHome
            //
            this.btnHome.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnHome.Location = new System.Drawing.Point(23, 242);
            this.btnHome.Margin = new System.Windows.Forms.Padding(2);
            this.btnHome.Name = "btnHome";
            this.btnHome.Size = new System.Drawing.Size(130, 24);
            this.btnHome.TabIndex = 0;
            this.btnHome.Text = "Return home";
            this.btnHome.UseVisualStyleBackColor = true;
            //
            // btnDialog
            //
            this.btnDialog.Location = new System.Drawing.Point(23, 36);
            this.btnDialog.Margin = new System.Windows.Forms.Padding(2);
            this.btnDialog.Name = "btnDialog";
            this.btnDialog.Size = new System.Drawing.Size(130, 24);
            this.btnDialog.TabIndex = 0;
            this.btnDialog.Text = "SpawnDialog";
            this.btnDialog.UseVisualStyleBackColor = true;
            //
            // btnToast
            //
            this.btnToast.Location = new System.Drawing.Point(23, 8);
            this.btnToast.Margin = new System.Windows.Forms.Padding(2);
            this.btnToast.Name = "btnToast";
            this.btnToast.Size = new System.Drawing.Size(130, 24);
            this.btnToast.TabIndex = 0;
            this.btnToast.Text = "SpawnToast";
            this.btnToast.UseVisualStyleBackColor = true;
            //
            // rtbDialogResult
            //
            this.rtbDialogResult.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)));
            this.rtbDialogResult.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.rtbDialogResult.Location = new System.Drawing.Point(23, 80);
            this.rtbDialogResult.Name = "rtbDialogResult";
            this.rtbDialogResult.ReadOnly = true;
            this.rtbDialogResult.Size = new System.Drawing.Size(130, 129);
            this.rtbDialogResult.TabIndex = 1;
            this.rtbDialogResult.Text = "";
            //
            // label1
            //
            this.label1.Location = new System.Drawing.Point(20, 62);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(133, 15);
            this.label1.TabIndex = 2;
            this.label1.Text = "Dialog result:";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // btnClear
            //
            this.btnClear.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnClear.Location = new System.Drawing.Point(23, 214);
            this.btnClear.Margin = new System.Windows.Forms.Padding(2);
            this.btnClear.Name = "btnClear";
            this.btnClear.Size = new System.Drawing.Size(130, 24);
            this.btnClear.TabIndex = 0;
            this.btnClear.Text = "Clear";
            this.btnClear.UseVisualStyleBackColor = true;
            //
            // PageA
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.label1);
            this.Controls.Add(this.rtbDialogResult);
            this.Controls.Add(this.btnToast);
            this.Controls.Add(this.btnDialog);
            this.Controls.Add(this.btnClear);
            this.Controls.Add(this.btnHome);
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "PageA";
            this.Size = new System.Drawing.Size(1055, 277);
            this.ResumeLayout(false);

        }

        #endregion

        private Button btnHome;
        private Button btnDialog;
        private Button btnToast;
        private RichTextBox rtbDialogResult;
        private Label label1;
        private Button btnClear;
    }
}
