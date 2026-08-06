namespace NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Pages
{
    partial class StockPage
    {
        private System.ComponentModel.IContainer components = null;

        private Theme.PageHeader _header;
        private Theme.StatusLine _status;
        private System.Windows.Forms.TableLayoutPanel _layout;

        private Theme.Card _productCard;
        private System.Windows.Forms.DataGridView _productGrid;
        private System.Windows.Forms.Panel _productBar;
        private System.Windows.Forms.Label _deltaCaption;
        private System.Windows.Forms.NumericUpDown _deltaValue;
        private Theme.FarmButton _addButton;
        private Theme.FarmButton _removeButton;
        private System.Windows.Forms.Label _productSummary;

        private Theme.Card _animalCard;
        private System.Windows.Forms.DataGridView _animalGrid;
        private System.Windows.Forms.Panel _animalBar;
        private Theme.FarmButton _removeAnimalButton;
        private System.Windows.Forms.Label _animalSummary;

        private Theme.FarmButton _refreshButton;
        private System.Windows.Forms.FlowLayoutPanel _headerActions;

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
            this._refreshButton = new Theme.FarmButton();
            this._headerActions = new System.Windows.Forms.FlowLayoutPanel();
            this._layout = new System.Windows.Forms.TableLayoutPanel();
            this._productCard = new Theme.Card();
            this._productGrid = new System.Windows.Forms.DataGridView();
            this._productBar = new System.Windows.Forms.Panel();
            this._deltaCaption = new System.Windows.Forms.Label();
            this._deltaValue = new System.Windows.Forms.NumericUpDown();
            this._addButton = new Theme.FarmButton();
            this._removeButton = new Theme.FarmButton();
            this._productSummary = new System.Windows.Forms.Label();
            this._animalCard = new Theme.Card();
            this._animalGrid = new System.Windows.Forms.DataGridView();
            this._animalBar = new System.Windows.Forms.Panel();
            this._removeAnimalButton = new Theme.FarmButton();
            this._animalSummary = new System.Windows.Forms.Label();
            this._headerActions.SuspendLayout();
            this._layout.SuspendLayout();
            this._productCard.SuspendLayout();
            this._productBar.SuspendLayout();
            this._animalCard.SuspendLayout();
            this._animalBar.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._deltaValue)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._productGrid)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._animalGrid)).BeginInit();
            this.SuspendLayout();
            //
            // _header
            //
            this._header.Controls.Add(this._headerActions);
            this._header.Dock = System.Windows.Forms.DockStyle.Top;
            this._header.Location = new System.Drawing.Point(0, 0);
            this._header.Name = "_header";
            this._header.Size = new System.Drawing.Size(900, 62);
            this._header.Subtitle = "Movimentar estoque grava a mudança e o log numa transação só.";
            this._header.TabIndex = 0;
            this._header.Text = "Controle de estoque";
            //
            // _headerActions
            //
            // Right-docked flow instead of an anchored button, for the same reason as
            // the query editor's bar: anchoring stores a delta, docking recomputes.
            this._headerActions.Controls.Add(this._refreshButton);
            this._headerActions.Dock = System.Windows.Forms.DockStyle.Right;
            this._headerActions.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this._headerActions.Location = new System.Drawing.Point(760, 0);
            this._headerActions.Name = "_headerActions";
            this._headerActions.Size = new System.Drawing.Size(140, 62);
            this._headerActions.TabIndex = 0;
            this._headerActions.WrapContents = false;
            //
            // _refreshButton
            //
            this._refreshButton.Kind = Theme.FarmButtonKind.Ghost;
            this._refreshButton.Margin = new System.Windows.Forms.Padding(0, 14, 0, 0);
            this._refreshButton.Name = "_refreshButton";
            this._refreshButton.Size = new System.Drawing.Size(126, 32);
            this._refreshButton.TabIndex = 0;
            this._refreshButton.Text = "Recarregar";
            //
            // _status
            //
            this._status.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._status.Location = new System.Drawing.Point(0, 470);
            this._status.Name = "_status";
            this._status.Size = new System.Drawing.Size(900, 30);
            this._status.TabIndex = 3;
            //
            // _layout
            //
            this._layout.ColumnCount = 2;
            this._layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 56F));
            this._layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 44F));
            this._layout.Controls.Add(this._productCard, 0, 0);
            this._layout.Controls.Add(this._animalCard, 1, 0);
            this._layout.Dock = System.Windows.Forms.DockStyle.Fill;
            this._layout.Location = new System.Drawing.Point(0, 62);
            this._layout.Name = "_layout";
            this._layout.RowCount = 1;
            this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._layout.Size = new System.Drawing.Size(900, 408);
            this._layout.TabIndex = 2;
            //
            // _productCard
            //
            this._productCard.Controls.Add(this._productGrid);
            this._productCard.Controls.Add(this._productBar);
            this._productCard.Dock = System.Windows.Forms.DockStyle.Fill;
            this._productCard.Location = new System.Drawing.Point(0, 0);
            this._productCard.Margin = new System.Windows.Forms.Padding(0, 0, 14, 8);
            this._productCard.Name = "_productCard";
            this._productCard.Padding = new System.Windows.Forms.Padding(12, 44, 12, 12);
            this._productCard.Size = new System.Drawing.Size(490, 400);
            this._productCard.Subtitle = "frutas, verduras e legumes";
            this._productCard.TabIndex = 0;
            this._productCard.Title = "Produtos";
            //
            // _productGrid
            //
            this._productGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this._productGrid.Location = new System.Drawing.Point(12, 44);
            this._productGrid.Name = "_productGrid";
            this._productGrid.Size = new System.Drawing.Size(466, 250);
            this._productGrid.TabIndex = 0;
            //
            // _productBar
            //
            this._productBar.Controls.Add(this._productSummary);
            this._productBar.Controls.Add(this._removeButton);
            this._productBar.Controls.Add(this._addButton);
            this._productBar.Controls.Add(this._deltaValue);
            this._productBar.Controls.Add(this._deltaCaption);
            this._productBar.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._productBar.Location = new System.Drawing.Point(12, 294);
            this._productBar.Name = "_productBar";
            this._productBar.Size = new System.Drawing.Size(466, 94);
            this._productBar.TabIndex = 1;
            //
            // _productSummary
            //
            this._productSummary.Dock = System.Windows.Forms.DockStyle.Top;
            this._productSummary.ForeColor = System.Drawing.Color.FromArgb(135, 153, 164);
            this._productSummary.Location = new System.Drawing.Point(0, 0);
            this._productSummary.Name = "_productSummary";
            this._productSummary.Size = new System.Drawing.Size(466, 34);
            this._productSummary.TabIndex = 0;
            this._productSummary.Text = "Nenhum produto selecionado";
            //
            // _deltaCaption
            //
            this._deltaCaption.AutoSize = true;
            this._deltaCaption.ForeColor = System.Drawing.Color.FromArgb(135, 153, 164);
            this._deltaCaption.Location = new System.Drawing.Point(2, 47);
            this._deltaCaption.Name = "_deltaCaption";
            this._deltaCaption.Size = new System.Drawing.Size(70, 15);
            this._deltaCaption.TabIndex = 1;
            this._deltaCaption.Text = "Quantidade";
            //
            // _deltaValue
            //
            this._deltaValue.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this._deltaValue.Location = new System.Drawing.Point(80, 44);
            this._deltaValue.Maximum = new decimal(new int[] { 100000, 0, 0, 0 });
            this._deltaValue.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this._deltaValue.Name = "_deltaValue";
            this._deltaValue.Size = new System.Drawing.Size(78, 23);
            this._deltaValue.TabIndex = 2;
            this._deltaValue.Value = new decimal(new int[] { 10, 0, 0, 0 });
            //
            // _addButton
            //
            this._addButton.Glyph = "＋";
            this._addButton.Location = new System.Drawing.Point(172, 41);
            this._addButton.Name = "_addButton";
            this._addButton.Size = new System.Drawing.Size(130, 30);
            this._addButton.TabIndex = 3;
            this._addButton.Text = "Entrada";
            //
            // _removeButton
            //
            this._removeButton.Glyph = "−";
            this._removeButton.Kind = Theme.FarmButtonKind.Ghost;
            this._removeButton.Location = new System.Drawing.Point(310, 41);
            this._removeButton.Name = "_removeButton";
            this._removeButton.Size = new System.Drawing.Size(130, 30);
            this._removeButton.TabIndex = 4;
            this._removeButton.Text = "Saída";
            //
            // _animalCard
            //
            this._animalCard.Controls.Add(this._animalGrid);
            this._animalCard.Controls.Add(this._animalBar);
            this._animalCard.Dock = System.Windows.Forms.DockStyle.Fill;
            this._animalCard.Location = new System.Drawing.Point(504, 0);
            this._animalCard.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            this._animalCard.Name = "_animalCard";
            this._animalCard.Padding = new System.Windows.Forms.Padding(12, 44, 12, 12);
            this._animalCard.Size = new System.Drawing.Size(396, 400);
            this._animalCard.Subtitle = "remoção exige motivo";
            this._animalCard.TabIndex = 1;
            this._animalCard.Title = "Rebanho";
            //
            // _animalGrid
            //
            this._animalGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this._animalGrid.Location = new System.Drawing.Point(12, 44);
            this._animalGrid.Name = "_animalGrid";
            this._animalGrid.Size = new System.Drawing.Size(372, 250);
            this._animalGrid.TabIndex = 0;
            //
            // _animalBar
            //
            this._animalBar.Controls.Add(this._removeAnimalButton);
            this._animalBar.Controls.Add(this._animalSummary);
            this._animalBar.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._animalBar.Location = new System.Drawing.Point(12, 294);
            this._animalBar.Name = "_animalBar";
            this._animalBar.Size = new System.Drawing.Size(372, 94);
            this._animalBar.TabIndex = 1;
            //
            // _animalSummary
            //
            this._animalSummary.Dock = System.Windows.Forms.DockStyle.Top;
            this._animalSummary.ForeColor = System.Drawing.Color.FromArgb(135, 153, 164);
            this._animalSummary.Location = new System.Drawing.Point(0, 0);
            this._animalSummary.Name = "_animalSummary";
            this._animalSummary.Size = new System.Drawing.Size(372, 34);
            this._animalSummary.TabIndex = 0;
            this._animalSummary.Text = "Nenhum animal selecionado";
            //
            // _removeAnimalButton
            //
            this._removeAnimalButton.Glyph = "✖";
            this._removeAnimalButton.Kind = Theme.FarmButtonKind.Danger;
            this._removeAnimalButton.Location = new System.Drawing.Point(2, 41);
            this._removeAnimalButton.Name = "_removeAnimalButton";
            this._removeAnimalButton.Size = new System.Drawing.Size(220, 30);
            this._removeAnimalButton.TabIndex = 1;
            this._removeAnimalButton.Text = "Remover do rebanho";
            //
            // StockPage
            //
            this.Controls.Add(this._layout);
            this.Controls.Add(this._status);
            this.Controls.Add(this._header);
            this.Name = "StockPage";
            this.Size = new System.Drawing.Size(900, 500);
            this._headerActions.ResumeLayout(false);
            this._layout.ResumeLayout(false);
            this._productCard.ResumeLayout(false);
            this._productBar.ResumeLayout(false);
            this._productBar.PerformLayout();
            this._animalCard.ResumeLayout(false);
            this._animalBar.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._deltaValue)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._productGrid)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._animalGrid)).EndInit();
            this.ResumeLayout(false);
        }
    }
}
