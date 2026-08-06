namespace NekoLib.Data.RuntimeTests.FarmDatabase.WinForms
{
    partial class ShellForm
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.Panel _sidebar;
        private System.Windows.Forms.Panel _brandPanel;
        private System.Windows.Forms.Label _brandTitle;
        private System.Windows.Forms.Label _brandSubtitle;
        private Theme.SidebarButton _navConnection;
        private Theme.SidebarButton _navBrowse;
        private Theme.SidebarButton _navRawQuery;
        private Theme.SidebarButton _navStock;
        private Theme.SidebarButton _navLog;
        private System.Windows.Forms.Panel _sidebarFooter;
        private Theme.Pill _connectionPill;
        private System.Windows.Forms.Label _connectionPath;
        private System.Windows.Forms.Panel _mainPanel;
        private System.Windows.Forms.Panel _hostPanel;
        private System.Windows.Forms.Panel _consolePanel;
        private System.Windows.Forms.Panel _consoleHeader;
        private System.Windows.Forms.Label _consoleTitle;
        private Theme.FarmButton _consoleClear;
        private System.Windows.Forms.ListBox _sqlTrace;

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
            this._sidebar = new System.Windows.Forms.Panel();
            this._brandPanel = new System.Windows.Forms.Panel();
            this._brandTitle = new System.Windows.Forms.Label();
            this._brandSubtitle = new System.Windows.Forms.Label();
            this._navConnection = new Theme.SidebarButton();
            this._navBrowse = new Theme.SidebarButton();
            this._navRawQuery = new Theme.SidebarButton();
            this._navStock = new Theme.SidebarButton();
            this._navLog = new Theme.SidebarButton();
            this._sidebarFooter = new System.Windows.Forms.Panel();
            this._connectionPill = new Theme.Pill();
            this._connectionPath = new System.Windows.Forms.Label();
            this._mainPanel = new System.Windows.Forms.Panel();
            this._hostPanel = new System.Windows.Forms.Panel();
            this._consolePanel = new System.Windows.Forms.Panel();
            this._consoleHeader = new System.Windows.Forms.Panel();
            this._consoleTitle = new System.Windows.Forms.Label();
            this._consoleClear = new Theme.FarmButton();
            this._sqlTrace = new System.Windows.Forms.ListBox();
            this._sidebar.SuspendLayout();
            this._brandPanel.SuspendLayout();
            this._sidebarFooter.SuspendLayout();
            this._mainPanel.SuspendLayout();
            this._consolePanel.SuspendLayout();
            this._consoleHeader.SuspendLayout();
            this.SuspendLayout();
            //
            // _sidebar
            //
            // Docked children are laid out from the highest index down, so the last
            // control added is the one nearest its edge. The Add order below is
            // therefore the reverse of the visual order.
            this._sidebar.BackColor = System.Drawing.Color.FromArgb(13, 17, 20);
            this._sidebar.Controls.Add(this._sidebarFooter);
            this._sidebar.Controls.Add(this._navLog);
            this._sidebar.Controls.Add(this._navStock);
            this._sidebar.Controls.Add(this._navRawQuery);
            this._sidebar.Controls.Add(this._navBrowse);
            this._sidebar.Controls.Add(this._navConnection);
            this._sidebar.Controls.Add(this._brandPanel);
            this._sidebar.Dock = System.Windows.Forms.DockStyle.Left;
            this._sidebar.Location = new System.Drawing.Point(0, 0);
            this._sidebar.Name = "_sidebar";
            this._sidebar.Size = new System.Drawing.Size(228, 761);
            this._sidebar.TabIndex = 0;
            //
            // _brandPanel
            //
            this._brandPanel.BackColor = System.Drawing.Color.FromArgb(13, 17, 20);
            this._brandPanel.Controls.Add(this._brandSubtitle);
            this._brandPanel.Controls.Add(this._brandTitle);
            this._brandPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this._brandPanel.Location = new System.Drawing.Point(0, 0);
            this._brandPanel.Name = "_brandPanel";
            this._brandPanel.Size = new System.Drawing.Size(228, 84);
            this._brandPanel.TabIndex = 0;
            //
            // _brandTitle
            //
            this._brandTitle.AutoSize = true;
            this._brandTitle.Font = new System.Drawing.Font("Segoe UI Semibold", 13F);
            this._brandTitle.ForeColor = System.Drawing.Color.FromArgb(228, 236, 240);
            this._brandTitle.Location = new System.Drawing.Point(17, 24);
            this._brandTitle.Name = "_brandTitle";
            this._brandTitle.Size = new System.Drawing.Size(140, 25);
            this._brandTitle.TabIndex = 0;
            this._brandTitle.Text = "Fazenda · Dados";
            //
            // _brandSubtitle
            //
            this._brandSubtitle.AutoSize = true;
            this._brandSubtitle.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this._brandSubtitle.ForeColor = System.Drawing.Color.FromArgb(92, 107, 116);
            this._brandSubtitle.Location = new System.Drawing.Point(19, 52);
            this._brandSubtitle.Name = "_brandSubtitle";
            this._brandSubtitle.Size = new System.Drawing.Size(150, 13);
            this._brandSubtitle.TabIndex = 1;
            this._brandSubtitle.Text = "NekoLib.Data · cenário runtime";
            //
            // _navConnection
            //
            this._navConnection.Dock = System.Windows.Forms.DockStyle.Top;
            this._navConnection.Glyph = "◆";
            this._navConnection.Location = new System.Drawing.Point(0, 84);
            this._navConnection.Name = "_navConnection";
            this._navConnection.Size = new System.Drawing.Size(228, 42);
            this._navConnection.TabIndex = 1;
            this._navConnection.Text = "Conexão";
            //
            // _navBrowse
            //
            this._navBrowse.Dock = System.Windows.Forms.DockStyle.Top;
            this._navBrowse.Glyph = "▦";
            this._navBrowse.Location = new System.Drawing.Point(0, 126);
            this._navBrowse.Name = "_navBrowse";
            this._navBrowse.Size = new System.Drawing.Size(228, 42);
            this._navBrowse.TabIndex = 2;
            this._navBrowse.Text = "Tabelas";
            //
            // _navRawQuery
            //
            this._navRawQuery.Dock = System.Windows.Forms.DockStyle.Top;
            this._navRawQuery.Glyph = "❯";
            this._navRawQuery.Location = new System.Drawing.Point(0, 168);
            this._navRawQuery.Name = "_navRawQuery";
            this._navRawQuery.Size = new System.Drawing.Size(228, 42);
            this._navRawQuery.TabIndex = 3;
            this._navRawQuery.Text = "Consulta livre";
            //
            // _navStock
            //
            this._navStock.Dock = System.Windows.Forms.DockStyle.Top;
            this._navStock.Glyph = "❖";
            this._navStock.Location = new System.Drawing.Point(0, 210);
            this._navStock.Name = "_navStock";
            this._navStock.Size = new System.Drawing.Size(228, 42);
            this._navStock.TabIndex = 4;
            this._navStock.Text = "Controle de estoque";
            //
            // _navLog
            //
            this._navLog.Dock = System.Windows.Forms.DockStyle.Top;
            this._navLog.Glyph = "☰";
            this._navLog.Location = new System.Drawing.Point(0, 252);
            this._navLog.Name = "_navLog";
            this._navLog.Size = new System.Drawing.Size(228, 42);
            this._navLog.TabIndex = 5;
            this._navLog.Text = "Log de operações";
            //
            // _sidebarFooter
            //
            this._sidebarFooter.BackColor = System.Drawing.Color.FromArgb(13, 17, 20);
            this._sidebarFooter.Controls.Add(this._connectionPath);
            this._sidebarFooter.Controls.Add(this._connectionPill);
            this._sidebarFooter.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._sidebarFooter.Location = new System.Drawing.Point(0, 665);
            this._sidebarFooter.Name = "_sidebarFooter";
            this._sidebarFooter.Padding = new System.Windows.Forms.Padding(16, 12, 16, 16);
            this._sidebarFooter.Size = new System.Drawing.Size(228, 96);
            this._sidebarFooter.TabIndex = 6;
            //
            // _connectionPill
            //
            this._connectionPill.Location = new System.Drawing.Point(16, 12);
            this._connectionPill.Name = "_connectionPill";
            this._connectionPill.Size = new System.Drawing.Size(140, 22);
            this._connectionPill.TabIndex = 0;
            this._connectionPill.Text = "desconectado";
            //
            // _connectionPath
            //
            this._connectionPath.Font = new System.Drawing.Font("Segoe UI", 7.5F);
            this._connectionPath.ForeColor = System.Drawing.Color.FromArgb(92, 107, 116);
            this._connectionPath.Location = new System.Drawing.Point(16, 40);
            this._connectionPath.Name = "_connectionPath";
            this._connectionPath.Size = new System.Drawing.Size(196, 40);
            this._connectionPath.TabIndex = 1;
            this._connectionPath.Text = "";
            //
            // _mainPanel
            //
            this._mainPanel.BackColor = System.Drawing.Color.FromArgb(18, 23, 26);
            this._mainPanel.Controls.Add(this._hostPanel);
            this._mainPanel.Controls.Add(this._consolePanel);
            this._mainPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._mainPanel.Location = new System.Drawing.Point(228, 0);
            this._mainPanel.Name = "_mainPanel";
            this._mainPanel.Size = new System.Drawing.Size(972, 761);
            this._mainPanel.TabIndex = 1;
            //
            // _hostPanel
            //
            // This panel is the navigation host. Nothing may be added to it by hand:
            // the runtime owns its children.
            this._hostPanel.BackColor = System.Drawing.Color.FromArgb(18, 23, 26);
            this._hostPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._hostPanel.Location = new System.Drawing.Point(0, 0);
            this._hostPanel.Name = "_hostPanel";
            this._hostPanel.Padding = new System.Windows.Forms.Padding(24, 20, 24, 12);
            this._hostPanel.Size = new System.Drawing.Size(972, 585);
            this._hostPanel.TabIndex = 1;
            //
            // _consolePanel
            //
            this._consolePanel.BackColor = System.Drawing.Color.FromArgb(13, 17, 20);
            this._consolePanel.Controls.Add(this._sqlTrace);
            this._consolePanel.Controls.Add(this._consoleHeader);
            this._consolePanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._consolePanel.Location = new System.Drawing.Point(0, 585);
            this._consolePanel.Name = "_consolePanel";
            this._consolePanel.Padding = new System.Windows.Forms.Padding(24, 0, 24, 12);
            this._consolePanel.Size = new System.Drawing.Size(972, 176);
            this._consolePanel.TabIndex = 0;
            //
            // _consoleHeader
            //
            this._consoleHeader.Controls.Add(this._consoleTitle);
            this._consoleHeader.Controls.Add(this._consoleClear);
            this._consoleHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this._consoleHeader.Location = new System.Drawing.Point(24, 0);
            this._consoleHeader.Name = "_consoleHeader";
            this._consoleHeader.Size = new System.Drawing.Size(924, 34);
            this._consoleHeader.TabIndex = 0;
            //
            // _consoleTitle
            //
            this._consoleTitle.AutoSize = true;
            this._consoleTitle.Font = new System.Drawing.Font("Segoe UI Semibold", 9F);
            this._consoleTitle.ForeColor = System.Drawing.Color.FromArgb(135, 153, 164);
            this._consoleTitle.Location = new System.Drawing.Point(2, 10);
            this._consoleTitle.Name = "_consoleTitle";
            this._consoleTitle.Size = new System.Drawing.Size(120, 15);
            this._consoleTitle.TabIndex = 0;
            this._consoleTitle.Text = "SQL emitido";
            //
            // _consoleClear
            //
            // Docked right rather than anchored: an anchored child of a resizing
            // container keeps its painted position but stops receiving mouse input
            // once the shell is maximized.
            this._consoleClear.Dock = System.Windows.Forms.DockStyle.Right;
            this._consoleClear.Kind = Theme.FarmButtonKind.Ghost;
            this._consoleClear.Location = new System.Drawing.Point(832, 0);
            this._consoleClear.Name = "_consoleClear";
            this._consoleClear.Size = new System.Drawing.Size(92, 34);
            this._consoleClear.TabIndex = 1;
            this._consoleClear.Text = "Limpar";
            //
            // _sqlTrace
            //
            this._sqlTrace.BackColor = System.Drawing.Color.FromArgb(13, 17, 20);
            this._sqlTrace.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this._sqlTrace.Dock = System.Windows.Forms.DockStyle.Fill;
            this._sqlTrace.Font = new System.Drawing.Font("Consolas", 8.25F);
            this._sqlTrace.ForeColor = System.Drawing.Color.FromArgb(135, 153, 164);
            this._sqlTrace.FormattingEnabled = true;
            this._sqlTrace.HorizontalScrollbar = true;
            this._sqlTrace.IntegralHeight = false;
            this._sqlTrace.ItemHeight = 13;
            this._sqlTrace.Location = new System.Drawing.Point(24, 34);
            this._sqlTrace.Name = "_sqlTrace";
            this._sqlTrace.Size = new System.Drawing.Size(924, 130);
            this._sqlTrace.TabIndex = 1;
            //
            // ShellForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.BackColor = System.Drawing.Color.FromArgb(18, 23, 26);
            this.ClientSize = new System.Drawing.Size(1200, 761);
            this.Controls.Add(this._mainPanel);
            this.Controls.Add(this._sidebar);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.ForeColor = System.Drawing.Color.FromArgb(228, 236, 240);
            this.MinimumSize = new System.Drawing.Size(1020, 640);
            this.Name = "ShellForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Fazenda · banco de dados — NekoLib.Data";
            this._sidebar.ResumeLayout(false);
            this._brandPanel.ResumeLayout(false);
            this._brandPanel.PerformLayout();
            this._sidebarFooter.ResumeLayout(false);
            this._mainPanel.ResumeLayout(false);
            this._consolePanel.ResumeLayout(false);
            this._consoleHeader.ResumeLayout(false);
            this._consoleHeader.PerformLayout();
            this.ResumeLayout(false);
        }
    }
}
