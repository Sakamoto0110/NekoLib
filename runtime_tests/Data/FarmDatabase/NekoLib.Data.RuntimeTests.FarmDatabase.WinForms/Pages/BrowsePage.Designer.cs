namespace NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Pages
{
    partial class BrowsePage
    {
        private System.ComponentModel.IContainer components = null;

        private Theme.PageHeader _header;
        private Theme.StatusLine _status;
        private System.Windows.Forms.TableLayoutPanel _layout;
        private Theme.Card _tablesCard;
        private System.Windows.Forms.ListBox _tablesList;
        private Theme.FarmButton _reloadTablesButton;
        private Theme.Card _dataCard;
        private System.Windows.Forms.Panel _toolbar;
        private System.Windows.Forms.CheckBox _limitCheck;
        private System.Windows.Forms.NumericUpDown _limitValue;
        private Theme.FarmButton _loadRowsButton;
        private System.Windows.Forms.Label _rowCountLabel;
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
            this._layout = new System.Windows.Forms.TableLayoutPanel();
            this._tablesCard = new Theme.Card();
            this._tablesList = new System.Windows.Forms.ListBox();
            this._reloadTablesButton = new Theme.FarmButton();
            this._dataCard = new Theme.Card();
            this._toolbar = new System.Windows.Forms.Panel();
            this._limitCheck = new System.Windows.Forms.CheckBox();
            this._limitValue = new System.Windows.Forms.NumericUpDown();
            this._loadRowsButton = new Theme.FarmButton();
            this._rowCountLabel = new System.Windows.Forms.Label();
            this._grid = new System.Windows.Forms.DataGridView();
            this._layout.SuspendLayout();
            this._tablesCard.SuspendLayout();
            this._dataCard.SuspendLayout();
            this._toolbar.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._limitValue)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._grid)).BeginInit();
            this.SuspendLayout();
            //
            // _header
            //
            this._header.Dock = System.Windows.Forms.DockStyle.Top;
            this._header.Location = new System.Drawing.Point(0, 0);
            this._header.Name = "_header";
            this._header.Size = new System.Drawing.Size(900, 62);
            this._header.Subtitle = "O catálogo vem do motor: SQLite consulta sqlite_master, Access usa o schema rowset do OleDb.";
            this._header.TabIndex = 0;
            this._header.Text = "Tabelas";
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
            this._layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 260F));
            this._layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._layout.Controls.Add(this._tablesCard, 0, 0);
            this._layout.Controls.Add(this._dataCard, 1, 0);
            this._layout.Dock = System.Windows.Forms.DockStyle.Fill;
            this._layout.Location = new System.Drawing.Point(0, 62);
            this._layout.Name = "_layout";
            this._layout.RowCount = 1;
            this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._layout.Size = new System.Drawing.Size(900, 408);
            this._layout.TabIndex = 1;
            //
            // _tablesCard
            //
            this._tablesCard.Controls.Add(this._tablesList);
            this._tablesCard.Controls.Add(this._reloadTablesButton);
            this._tablesCard.Dock = System.Windows.Forms.DockStyle.Fill;
            this._tablesCard.Location = new System.Drawing.Point(0, 0);
            this._tablesCard.Margin = new System.Windows.Forms.Padding(0, 0, 16, 8);
            this._tablesCard.Name = "_tablesCard";
            this._tablesCard.Padding = new System.Windows.Forms.Padding(12, 44, 12, 56);
            this._tablesCard.Size = new System.Drawing.Size(244, 400);
            this._tablesCard.TabIndex = 0;
            this._tablesCard.Title = "Catálogo";
            //
            // _tablesList
            //
            this._tablesList.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this._tablesList.Dock = System.Windows.Forms.DockStyle.Fill;
            this._tablesList.IntegralHeight = false;
            this._tablesList.ItemHeight = 15;
            this._tablesList.Location = new System.Drawing.Point(12, 44);
            this._tablesList.Name = "_tablesList";
            this._tablesList.Size = new System.Drawing.Size(220, 300);
            this._tablesList.TabIndex = 0;
            //
            // _reloadTablesButton
            //
            this._reloadTablesButton.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._reloadTablesButton.Kind = Theme.FarmButtonKind.Ghost;
            this._reloadTablesButton.Location = new System.Drawing.Point(12, 356);
            this._reloadTablesButton.Name = "_reloadTablesButton";
            this._reloadTablesButton.Size = new System.Drawing.Size(220, 32);
            this._reloadTablesButton.TabIndex = 1;
            this._reloadTablesButton.Text = "Recarregar catálogo";
            //
            // _dataCard
            //
            this._dataCard.Controls.Add(this._grid);
            this._dataCard.Controls.Add(this._toolbar);
            this._dataCard.Dock = System.Windows.Forms.DockStyle.Fill;
            this._dataCard.Location = new System.Drawing.Point(260, 0);
            this._dataCard.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            this._dataCard.Name = "_dataCard";
            this._dataCard.Padding = new System.Windows.Forms.Padding(12, 44, 12, 12);
            this._dataCard.Size = new System.Drawing.Size(640, 400);
            this._dataCard.TabIndex = 1;
            this._dataCard.Title = "Conteúdo";
            //
            // _toolbar
            //
            this._toolbar.Controls.Add(this._rowCountLabel);
            this._toolbar.Controls.Add(this._loadRowsButton);
            this._toolbar.Controls.Add(this._limitValue);
            this._toolbar.Controls.Add(this._limitCheck);
            this._toolbar.Dock = System.Windows.Forms.DockStyle.Top;
            this._toolbar.Location = new System.Drawing.Point(12, 44);
            this._toolbar.Name = "_toolbar";
            this._toolbar.Size = new System.Drawing.Size(616, 42);
            this._toolbar.TabIndex = 0;
            //
            // _limitCheck
            //
            this._limitCheck.AutoSize = true;
            this._limitCheck.Checked = true;
            this._limitCheck.CheckState = System.Windows.Forms.CheckState.Checked;
            this._limitCheck.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._limitCheck.ForeColor = System.Drawing.Color.FromArgb(228, 236, 240);
            this._limitCheck.Location = new System.Drawing.Point(2, 10);
            this._limitCheck.Name = "_limitCheck";
            this._limitCheck.Size = new System.Drawing.Size(80, 19);
            this._limitCheck.TabIndex = 0;
            this._limitCheck.Text = "Top";
            //
            // _limitValue
            //
            this._limitValue.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this._limitValue.Location = new System.Drawing.Point(66, 9);
            this._limitValue.Maximum = new decimal(new int[] { 100000, 0, 0, 0 });
            this._limitValue.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this._limitValue.Name = "_limitValue";
            this._limitValue.Size = new System.Drawing.Size(74, 23);
            this._limitValue.TabIndex = 1;
            this._limitValue.Value = new decimal(new int[] { 100, 0, 0, 0 });
            //
            // _loadRowsButton
            //
            this._loadRowsButton.Location = new System.Drawing.Point(152, 6);
            this._loadRowsButton.Name = "_loadRowsButton";
            this._loadRowsButton.Size = new System.Drawing.Size(120, 30);
            this._loadRowsButton.TabIndex = 2;
            this._loadRowsButton.Text = "Ler tabela";
            //
            // _rowCountLabel
            //
            this._rowCountLabel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            this._rowCountLabel.ForeColor = System.Drawing.Color.FromArgb(135, 153, 164);
            this._rowCountLabel.Location = new System.Drawing.Point(346, 13);
            this._rowCountLabel.Name = "_rowCountLabel";
            this._rowCountLabel.Size = new System.Drawing.Size(266, 18);
            this._rowCountLabel.TabIndex = 3;
            this._rowCountLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // _grid
            //
            this._grid.Dock = System.Windows.Forms.DockStyle.Fill;
            this._grid.Location = new System.Drawing.Point(12, 86);
            this._grid.Name = "_grid";
            this._grid.Size = new System.Drawing.Size(616, 302);
            this._grid.TabIndex = 1;
            //
            // BrowsePage
            //
            this.Controls.Add(this._layout);
            this.Controls.Add(this._status);
            this.Controls.Add(this._header);
            this.Name = "BrowsePage";
            this.Size = new System.Drawing.Size(900, 500);
            this._layout.ResumeLayout(false);
            this._tablesCard.ResumeLayout(false);
            this._dataCard.ResumeLayout(false);
            this._toolbar.ResumeLayout(false);
            this._toolbar.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._limitValue)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._grid)).EndInit();
            this.ResumeLayout(false);
        }
    }
}
