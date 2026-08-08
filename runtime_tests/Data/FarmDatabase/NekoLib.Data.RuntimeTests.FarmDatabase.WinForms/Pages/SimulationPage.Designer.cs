namespace NekoLib.Data.RuntimeTests.FarmDatabase.WinForms.Pages
{
    partial class SimulationPage
    {
        private System.ComponentModel.IContainer components = null;

        private Theme.PageHeader _header;
        private Theme.StatusLine _status;
        private Theme.Card _card;
        private System.Windows.Forms.Panel _toolbar;
        private System.Windows.Forms.Label _seedLabel;
        private System.Windows.Forms.NumericUpDown _seed;
        private Theme.FarmButton _startButton;
        private Theme.FarmButton _resumeButton;
        private Theme.FarmButton _playButton;
        private System.Windows.Forms.Label _speedLabel;
        private System.Windows.Forms.ComboBox _speed;
        private System.Windows.Forms.CheckBox _showFarm;
        private System.Windows.Forms.CheckBox _showWorkers;
        private System.Windows.Forms.Panel _stats;
        private System.Windows.Forms.Panel _prices;
        private Theme.FarmField _field;

        // Dispose lives in SimulationPage.cs for this page, because it also has to
        // stop the two timers before the base class tears the control down.

        private void InitializeComponent()
        {
            this._header = new Theme.PageHeader();
            this._status = new Theme.StatusLine();
            this._card = new Theme.Card();
            this._toolbar = new System.Windows.Forms.Panel();
            this._seedLabel = new System.Windows.Forms.Label();
            this._seed = new System.Windows.Forms.NumericUpDown();
            this._startButton = new Theme.FarmButton();
            this._resumeButton = new Theme.FarmButton();
            this._playButton = new Theme.FarmButton();
            this._speedLabel = new System.Windows.Forms.Label();
            this._speed = new System.Windows.Forms.ComboBox();
            this._showFarm = new System.Windows.Forms.CheckBox();
            this._showWorkers = new System.Windows.Forms.CheckBox();
            this._stats = new System.Windows.Forms.Panel();
            this._prices = new System.Windows.Forms.Panel();
            this._field = new Theme.FarmField();
            this._card.SuspendLayout();
            this._toolbar.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._seed)).BeginInit();
            this.SuspendLayout();
            //
            // _header
            //
            this._header.Dock = System.Windows.Forms.DockStyle.Top;
            this._header.Location = new System.Drawing.Point(0, 0);
            this._header.Name = "_header";
            this._header.Size = new System.Drawing.Size(900, 62);
            this._header.Subtitle = "O mercado global é oculto: só o preço que ele produz aparece.";
            this._header.TabIndex = 0;
            this._header.Text = "Simulação";
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
            this._card.Controls.Add(this._field);
            this._card.Controls.Add(this._prices);
            this._card.Controls.Add(this._stats);
            this._card.Controls.Add(this._toolbar);
            this._card.Dock = System.Windows.Forms.DockStyle.Fill;
            this._card.Location = new System.Drawing.Point(0, 62);
            this._card.Name = "_card";
            this._card.Padding = new System.Windows.Forms.Padding(12, 44, 12, 12);
            this._card.Size = new System.Drawing.Size(900, 408);
            this._card.TabIndex = 1;
            this._card.Title = "Fazenda";
            //
            // _toolbar
            //
            this._toolbar.Controls.Add(this._showWorkers);
            this._toolbar.Controls.Add(this._showFarm);
            this._toolbar.Controls.Add(this._speed);
            this._toolbar.Controls.Add(this._speedLabel);
            this._toolbar.Controls.Add(this._playButton);
            this._toolbar.Controls.Add(this._resumeButton);
            this._toolbar.Controls.Add(this._startButton);
            this._toolbar.Controls.Add(this._seed);
            this._toolbar.Controls.Add(this._seedLabel);
            this._toolbar.Dock = System.Windows.Forms.DockStyle.Top;
            this._toolbar.Location = new System.Drawing.Point(12, 44);
            this._toolbar.Name = "_toolbar";
            this._toolbar.Size = new System.Drawing.Size(876, 42);
            this._toolbar.TabIndex = 0;
            //
            // _seedLabel
            //
            this._seedLabel.AutoSize = true;
            this._seedLabel.ForeColor = System.Drawing.Color.FromArgb(135, 153, 164);
            this._seedLabel.Location = new System.Drawing.Point(4, 14);
            this._seedLabel.Name = "_seedLabel";
            this._seedLabel.Size = new System.Drawing.Size(46, 15);
            this._seedLabel.TabIndex = 0;
            this._seedLabel.Text = "Semente";
            //
            // _seed
            //
            this._seed.Location = new System.Drawing.Point(58, 10);
            this._seed.Maximum = new decimal(new int[] { 999999, 0, 0, 0 });
            this._seed.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this._seed.Name = "_seed";
            this._seed.Size = new System.Drawing.Size(72, 23);
            this._seed.TabIndex = 1;
            this._seed.Value = new decimal(new int[] { 7, 0, 0, 0 });
            //
            // _startButton
            //
            this._startButton.Location = new System.Drawing.Point(140, 6);
            this._startButton.Name = "_startButton";
            this._startButton.Size = new System.Drawing.Size(92, 30);
            this._startButton.TabIndex = 2;
            this._startButton.Text = "Start";
            //
            // _resumeButton
            //
            this._resumeButton.Location = new System.Drawing.Point(238, 6);
            this._resumeButton.Name = "_resumeButton";
            this._resumeButton.Size = new System.Drawing.Size(92, 30);
            this._resumeButton.TabIndex = 3;
            this._resumeButton.Text = "Retomar";
            //
            // _playButton
            //
            this._playButton.Location = new System.Drawing.Point(336, 6);
            this._playButton.Name = "_playButton";
            this._playButton.Size = new System.Drawing.Size(92, 30);
            this._playButton.TabIndex = 4;
            this._playButton.Text = "Continuar";
            //
            // _speedLabel
            //
            this._speedLabel.AutoSize = true;
            this._speedLabel.ForeColor = System.Drawing.Color.FromArgb(135, 153, 164);
            this._speedLabel.Location = new System.Drawing.Point(442, 14);
            this._speedLabel.Name = "_speedLabel";
            this._speedLabel.Size = new System.Drawing.Size(64, 15);
            this._speedLabel.TabIndex = 5;
            this._speedLabel.Text = "Velocidade";
            //
            // _speed
            //
            this._speed.Location = new System.Drawing.Point(512, 10);
            this._speed.Name = "_speed";
            this._speed.Size = new System.Drawing.Size(120, 23);
            this._speed.TabIndex = 6;
            //
            // _showFarm
            //
            this._showFarm.AutoSize = true;
            this._showFarm.Checked = true;
            this._showFarm.CheckState = System.Windows.Forms.CheckState.Checked;
            this._showFarm.ForeColor = System.Drawing.Color.FromArgb(228, 236, 240);
            this._showFarm.Location = new System.Drawing.Point(648, 13);
            this._showFarm.Name = "_showFarm";
            this._showFarm.Size = new System.Drawing.Size(107, 19);
            this._showFarm.TabIndex = 7;
            this._showFarm.Text = "Mostrar fazenda";
            //
            // _showWorkers
            //
            this._showWorkers.AutoSize = true;
            this._showWorkers.Checked = true;
            this._showWorkers.CheckState = System.Windows.Forms.CheckState.Checked;
            this._showWorkers.ForeColor = System.Drawing.Color.FromArgb(228, 236, 240);
            this._showWorkers.Location = new System.Drawing.Point(762, 13);
            this._showWorkers.Name = "_showWorkers";
            this._showWorkers.Size = new System.Drawing.Size(104, 19);
            this._showWorkers.TabIndex = 8;
            this._showWorkers.Text = "Mostrar workers";
            //
            // _stats
            //
            this._stats.Dock = System.Windows.Forms.DockStyle.Top;
            this._stats.Location = new System.Drawing.Point(12, 86);
            this._stats.Name = "_stats";
            this._stats.Size = new System.Drawing.Size(876, 58);
            this._stats.TabIndex = 1;
            //
            // _prices
            //
            this._prices.Dock = System.Windows.Forms.DockStyle.Right;
            this._prices.Location = new System.Drawing.Point(676, 144);
            this._prices.Name = "_prices";
            this._prices.Size = new System.Drawing.Size(212, 252);
            this._prices.TabIndex = 2;
            //
            // _field
            //
            this._field.Dock = System.Windows.Forms.DockStyle.Fill;
            this._field.Location = new System.Drawing.Point(12, 144);
            this._field.Name = "_field";
            this._field.Size = new System.Drawing.Size(664, 252);
            this._field.TabIndex = 3;
            //
            // SimulationPage
            //
            this.Controls.Add(this._card);
            this.Controls.Add(this._status);
            this.Controls.Add(this._header);
            this.Name = "SimulationPage";
            this.Size = new System.Drawing.Size(900, 500);
            this._card.ResumeLayout(false);
            this._toolbar.ResumeLayout(false);
            this._toolbar.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._seed)).EndInit();
            this.ResumeLayout(false);
        }
    }
}
