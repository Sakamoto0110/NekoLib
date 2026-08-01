namespace NekoLib.Watchdog.RuntimeTests.Supervisor481
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.Panel pnlTop;
        private System.Windows.Forms.Label lblTarget;
        private System.Windows.Forms.TextBox txtTarget;
        private System.Windows.Forms.Button btnBrowse;

        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.Button btnPing;
        private System.Windows.Forms.Button btnStatus;
        private System.Windows.Forms.Button btnPause;
        private System.Windows.Forms.Button btnResume;
        private System.Windows.Forms.Button btnRestart;

        private System.Windows.Forms.Label lblState;

        private System.Windows.Forms.SplitContainer split;
        private System.Windows.Forms.GroupBox grpLog;
        private System.Windows.Forms.RichTextBox rtbLog;
        private System.Windows.Forms.GroupBox grpErrors;
        private System.Windows.Forms.RichTextBox rtbErrors;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.pnlTop = new System.Windows.Forms.Panel();
            this.lblTarget = new System.Windows.Forms.Label();
            this.txtTarget = new System.Windows.Forms.TextBox();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.btnStart = new System.Windows.Forms.Button();
            this.btnStop = new System.Windows.Forms.Button();
            this.btnPing = new System.Windows.Forms.Button();
            this.btnStatus = new System.Windows.Forms.Button();
            this.btnPause = new System.Windows.Forms.Button();
            this.btnResume = new System.Windows.Forms.Button();
            this.btnRestart = new System.Windows.Forms.Button();
            this.lblState = new System.Windows.Forms.Label();
            this.split = new System.Windows.Forms.SplitContainer();
            this.grpLog = new System.Windows.Forms.GroupBox();
            this.rtbLog = new System.Windows.Forms.RichTextBox();
            this.grpErrors = new System.Windows.Forms.GroupBox();
            this.rtbErrors = new System.Windows.Forms.RichTextBox();

            this.pnlTop.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.split)).BeginInit();
            this.split.Panel1.SuspendLayout();
            this.split.Panel2.SuspendLayout();
            this.split.SuspendLayout();
            this.grpLog.SuspendLayout();
            this.grpErrors.SuspendLayout();
            this.SuspendLayout();

            //
            // pnlTop
            //
            this.pnlTop.Controls.Add(this.lblTarget);
            this.pnlTop.Controls.Add(this.txtTarget);
            this.pnlTop.Controls.Add(this.btnBrowse);
            this.pnlTop.Controls.Add(this.btnStart);
            this.pnlTop.Controls.Add(this.btnStop);
            this.pnlTop.Controls.Add(this.btnPing);
            this.pnlTop.Controls.Add(this.btnStatus);
            this.pnlTop.Controls.Add(this.btnPause);
            this.pnlTop.Controls.Add(this.btnResume);
            this.pnlTop.Controls.Add(this.btnRestart);
            this.pnlTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlTop.Location = new System.Drawing.Point(0, 0);
            this.pnlTop.Name = "pnlTop";
            this.pnlTop.Padding = new System.Windows.Forms.Padding(10);
            this.pnlTop.Size = new System.Drawing.Size(884, 92);
            this.pnlTop.TabIndex = 0;

            //
            // lblTarget
            //
            this.lblTarget.AutoSize = true;
            this.lblTarget.Location = new System.Drawing.Point(13, 16);
            this.lblTarget.Name = "lblTarget";
            this.lblTarget.Size = new System.Drawing.Size(61, 13);
            this.lblTarget.TabIndex = 0;
            this.lblTarget.Text = "Target exe:";

            //
            // txtTarget
            //
            this.txtTarget.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                | System.Windows.Forms.AnchorStyles.Right)));
            this.txtTarget.Location = new System.Drawing.Point(80, 13);
            this.txtTarget.Name = "txtTarget";
            this.txtTarget.Size = new System.Drawing.Size(740, 20);
            this.txtTarget.TabIndex = 1;

            //
            // btnBrowse
            //
            this.btnBrowse.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowse.Location = new System.Drawing.Point(826, 11);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(45, 23);
            this.btnBrowse.TabIndex = 2;
            this.btnBrowse.Text = "...";
            this.btnBrowse.UseVisualStyleBackColor = true;
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);

            //
            // btnStart
            //
            this.btnStart.Location = new System.Drawing.Point(13, 50);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(110, 28);
            this.btnStart.TabIndex = 3;
            this.btnStart.Text = "Start Watchdog";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);

            //
            // btnStop
            //
            this.btnStop.Enabled = false;
            this.btnStop.Location = new System.Drawing.Point(129, 50);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(110, 28);
            this.btnStop.TabIndex = 4;
            this.btnStop.Text = "Stop Watchdog";
            this.btnStop.UseVisualStyleBackColor = true;
            this.btnStop.Click += new System.EventHandler(this.btnStop_Click);

            //
            // btnPing
            //
            this.btnPing.Enabled = false;
            this.btnPing.Location = new System.Drawing.Point(255, 50);
            this.btnPing.Name = "btnPing";
            this.btnPing.Size = new System.Drawing.Size(75, 28);
            this.btnPing.TabIndex = 5;
            this.btnPing.Text = "Ping";
            this.btnPing.UseVisualStyleBackColor = true;
            this.btnPing.Click += new System.EventHandler(this.btnPing_Click);

            //
            // btnStatus
            //
            this.btnStatus.Enabled = false;
            this.btnStatus.Location = new System.Drawing.Point(336, 50);
            this.btnStatus.Name = "btnStatus";
            this.btnStatus.Size = new System.Drawing.Size(75, 28);
            this.btnStatus.TabIndex = 6;
            this.btnStatus.Text = "Status";
            this.btnStatus.UseVisualStyleBackColor = true;
            this.btnStatus.Click += new System.EventHandler(this.btnStatus_Click);

            //
            // btnPause
            //
            this.btnPause.Enabled = false;
            this.btnPause.Location = new System.Drawing.Point(417, 50);
            this.btnPause.Name = "btnPause";
            this.btnPause.Size = new System.Drawing.Size(75, 28);
            this.btnPause.TabIndex = 7;
            this.btnPause.Text = "Pause";
            this.btnPause.UseVisualStyleBackColor = true;
            this.btnPause.Click += new System.EventHandler(this.btnPause_Click);

            //
            // btnResume
            //
            this.btnResume.Enabled = false;
            this.btnResume.Location = new System.Drawing.Point(498, 50);
            this.btnResume.Name = "btnResume";
            this.btnResume.Size = new System.Drawing.Size(75, 28);
            this.btnResume.TabIndex = 8;
            this.btnResume.Text = "Resume";
            this.btnResume.UseVisualStyleBackColor = true;
            this.btnResume.Click += new System.EventHandler(this.btnResume_Click);

            //
            // btnRestart
            //
            this.btnRestart.Enabled = false;
            this.btnRestart.Location = new System.Drawing.Point(579, 50);
            this.btnRestart.Name = "btnRestart";
            this.btnRestart.Size = new System.Drawing.Size(95, 28);
            this.btnRestart.TabIndex = 9;
            this.btnRestart.Text = "Restart child";
            this.btnRestart.UseVisualStyleBackColor = true;
            this.btnRestart.Click += new System.EventHandler(this.btnRestart_Click);

            //
            // lblState
            //
            this.lblState.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
            this.lblState.ForeColor = System.Drawing.Color.Gainsboro;
            this.lblState.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblState.Location = new System.Drawing.Point(0, 92);
            this.lblState.Name = "lblState";
            this.lblState.Padding = new System.Windows.Forms.Padding(10, 6, 10, 6);
            this.lblState.Size = new System.Drawing.Size(884, 26);
            this.lblState.TabIndex = 1;
            this.lblState.Text = "State: stopped";
            this.lblState.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            //
            // split
            //
            this.split.Dock = System.Windows.Forms.DockStyle.Fill;
            this.split.Location = new System.Drawing.Point(0, 118);
            this.split.Name = "split";
            this.split.Panel1.Controls.Add(this.grpLog);
            this.split.Panel2.Controls.Add(this.grpErrors);
            this.split.Size = new System.Drawing.Size(884, 443);
            this.split.SplitterDistance = 520;
            this.split.TabIndex = 2;

            //
            // grpLog
            //
            this.grpLog.Controls.Add(this.rtbLog);
            this.grpLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grpLog.Location = new System.Drawing.Point(0, 0);
            this.grpLog.Name = "grpLog";
            this.grpLog.Padding = new System.Windows.Forms.Padding(6);
            this.grpLog.Size = new System.Drawing.Size(520, 443);
            this.grpLog.TabIndex = 0;
            this.grpLog.TabStop = false;
            this.grpLog.Text = "Live log stream";

            //
            // rtbLog
            //
            this.rtbLog.BackColor = System.Drawing.Color.FromArgb(24, 24, 24);
            this.rtbLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtbLog.Font = new System.Drawing.Font("Consolas", 9F);
            this.rtbLog.ForeColor = System.Drawing.Color.Gainsboro;
            this.rtbLog.Location = new System.Drawing.Point(6, 19);
            this.rtbLog.Name = "rtbLog";
            this.rtbLog.ReadOnly = true;
            this.rtbLog.Size = new System.Drawing.Size(508, 418);
            this.rtbLog.TabIndex = 0;
            this.rtbLog.Text = "";

            //
            // grpErrors
            //
            this.grpErrors.Controls.Add(this.rtbErrors);
            this.grpErrors.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grpErrors.Location = new System.Drawing.Point(0, 0);
            this.grpErrors.Name = "grpErrors";
            this.grpErrors.Padding = new System.Windows.Forms.Padding(6);
            this.grpErrors.Size = new System.Drawing.Size(360, 443);
            this.grpErrors.TabIndex = 0;
            this.grpErrors.TabStop = false;
            this.grpErrors.Text = "Command results / errors";

            //
            // rtbErrors
            //
            this.rtbErrors.BackColor = System.Drawing.Color.FromArgb(24, 24, 24);
            this.rtbErrors.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtbErrors.Font = new System.Drawing.Font("Consolas", 9F);
            this.rtbErrors.ForeColor = System.Drawing.Color.Gainsboro;
            this.rtbErrors.Location = new System.Drawing.Point(6, 19);
            this.rtbErrors.Name = "rtbErrors";
            this.rtbErrors.ReadOnly = true;
            this.rtbErrors.Size = new System.Drawing.Size(348, 418);
            this.rtbErrors.TabIndex = 0;
            this.rtbErrors.Text = "";

            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(884, 561);
            this.Controls.Add(this.split);
            this.Controls.Add(this.lblState);
            this.Controls.Add(this.pnlTop);
            this.MinimumSize = new System.Drawing.Size(720, 400);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "NekoLib Watchdog — Supervisor Dashboard";

            this.pnlTop.ResumeLayout(false);
            this.pnlTop.PerformLayout();
            this.split.Panel1.ResumeLayout(false);
            this.split.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.split)).EndInit();
            this.split.ResumeLayout(false);
            this.grpLog.ResumeLayout(false);
            this.grpErrors.ResumeLayout(false);
            this.ResumeLayout(false);
        }
    }
}
