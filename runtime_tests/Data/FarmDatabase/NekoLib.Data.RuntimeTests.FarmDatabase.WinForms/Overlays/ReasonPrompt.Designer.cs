namespace NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Overlays
{
    partial class ReasonPrompt
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.Label _title;
        private System.Windows.Forms.Label _subject;
        private System.Windows.Forms.Label _prompt;
        private System.Windows.Forms.ComboBox _presetCombo;
        private System.Windows.Forms.TextBox _reasonBox;
        private System.Windows.Forms.Label _hint;
        private Theme.FarmButton _confirmButton;
        private Theme.FarmButton _cancelButton;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this._title = new System.Windows.Forms.Label();
            this._subject = new System.Windows.Forms.Label();
            this._prompt = new System.Windows.Forms.Label();
            this._presetCombo = new System.Windows.Forms.ComboBox();
            this._reasonBox = new System.Windows.Forms.TextBox();
            this._hint = new System.Windows.Forms.Label();
            this._confirmButton = new Theme.FarmButton();
            this._cancelButton = new Theme.FarmButton();
            this.SuspendLayout();
            //
            // _title
            //
            this._title.AutoSize = true;
            this._title.Font = new System.Drawing.Font("Segoe UI Semibold", 12F);
            this._title.ForeColor = System.Drawing.Color.FromArgb(228, 236, 240);
            this._title.Location = new System.Drawing.Point(24, 22);
            this._title.Name = "_title";
            this._title.Size = new System.Drawing.Size(220, 21);
            this._title.TabIndex = 0;
            this._title.Text = "Remover do rebanho";
            //
            // _subject
            //
            this._subject.ForeColor = System.Drawing.Color.FromArgb(216, 101, 79);
            this._subject.Location = new System.Drawing.Point(26, 50);
            this._subject.Name = "_subject";
            this._subject.Size = new System.Drawing.Size(410, 20);
            this._subject.TabIndex = 1;
            //
            // _prompt
            //
            this._prompt.AutoSize = true;
            this._prompt.ForeColor = System.Drawing.Color.FromArgb(135, 153, 164);
            this._prompt.Location = new System.Drawing.Point(26, 84);
            this._prompt.Name = "_prompt";
            this._prompt.Size = new System.Drawing.Size(150, 15);
            this._prompt.TabIndex = 2;
            this._prompt.Text = "Motivo da remoção";
            //
            // _presetCombo
            //
            this._presetCombo.Location = new System.Drawing.Point(26, 104);
            this._presetCombo.Name = "_presetCombo";
            this._presetCombo.Size = new System.Drawing.Size(410, 24);
            this._presetCombo.TabIndex = 3;
            //
            // _reasonBox
            //
            this._reasonBox.Location = new System.Drawing.Point(26, 138);
            this._reasonBox.Multiline = true;
            this._reasonBox.Name = "_reasonBox";
            this._reasonBox.Size = new System.Drawing.Size(410, 62);
            this._reasonBox.TabIndex = 4;
            //
            // _hint
            //
            this._hint.ForeColor = System.Drawing.Color.FromArgb(92, 107, 116);
            this._hint.Location = new System.Drawing.Point(26, 204);
            this._hint.Name = "_hint";
            this._hint.Size = new System.Drawing.Size(410, 18);
            this._hint.TabIndex = 5;
            this._hint.Text = "O motivo vai para o log de operações, junto com a remoção.";
            //
            // _cancelButton
            //
            this._cancelButton.Kind = Theme.FarmButtonKind.Ghost;
            this._cancelButton.Location = new System.Drawing.Point(196, 234);
            this._cancelButton.Name = "_cancelButton";
            this._cancelButton.Size = new System.Drawing.Size(114, 34);
            this._cancelButton.TabIndex = 6;
            this._cancelButton.Text = "Cancelar";
            //
            // _confirmButton
            //
            this._confirmButton.Kind = Theme.FarmButtonKind.Danger;
            this._confirmButton.Location = new System.Drawing.Point(320, 234);
            this._confirmButton.Name = "_confirmButton";
            this._confirmButton.Size = new System.Drawing.Size(116, 34);
            this._confirmButton.TabIndex = 7;
            this._confirmButton.Text = "Remover";
            //
            // ReasonPrompt
            //
            this.BackColor = System.Drawing.Color.FromArgb(26, 33, 38);
            this.Controls.Add(this._confirmButton);
            this.Controls.Add(this._cancelButton);
            this.Controls.Add(this._hint);
            this.Controls.Add(this._reasonBox);
            this.Controls.Add(this._presetCombo);
            this.Controls.Add(this._prompt);
            this.Controls.Add(this._subject);
            this.Controls.Add(this._title);
            this.Name = "ReasonPrompt";
            this.Size = new System.Drawing.Size(462, 292);
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
