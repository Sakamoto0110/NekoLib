namespace NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Pages
{
    partial class RawQueryPage
    {
        private System.ComponentModel.IContainer components = null;

        private Theme.PageHeader _header;
        private Theme.StatusLine _status;
        private System.Windows.Forms.TableLayoutPanel _layout;
        private Theme.Card _editorCard;
        private System.Windows.Forms.TextBox _sqlBox;
        private System.Windows.Forms.Panel _editorBar;
        private System.Windows.Forms.FlowLayoutPanel _editorButtons;
        private System.Windows.Forms.ComboBox _samplesCombo;
        private Theme.FarmButton _runButton;
        private Theme.FarmButton _clearButton;
        private Theme.Card _resultCard;
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
            this._editorCard = new Theme.Card();
            this._sqlBox = new System.Windows.Forms.TextBox();
            this._editorBar = new System.Windows.Forms.Panel();
            this._editorButtons = new System.Windows.Forms.FlowLayoutPanel();
            this._samplesCombo = new System.Windows.Forms.ComboBox();
            this._runButton = new Theme.FarmButton();
            this._clearButton = new Theme.FarmButton();
            this._resultCard = new Theme.Card();
            this._grid = new System.Windows.Forms.DataGridView();
            this._layout.SuspendLayout();
            this._editorCard.SuspendLayout();
            this._editorBar.SuspendLayout();
            this._editorButtons.SuspendLayout();
            this._resultCard.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._grid)).BeginInit();
            this.SuspendLayout();
            //
            // _header
            //
            this._header.Dock = System.Windows.Forms.DockStyle.Top;
            this._header.Location = new System.Drawing.Point(0, 0);
            this._header.Name = "_header";
            this._header.Size = new System.Drawing.Size(900, 62);
            this._header.Subtitle = "SQL passa direto, sem builder e sem translator. É aqui que os dialetos divergem.";
            this._header.TabIndex = 0;
            this._header.Text = "Consulta livre";
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
            // A two-row TableLayoutPanel rather than a SplitContainer. The split was
            // the only container in this app that a page did not share with the
            // others, and it was also the only page whose buttons stopped receiving
            // mouse input once the shell was maximized.
            this._layout.ColumnCount = 1;
            this._layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._layout.Controls.Add(this._editorCard, 0, 0);
            this._layout.Controls.Add(this._resultCard, 0, 1);
            this._layout.Dock = System.Windows.Forms.DockStyle.Fill;
            this._layout.Location = new System.Drawing.Point(0, 62);
            this._layout.Name = "_layout";
            this._layout.RowCount = 2;
            this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 200F));
            this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._layout.Size = new System.Drawing.Size(900, 408);
            this._layout.TabIndex = 1;
            //
            // _editorCard
            //
            this._editorCard.Controls.Add(this._sqlBox);
            this._editorCard.Controls.Add(this._editorBar);
            this._editorCard.Dock = System.Windows.Forms.DockStyle.Fill;
            this._editorCard.Location = new System.Drawing.Point(0, 0);
            this._editorCard.Margin = new System.Windows.Forms.Padding(0, 0, 0, 12);
            this._editorCard.Name = "_editorCard";
            this._editorCard.Padding = new System.Windows.Forms.Padding(12, 44, 12, 12);
            this._editorCard.Size = new System.Drawing.Size(900, 188);
            this._editorCard.Subtitle = "executada exatamente como escrita";
            this._editorCard.TabIndex = 0;
            this._editorCard.Title = "Instrução";
            //
            // _sqlBox
            //
            this._sqlBox.AcceptsReturn = true;
            this._sqlBox.AcceptsTab = true;
            this._sqlBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this._sqlBox.Location = new System.Drawing.Point(12, 82);
            this._sqlBox.Multiline = true;
            this._sqlBox.Name = "_sqlBox";
            this._sqlBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this._sqlBox.Size = new System.Drawing.Size(876, 94);
            this._sqlBox.TabIndex = 0;
            //
            // _editorBar
            //
            // Docked Top, matching the toolbar/grid arrangement BrowsePage uses. As
            // Dock=Bottom - underneath a Dock=Fill multiline TextBox that sits in
            // front of it in the z-order - this bar rendered correctly but stopped
            // receiving mouse input once the shell was maximized.
            this._editorBar.Controls.Add(this._samplesCombo);
            this._editorBar.Controls.Add(this._editorButtons);
            this._editorBar.Dock = System.Windows.Forms.DockStyle.Top;
            this._editorBar.Location = new System.Drawing.Point(12, 44);
            this._editorBar.Name = "_editorBar";
            this._editorBar.Size = new System.Drawing.Size(876, 38);
            this._editorBar.TabIndex = 1;
            //
            // _editorButtons
            //
            this._editorButtons.Controls.Add(this._runButton);
            this._editorButtons.Controls.Add(this._clearButton);
            this._editorButtons.Dock = System.Windows.Forms.DockStyle.Right;
            this._editorButtons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this._editorButtons.Location = new System.Drawing.Point(608, 0);
            this._editorButtons.Name = "_editorButtons";
            this._editorButtons.Size = new System.Drawing.Size(268, 38);
            this._editorButtons.TabIndex = 1;
            this._editorButtons.WrapContents = false;
            //
            // _runButton
            //
            this._runButton.Glyph = "▶";
            this._runButton.Margin = new System.Windows.Forms.Padding(8, 3, 0, 3);
            this._runButton.Name = "_runButton";
            this._runButton.Size = new System.Drawing.Size(158, 32);
            this._runButton.TabIndex = 0;
            this._runButton.Text = "Executar  (Ctrl+Enter)";
            //
            // _clearButton
            //
            this._clearButton.Kind = Theme.FarmButtonKind.Ghost;
            this._clearButton.Margin = new System.Windows.Forms.Padding(0, 3, 0, 3);
            this._clearButton.Name = "_clearButton";
            this._clearButton.Size = new System.Drawing.Size(90, 32);
            this._clearButton.TabIndex = 1;
            this._clearButton.Text = "Limpar";
            //
            // _samplesCombo
            //
            this._samplesCombo.Location = new System.Drawing.Point(2, 6);
            this._samplesCombo.Name = "_samplesCombo";
            this._samplesCombo.Size = new System.Drawing.Size(330, 24);
            this._samplesCombo.TabIndex = 0;
            //
            // _resultCard
            //
            this._resultCard.Controls.Add(this._grid);
            this._resultCard.Dock = System.Windows.Forms.DockStyle.Fill;
            this._resultCard.Location = new System.Drawing.Point(0, 200);
            this._resultCard.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            this._resultCard.Name = "_resultCard";
            this._resultCard.Padding = new System.Windows.Forms.Padding(12, 44, 12, 12);
            this._resultCard.Size = new System.Drawing.Size(900, 200);
            this._resultCard.TabIndex = 1;
            this._resultCard.Title = "Resultado";
            //
            // _grid
            //
            this._grid.Dock = System.Windows.Forms.DockStyle.Fill;
            this._grid.Location = new System.Drawing.Point(12, 44);
            this._grid.Name = "_grid";
            this._grid.Size = new System.Drawing.Size(876, 144);
            this._grid.TabIndex = 0;
            //
            // RawQueryPage
            //
            this.Controls.Add(this._layout);
            this.Controls.Add(this._status);
            this.Controls.Add(this._header);
            this.Name = "RawQueryPage";
            this.Size = new System.Drawing.Size(900, 500);
            this._layout.ResumeLayout(false);
            this._editorCard.ResumeLayout(false);
            this._editorCard.PerformLayout();
            this._editorButtons.ResumeLayout(false);
            this._editorBar.ResumeLayout(false);
            this._resultCard.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._grid)).EndInit();
            this.ResumeLayout(false);
        }
    }
}
