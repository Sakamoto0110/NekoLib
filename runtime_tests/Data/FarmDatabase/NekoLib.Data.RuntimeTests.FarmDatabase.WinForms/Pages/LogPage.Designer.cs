namespace NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Pages
{
    partial class LogPage
    {
        private System.ComponentModel.IContainer components = null;

        private Theme.PageHeader _header;
        private Theme.StatusLine _status;
        private Theme.Card _card;
        private System.Windows.Forms.Panel _toolbar;
        private Theme.FarmButton _refreshButton;
        private System.Windows.Forms.Label _summaryLabel;
        private System.Windows.Forms.DataGridView _grid;

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
            this._card = new Theme.Card();
            this._toolbar = new System.Windows.Forms.Panel();
            this._refreshButton = new Theme.FarmButton();
            this._summaryLabel = new System.Windows.Forms.Label();
            this._grid = new System.Windows.Forms.DataGridView();
            this._card.SuspendLayout();
            this._toolbar.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._grid)).BeginInit();
            this.SuspendLayout();
            //
            // _header
            //
            this._header.Dock = System.Windows.Forms.DockStyle.Top;
            this._header.Location = new System.Drawing.Point(0, 0);
            this._header.Name = "_header";
            this._header.Size = new System.Drawing.Size(900, 62);
            this._header.Subtitle = "Cada linha foi gravada na mesma transação da mudança que descreve.";
            this._header.TabIndex = 0;
            this._header.Text = "Log de operações";
            //
            // _status
            //
            this._status.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._status.Location = new System.Drawing.Point(0, 470);
            this._status.Name = "_status";
            this._status.Size = new System.Drawing.Size(900, 30);
            this._status.TabIndex = 2;
            //
            // _card
            //
            this._card.Controls.Add(this._grid);
            this._card.Controls.Add(this._toolbar);
            this._card.Dock = System.Windows.Forms.DockStyle.Fill;
            this._card.Location = new System.Drawing.Point(0, 62);
            this._card.Name = "_card";
            this._card.Padding = new System.Windows.Forms.Padding(12, 44, 12, 12);
            this._card.Size = new System.Drawing.Size(900, 408);
            this._card.TabIndex = 1;
            this._card.Title = "Movimentações";
            //
            // _toolbar
            //
            this._toolbar.Controls.Add(this._summaryLabel);
            this._toolbar.Controls.Add(this._refreshButton);
            this._toolbar.Dock = System.Windows.Forms.DockStyle.Top;
            this._toolbar.Location = new System.Drawing.Point(12, 44);
            this._toolbar.Name = "_toolbar";
            this._toolbar.Size = new System.Drawing.Size(876, 42);
            this._toolbar.TabIndex = 0;
            //
            // _refreshButton
            //
            this._refreshButton.Location = new System.Drawing.Point(2, 6);
            this._refreshButton.Name = "_refreshButton";
            this._refreshButton.Size = new System.Drawing.Size(130, 30);
            this._refreshButton.TabIndex = 0;
            this._refreshButton.Text = "Recarregar";
            //
            // _summaryLabel
            //
            this._summaryLabel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this._summaryLabel.ForeColor = System.Drawing.Color.FromArgb(135, 153, 164);
            this._summaryLabel.Location = new System.Drawing.Point(456, 13);
            this._summaryLabel.Name = "_summaryLabel";
            this._summaryLabel.Size = new System.Drawing.Size(416, 18);
            this._summaryLabel.TabIndex = 1;
            this._summaryLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // _grid
            //
            this._grid.Dock = System.Windows.Forms.DockStyle.Fill;
            this._grid.Location = new System.Drawing.Point(12, 86);
            this._grid.Name = "_grid";
            this._grid.Size = new System.Drawing.Size(876, 310);
            this._grid.TabIndex = 1;
            //
            // LogPage
            //
            this.Controls.Add(this._card);
            this.Controls.Add(this._status);
            this.Controls.Add(this._header);
            this.Name = "LogPage";
            this.Size = new System.Drawing.Size(900, 500);
            this._card.ResumeLayout(false);
            this._toolbar.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._grid)).EndInit();
            this.ResumeLayout(false);
        }
    }
}
