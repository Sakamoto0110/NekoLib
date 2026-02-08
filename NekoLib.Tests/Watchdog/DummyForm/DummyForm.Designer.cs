namespace NekoLib.Tests.Watchdog;

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
        btnExit1.Location = new Point(66, 86);
        btnExit1.Name = "btnExit1";
        btnExit1.Size = new Size(94, 29);
        btnExit1.TabIndex = 0;
        btnExit1.Text = "Exit(1)";
        btnExit1.UseVisualStyleBackColor = true;
        btnExit1.Click += btnExit1_Click;
        // 
        // btnCrash
        // 
        btnCrash.Location = new Point(66, 137);
        btnCrash.Name = "btnCrash";
        btnCrash.RightToLeft = RightToLeft.No;
        btnCrash.Size = new Size(94, 29);
        btnCrash.TabIndex = 0;
        btnCrash.Text = "Crash";
        btnCrash.UseVisualStyleBackColor = true;
        btnCrash.Click += btnCrash_Click;
        // 
        // DummyForm
        // 
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(231, 242);
        Controls.Add(btnCrash);
        Controls.Add(btnExit1);
        Controls.Add(btnExit0);
        Name = "DummyForm";
        Text = "Form1";
        ResumeLayout(false);
    }

    #endregion

    private Button btnExit0;
    private Button btnExit1;
    private Button btnCrash;
}
