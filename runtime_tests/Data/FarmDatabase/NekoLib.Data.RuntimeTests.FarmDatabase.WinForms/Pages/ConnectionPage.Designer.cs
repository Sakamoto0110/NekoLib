namespace NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Pages
{
    partial class ConnectionPage
    {
        private System.ComponentModel.IContainer components = null;

        private Theme.PageHeader _header;
        private Theme.StatusLine _status;
        private System.Windows.Forms.TableLayoutPanel _layout;
        private Theme.Card _providerCard;
        private System.Windows.Forms.Label _providerLabel;
        private System.Windows.Forms.ComboBox _providerCombo;
        private System.Windows.Forms.CheckBox _recreateCheck;
        private System.Windows.Forms.Label _recreateHint;
        private Theme.FarmButton _connectButton;
        private Theme.FarmButton _disconnectButton;
        private Theme.Card _detailCard;
        private System.Windows.Forms.Label _pathCaption;
        private System.Windows.Forms.Label _pathValue;
        private System.Windows.Forms.Label _connCaption;
        private System.Windows.Forms.TextBox _connValue;
        private System.Windows.Forms.Label _dialectCaption;
        private System.Windows.Forms.Label _dialectValue;
        private System.Windows.Forms.Label _warningValue;

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
            this._header = new Theme.PageHeader();
            this._status = new Theme.StatusLine();
            this._layout = new System.Windows.Forms.TableLayoutPanel();
            this._providerCard = new Theme.Card();
            this._providerLabel = new System.Windows.Forms.Label();
            this._providerCombo = new System.Windows.Forms.ComboBox();
            this._recreateCheck = new System.Windows.Forms.CheckBox();
            this._recreateHint = new System.Windows.Forms.Label();
            this._connectButton = new Theme.FarmButton();
            this._disconnectButton = new Theme.FarmButton();
            this._detailCard = new Theme.Card();
            this._pathCaption = new System.Windows.Forms.Label();
            this._pathValue = new System.Windows.Forms.Label();
            this._connCaption = new System.Windows.Forms.Label();
            this._connValue = new System.Windows.Forms.TextBox();
            this._dialectCaption = new System.Windows.Forms.Label();
            this._dialectValue = new System.Windows.Forms.Label();
            this._warningValue = new System.Windows.Forms.Label();
            this._layout.SuspendLayout();
            this._providerCard.SuspendLayout();
            this._detailCard.SuspendLayout();
            this.SuspendLayout();
            //
            // _header
            //
            this._header.Dock = System.Windows.Forms.DockStyle.Top;
            this._header.Location = new System.Drawing.Point(0, 0);
            this._header.Name = "_header";
            this._header.Size = new System.Drawing.Size(900, 62);
            this._header.Subtitle = "Escolha o motor de banco. Os dois usam o mesmo esquema e o mesmo código.";
            this._header.TabIndex = 0;
            this._header.Text = "Conexão";
            //
            // _status
            //
            this._status.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._status.Location = new System.Drawing.Point(0, 470);
            this._status.Name = "_status";
            this._status.Size = new System.Drawing.Size(900, 30);
            this._status.TabIndex = 2;
            //
            // _layout
            //
            this._layout.ColumnCount = 2;
            this._layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 380F));
            this._layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._layout.Controls.Add(this._providerCard, 0, 0);
            this._layout.Controls.Add(this._detailCard, 1, 0);
            this._layout.Dock = System.Windows.Forms.DockStyle.Fill;
            this._layout.Location = new System.Drawing.Point(0, 62);
            this._layout.Name = "_layout";
            this._layout.RowCount = 1;
            this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._layout.Size = new System.Drawing.Size(900, 408);
            this._layout.TabIndex = 1;
            //
            // _providerCard
            //
            this._providerCard.Controls.Add(this._disconnectButton);
            this._providerCard.Controls.Add(this._connectButton);
            this._providerCard.Controls.Add(this._recreateHint);
            this._providerCard.Controls.Add(this._recreateCheck);
            this._providerCard.Controls.Add(this._providerCombo);
            this._providerCard.Controls.Add(this._providerLabel);
            this._providerCard.Dock = System.Windows.Forms.DockStyle.Fill;
            this._providerCard.Location = new System.Drawing.Point(0, 0);
            this._providerCard.Margin = new System.Windows.Forms.Padding(0, 0, 16, 8);
            this._providerCard.Name = "_providerCard";
            this._providerCard.Size = new System.Drawing.Size(364, 400);
            this._providerCard.Subtitle = "SQLite ou Access";
            this._providerCard.TabIndex = 0;
            this._providerCard.Title = "Provider";
            //
            // _providerLabel
            //
            this._providerLabel.AutoSize = true;
            this._providerLabel.ForeColor = System.Drawing.Color.FromArgb(135, 153, 164);
            this._providerLabel.Location = new System.Drawing.Point(16, 52);
            this._providerLabel.Name = "_providerLabel";
            this._providerLabel.Size = new System.Drawing.Size(50, 15);
            this._providerLabel.TabIndex = 0;
            this._providerLabel.Text = "Motor";
            //
            // _providerCombo
            //
            this._providerCombo.Location = new System.Drawing.Point(16, 72);
            this._providerCombo.Name = "_providerCombo";
            this._providerCombo.Size = new System.Drawing.Size(330, 24);
            this._providerCombo.TabIndex = 1;
            //
            // _recreateCheck
            //
            this._recreateCheck.AutoSize = true;
            this._recreateCheck.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._recreateCheck.ForeColor = System.Drawing.Color.FromArgb(228, 236, 240);
            this._recreateCheck.Location = new System.Drawing.Point(16, 114);
            this._recreateCheck.Name = "_recreateCheck";
            this._recreateCheck.Size = new System.Drawing.Size(210, 19);
            this._recreateCheck.TabIndex = 2;
            this._recreateCheck.Text = "Recriar o banco do zero";
            //
            // _recreateHint
            //
            this._recreateHint.ForeColor = System.Drawing.Color.FromArgb(92, 107, 116);
            this._recreateHint.Location = new System.Drawing.Point(34, 134);
            this._recreateHint.Name = "_recreateHint";
            this._recreateHint.Size = new System.Drawing.Size(312, 44);
            this._recreateHint.TabIndex = 3;
            this._recreateHint.Text = "Apaga o arquivo e refaz esquema e povoamento. Vale só para a próxima conexão.";
            //
            // _connectButton
            //
            this._connectButton.Location = new System.Drawing.Point(16, 190);
            this._connectButton.Name = "_connectButton";
            this._connectButton.Size = new System.Drawing.Size(150, 34);
            this._connectButton.TabIndex = 4;
            this._connectButton.Text = "Conectar";
            //
            // _disconnectButton
            //
            this._disconnectButton.Kind = Theme.FarmButtonKind.Ghost;
            this._disconnectButton.Location = new System.Drawing.Point(176, 190);
            this._disconnectButton.Name = "_disconnectButton";
            this._disconnectButton.Size = new System.Drawing.Size(150, 34);
            this._disconnectButton.TabIndex = 5;
            this._disconnectButton.Text = "Desconectar";
            //
            // _detailCard
            //
            this._detailCard.Controls.Add(this._warningValue);
            this._detailCard.Controls.Add(this._dialectValue);
            this._detailCard.Controls.Add(this._dialectCaption);
            this._detailCard.Controls.Add(this._connValue);
            this._detailCard.Controls.Add(this._connCaption);
            this._detailCard.Controls.Add(this._pathValue);
            this._detailCard.Controls.Add(this._pathCaption);
            this._detailCard.Dock = System.Windows.Forms.DockStyle.Fill;
            this._detailCard.Location = new System.Drawing.Point(380, 0);
            this._detailCard.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            this._detailCard.Name = "_detailCard";
            this._detailCard.Size = new System.Drawing.Size(520, 400);
            this._detailCard.Subtitle = "o que muda entre os motores";
            this._detailCard.TabIndex = 1;
            this._detailCard.Title = "Detalhes";
            //
            // _pathCaption
            //
            this._pathCaption.AutoSize = true;
            this._pathCaption.ForeColor = System.Drawing.Color.FromArgb(135, 153, 164);
            this._pathCaption.Location = new System.Drawing.Point(16, 52);
            this._pathCaption.Name = "_pathCaption";
            this._pathCaption.Size = new System.Drawing.Size(80, 15);
            this._pathCaption.TabIndex = 0;
            this._pathCaption.Text = "Arquivo";
            //
            // _pathValue
            //
            this._pathValue.Font = new System.Drawing.Font("Consolas", 8.25F);
            this._pathValue.ForeColor = System.Drawing.Color.FromArgb(228, 236, 240);
            this._pathValue.Location = new System.Drawing.Point(16, 70);
            this._pathValue.Name = "_pathValue";
            this._pathValue.Size = new System.Drawing.Size(486, 32);
            this._pathValue.TabIndex = 1;
            //
            // _connCaption
            //
            this._connCaption.AutoSize = true;
            this._connCaption.ForeColor = System.Drawing.Color.FromArgb(135, 153, 164);
            this._connCaption.Location = new System.Drawing.Point(16, 110);
            this._connCaption.Name = "_connCaption";
            this._connCaption.Size = new System.Drawing.Size(130, 15);
            this._connCaption.TabIndex = 2;
            this._connCaption.Text = "String de conexão";
            //
            // _connValue
            //
            this._connValue.Location = new System.Drawing.Point(16, 130);
            this._connValue.Multiline = true;
            this._connValue.Name = "_connValue";
            this._connValue.ReadOnly = true;
            this._connValue.Size = new System.Drawing.Size(486, 46);
            this._connValue.TabIndex = 3;
            //
            // _dialectCaption
            //
            this._dialectCaption.AutoSize = true;
            this._dialectCaption.ForeColor = System.Drawing.Color.FromArgb(135, 153, 164);
            this._dialectCaption.Location = new System.Drawing.Point(16, 190);
            this._dialectCaption.Name = "_dialectCaption";
            this._dialectCaption.Size = new System.Drawing.Size(60, 15);
            this._dialectCaption.TabIndex = 4;
            this._dialectCaption.Text = "Dialeto";
            //
            // _dialectValue
            //
            this._dialectValue.ForeColor = System.Drawing.Color.FromArgb(228, 236, 240);
            this._dialectValue.Location = new System.Drawing.Point(16, 210);
            this._dialectValue.Name = "_dialectValue";
            this._dialectValue.Size = new System.Drawing.Size(486, 56);
            this._dialectValue.TabIndex = 5;
            //
            // _warningValue
            //
            this._warningValue.ForeColor = System.Drawing.Color.FromArgb(216, 101, 79);
            this._warningValue.Location = new System.Drawing.Point(16, 276);
            this._warningValue.Name = "_warningValue";
            this._warningValue.Size = new System.Drawing.Size(486, 100);
            this._warningValue.TabIndex = 6;
            //
            // ConnectionPage
            //
            this.Controls.Add(this._layout);
            this.Controls.Add(this._status);
            this.Controls.Add(this._header);
            this.Name = "ConnectionPage";
            this.Size = new System.Drawing.Size(900, 500);
            this._layout.ResumeLayout(false);
            this._providerCard.ResumeLayout(false);
            this._providerCard.PerformLayout();
            this._detailCard.ResumeLayout(false);
            this._detailCard.PerformLayout();
            this.ResumeLayout(false);
        }
    }
}
