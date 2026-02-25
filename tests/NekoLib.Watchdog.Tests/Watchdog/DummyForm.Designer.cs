using System;
using System.Drawing;
using System.Windows.Forms;
namespace NekoLib.Tests.Watchdog{

    partial class DummyForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            btnExit0 = new Button();
            btnExit1 = new Button();
            btnCrash = new Button();
            btnPause = new Button();
            btnShutdown = new Button();
            btnPing = new Button();
            btnStatus = new Button();
            outputBottom = new RichTextBox();
            splitContainer1 = new SplitContainer();
            outputTop = new RichTextBox();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            SuspendLayout();
            // 
            // btnExit0
            // 
            btnExit0.Location = new Point(66, 32);
            btnExit0.Name = "btnExit0";
            btnExit0.Size = new Size(94, 29);
            btnExit0.TabIndex = 0;
            btnExit0.Text = "Exit(0)";
            btnExit0.UseVisualStyleBackColor = true;
            btnExit0.Click += btnExit0_Click;
            // 
            // btnExit1
            // 
            btnExit1.Location = new Point(66, 67);
            btnExit1.Name = "btnExit1";
            btnExit1.Size = new Size(94, 29);
            btnExit1.TabIndex = 0;
            btnExit1.Text = "Exit(1)";
            btnExit1.UseVisualStyleBackColor = true;
            btnExit1.Click += btnExit1_Click;
            // 
            // btnCrash
            // 
            btnCrash.Location = new Point(66, 102);
            btnCrash.Name = "btnCrash";
            btnCrash.RightToLeft = RightToLeft.No;
            btnCrash.Size = new Size(94, 29);
            btnCrash.TabIndex = 0;
            btnCrash.Text = "Crash";
            btnCrash.UseVisualStyleBackColor = true;
            btnCrash.Click += btnCrash_Click;
            // 
            // btnPause
            // 
            btnPause.Location = new Point(66, 207);
            btnPause.Name = "btnPause";
            btnPause.RightToLeft = RightToLeft.No;
            btnPause.Size = new Size(94, 29);
            btnPause.TabIndex = 1;
            btnPause.Text = "Pause";
            btnPause.UseVisualStyleBackColor = true;
            btnPause.Click += btnPause_Click;
            // 
            // btnShutdown
            // 
            btnShutdown.Location = new Point(66, 242);
            btnShutdown.Name = "btnShutdown";
            btnShutdown.RightToLeft = RightToLeft.No;
            btnShutdown.Size = new Size(94, 29);
            btnShutdown.TabIndex = 1;
            btnShutdown.Text = "Shutdown";
            btnShutdown.UseVisualStyleBackColor = true;
            btnShutdown.Click += btnShutdown_Click;
            // 
            // btnPing
            // 
            btnPing.Location = new Point(66, 172);
            btnPing.Name = "btnPing";
            btnPing.RightToLeft = RightToLeft.No;
            btnPing.Size = new Size(94, 29);
            btnPing.TabIndex = 1;
            btnPing.Text = "Ping";
            btnPing.UseVisualStyleBackColor = true;
            btnPing.Click += btnPing_Click;
            // 
            // btnStatus
            // 
            btnStatus.Location = new Point(66, 137);
            btnStatus.Name = "btnStatus";
            btnStatus.RightToLeft = RightToLeft.No;
            btnStatus.Size = new Size(94, 29);
            btnStatus.TabIndex = 1;
            btnStatus.Text = "Status";
            btnStatus.UseVisualStyleBackColor = true;
            btnStatus.Click += btnStatus_Click;
            // 
            // outputBottom
            // 
            outputBottom.Dock = DockStyle.Fill;
            outputBottom.Location = new Point(0, 0);
            outputBottom.Name = "outputBottom";
            outputBottom.Size = new Size(722, 144);
            outputBottom.TabIndex = 2;
            outputBottom.Text = "";
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Right;
            splitContainer1.Location = new Point(211, 0);
            splitContainer1.Name = "splitContainer1";
            splitContainer1.Orientation = Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(outputTop);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(outputBottom);
            splitContainer1.Size = new Size(722, 301);
            splitContainer1.SplitterDistance = 149;
            splitContainer1.SplitterWidth = 8;
            splitContainer1.TabIndex = 3;
            // 
            // outputTop
            // 
            outputTop.Dock = DockStyle.Fill;
            outputTop.Location = new Point(0, 0);
            outputTop.Name = "outputTop";
            outputTop.Size = new Size(722, 149);
            outputTop.TabIndex = 2;
            outputTop.Text = "";
            // 
            // DummyForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(933, 301);
            Controls.Add(splitContainer1);
            Controls.Add(btnShutdown);
            Controls.Add(btnStatus);
            Controls.Add(btnPing);
            Controls.Add(btnPause);
            Controls.Add(btnCrash);
            Controls.Add(btnExit1);
            Controls.Add(btnExit0);
            Name = "DummyForm";
            Text = "Form1";
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private Button btnExit0;
        private Button btnExit1;
        private Button btnCrash;
        private Button btnPause;
        private Button btnShutdown;
        private Button btnPing;
        private Button btnStatus;
        private RichTextBox outputBottom;
        private SplitContainer splitContainer1;
        private RichTextBox outputTop;
    }
}