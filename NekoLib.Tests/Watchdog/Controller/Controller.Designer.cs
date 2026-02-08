namespace NekoLib.Tests.Watchdog;

partial class Controller
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
        btnStart = new Button();
        btnPause = new Button();
        btnPing = new Button();
        btnStop = new Button();
        btnStatus = new Button();
        rtbLog = new RichTextBox();
        rtbStatus = new RichTextBox();
        SuspendLayout();
        // 
        // btnStart
        // 
        btnStart.Location = new Point(12, 119);
        btnStart.Name = "btnStart";
        btnStart.Size = new Size(94, 29);
        btnStart.TabIndex = 0;
        btnStart.Text = "Start";
        btnStart.UseVisualStyleBackColor = true;
        btnStart.Click += btnStart_Click;
        // 
        // btnPause
        // 
        btnPause.Location = new Point(12, 154);
        btnPause.Name = "btnPause";
        btnPause.Size = new Size(94, 29);
        btnPause.TabIndex = 0;
        btnPause.Text = "Pause";
        btnPause.UseVisualStyleBackColor = true;
        btnPause.Click += btnPause_Click;
        // 
        // btnPing
        // 
        btnPing.Location = new Point(12, 224);
        btnPing.Name = "btnPing";
        btnPing.Size = new Size(94, 29);
        btnPing.TabIndex = 1;
        btnPing.Text = "Ping";
        btnPing.UseVisualStyleBackColor = true;
        btnPing.Click += btnPing_Click;
        // 
        // btnStop
        // 
        btnStop.Location = new Point(12, 189);
        btnStop.Name = "btnStop";
        btnStop.Size = new Size(94, 29);
        btnStop.TabIndex = 2;
        btnStop.Text = "Stop";
        btnStop.UseVisualStyleBackColor = true;
        btnStop.Click += btnStop_Click;
        // 
        // btnStatus
        // 
        btnStatus.Location = new Point(12, 259);
        btnStatus.Name = "btnStatus";
        btnStatus.Size = new Size(94, 29);
        btnStatus.TabIndex = 1;
        btnStatus.Text = "Status";
        btnStatus.UseVisualStyleBackColor = true;
        btnStatus.Click += btnStatus_Click;
        // 
        // rtbLog
        // 
        rtbLog.Dock = DockStyle.Right;
        rtbLog.Location = new Point(608, 0);
        rtbLog.Name = "rtbLog";
        rtbLog.Size = new Size(192, 450);
        rtbLog.TabIndex = 3;
        rtbLog.Text = "";
        // 
        // rtbStatus
        // 
        rtbStatus.BackColor = SystemColors.ControlLight;
        rtbStatus.Dock = DockStyle.Right;
        rtbStatus.Location = new Point(416, 0);
        rtbStatus.Name = "rtbStatus";
        rtbStatus.Size = new Size(192, 450);
        rtbStatus.TabIndex = 4;
        rtbStatus.Text = "";
        // 
        // Controller
        // 
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(800, 450);
        Controls.Add(rtbStatus);
        Controls.Add(rtbLog);
        Controls.Add(btnStatus);
        Controls.Add(btnPing);
        Controls.Add(btnStop);
        Controls.Add(btnPause);
        Controls.Add(btnStart);
        Name = "Controller";
        Text = "Form1";
        ResumeLayout(false);
    }

    #endregion

    private Button btnStart;
    private Button btnPause;
    private Button btnPing;
    private Button btnStop;
    private Button btnStatus;
    private RichTextBox rtbLog;
    private RichTextBox rtbStatus;
}
