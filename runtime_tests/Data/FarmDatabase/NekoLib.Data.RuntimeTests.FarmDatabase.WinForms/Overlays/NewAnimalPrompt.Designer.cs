namespace NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Overlays
{
    partial class NewAnimalPrompt
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.Label _title;
        private System.Windows.Forms.Label _subtitle;
        private System.Windows.Forms.Label _speciesCaption;
        private System.Windows.Forms.ComboBox _speciesCombo;
        private System.Windows.Forms.Label _genderCaption;
        private System.Windows.Forms.ComboBox _genderCombo;
        private System.Windows.Forms.Label _ageCaption;
        private System.Windows.Forms.NumericUpDown _ageValue;
        private System.Windows.Forms.Label _notesCaption;
        private System.Windows.Forms.TextBox _notesBox;
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
            this._subtitle = new System.Windows.Forms.Label();
            this._speciesCaption = new System.Windows.Forms.Label();
            this._speciesCombo = new System.Windows.Forms.ComboBox();
            this._genderCaption = new System.Windows.Forms.Label();
            this._genderCombo = new System.Windows.Forms.ComboBox();
            this._ageCaption = new System.Windows.Forms.Label();
            this._ageValue = new System.Windows.Forms.NumericUpDown();
            this._notesCaption = new System.Windows.Forms.Label();
            this._notesBox = new System.Windows.Forms.TextBox();
            this._confirmButton = new Theme.FarmButton();
            this._cancelButton = new Theme.FarmButton();
            ((System.ComponentModel.ISupportInitialize)(this._ageValue)).BeginInit();
            this.SuspendLayout();
            //
            // _title
            //
            this._title.AutoSize = true;
            this._title.Font = new System.Drawing.Font("Segoe UI Semibold", 12F);
            this._title.ForeColor = System.Drawing.Color.FromArgb(228, 236, 240);
            this._title.Location = new System.Drawing.Point(24, 22);
            this._title.Name = "_title";
            this._title.Size = new System.Drawing.Size(200, 21);
            this._title.TabIndex = 0;
            this._title.Text = "Registrar no rebanho";
            //
            // _subtitle
            //
            this._subtitle.ForeColor = System.Drawing.Color.FromArgb(95, 179, 122);
            this._subtitle.Location = new System.Drawing.Point(26, 50);
            this._subtitle.Name = "_subtitle";
            this._subtitle.Size = new System.Drawing.Size(410, 20);
            this._subtitle.TabIndex = 1;
            this._subtitle.Text = "O brinco é atribuído pelo banco.";
            //
            // _speciesCaption
            //
            this._speciesCaption.AutoSize = true;
            this._speciesCaption.ForeColor = System.Drawing.Color.FromArgb(135, 153, 164);
            this._speciesCaption.Location = new System.Drawing.Point(26, 84);
            this._speciesCaption.Name = "_speciesCaption";
            this._speciesCaption.Size = new System.Drawing.Size(60, 15);
            this._speciesCaption.TabIndex = 2;
            this._speciesCaption.Text = "Espécie";
            //
            // _speciesCombo
            //
            this._speciesCombo.Location = new System.Drawing.Point(26, 104);
            this._speciesCombo.Name = "_speciesCombo";
            this._speciesCombo.Size = new System.Drawing.Size(196, 24);
            this._speciesCombo.TabIndex = 3;
            //
            // _genderCaption
            //
            this._genderCaption.AutoSize = true;
            this._genderCaption.ForeColor = System.Drawing.Color.FromArgb(135, 153, 164);
            this._genderCaption.Location = new System.Drawing.Point(240, 84);
            this._genderCaption.Name = "_genderCaption";
            this._genderCaption.Size = new System.Drawing.Size(60, 15);
            this._genderCaption.TabIndex = 4;
            this._genderCaption.Text = "Gênero";
            //
            // _genderCombo
            //
            this._genderCombo.Location = new System.Drawing.Point(240, 104);
            this._genderCombo.Name = "_genderCombo";
            this._genderCombo.Size = new System.Drawing.Size(120, 24);
            this._genderCombo.TabIndex = 5;
            //
            // _ageCaption
            //
            this._ageCaption.AutoSize = true;
            this._ageCaption.ForeColor = System.Drawing.Color.FromArgb(135, 153, 164);
            this._ageCaption.Location = new System.Drawing.Point(378, 84);
            this._ageCaption.Name = "_ageCaption";
            this._ageCaption.Size = new System.Drawing.Size(40, 15);
            this._ageCaption.TabIndex = 6;
            this._ageCaption.Text = "Idade";
            //
            // _ageValue
            //
            this._ageValue.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this._ageValue.Location = new System.Drawing.Point(378, 104);
            this._ageValue.Maximum = new decimal(new int[] { 40, 0, 0, 0 });
            this._ageValue.Name = "_ageValue";
            this._ageValue.Size = new System.Drawing.Size(58, 23);
            this._ageValue.TabIndex = 7;
            this._ageValue.Value = new decimal(new int[] { 1, 0, 0, 0 });
            //
            // _notesCaption
            //
            this._notesCaption.AutoSize = true;
            this._notesCaption.ForeColor = System.Drawing.Color.FromArgb(135, 153, 164);
            this._notesCaption.Location = new System.Drawing.Point(26, 140);
            this._notesCaption.Name = "_notesCaption";
            this._notesCaption.Size = new System.Drawing.Size(150, 15);
            this._notesCaption.TabIndex = 8;
            this._notesCaption.Text = "Observação (opcional)";
            //
            // _notesBox
            //
            this._notesBox.Location = new System.Drawing.Point(26, 160);
            this._notesBox.Multiline = true;
            this._notesBox.Name = "_notesBox";
            this._notesBox.Size = new System.Drawing.Size(410, 48);
            this._notesBox.TabIndex = 9;
            //
            // _cancelButton
            //
            this._cancelButton.Kind = Theme.FarmButtonKind.Ghost;
            this._cancelButton.Location = new System.Drawing.Point(196, 226);
            this._cancelButton.Name = "_cancelButton";
            this._cancelButton.Size = new System.Drawing.Size(114, 34);
            this._cancelButton.TabIndex = 10;
            this._cancelButton.Text = "Cancelar";
            //
            // _confirmButton
            //
            this._confirmButton.Glyph = "＋";
            this._confirmButton.Location = new System.Drawing.Point(320, 226);
            this._confirmButton.Name = "_confirmButton";
            this._confirmButton.Size = new System.Drawing.Size(116, 34);
            this._confirmButton.TabIndex = 11;
            this._confirmButton.Text = "Registrar";
            //
            // NewAnimalPrompt
            //
            this.BackColor = System.Drawing.Color.FromArgb(26, 33, 38);
            this.Controls.Add(this._confirmButton);
            this.Controls.Add(this._cancelButton);
            this.Controls.Add(this._notesBox);
            this.Controls.Add(this._notesCaption);
            this.Controls.Add(this._ageValue);
            this.Controls.Add(this._ageCaption);
            this.Controls.Add(this._genderCombo);
            this.Controls.Add(this._genderCaption);
            this.Controls.Add(this._speciesCombo);
            this.Controls.Add(this._speciesCaption);
            this.Controls.Add(this._subtitle);
            this.Controls.Add(this._title);
            this.Name = "NewAnimalPrompt";
            this.Size = new System.Drawing.Size(462, 284);
            ((System.ComponentModel.ISupportInitialize)(this._ageValue)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
