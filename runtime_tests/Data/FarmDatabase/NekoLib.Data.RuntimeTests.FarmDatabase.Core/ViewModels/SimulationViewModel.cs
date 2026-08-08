#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using NekoLib.Mvvm;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.Simulation;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.Core.ViewModels
{
    /// <summary>
    /// Drives the farm simulation and exposes what the panels show.
    /// <para/>
    /// The tick loop is not owned here: this exposes <see cref="AdvanceAsync"/> and the
    /// page decides the cadence. That keeps the engine and its persistence free of any
    /// UI timer, which is what a headless run would need.
    /// </summary>
    public sealed class SimulationViewModel : FarmViewModelBase
    {
        private SimSnapshot? _snapshot;
        private bool _running;
        private bool _ticking;
        private readonly SimMetrics _metrics;
        private bool _showFarm = true;
        private bool _showWorkers = true;
        private int _seed = 7;
        private int _ticksPerPulse = 1;
        private long _persistedTicks;
        private string _lastEvent = string.Empty;

        public SimulationViewModel(FarmWorkspace workspace, SimMetrics? metrics = null)
            : base(workspace)
        {
            _metrics = metrics ?? new SimMetrics(null, null);

            StartCommand = new RelayCommand(
                () => Run(StartAsync, "Simulação iniciada."),
                () => IsConnected && IsIdle);

            ResumeCommand = new RelayCommand(
                () => Run(ResumeAsync, "Run retomado."),
                () => IsConnected && IsIdle);

            PlayPauseCommand = new RelayCommand(
                TogglePlay,
                () => _snapshot != null);
        }

        public RelayCommand StartCommand { get; }
        public RelayCommand ResumeCommand { get; }
        public RelayCommand PlayPauseCommand { get; }

        /// <summary>The world, or null before a run is started or resumed.</summary>
        public SimSnapshot? Snapshot => _snapshot;

        public bool HasRun => _snapshot != null;

        /// <summary>Seed for the next <see cref="StartCommand"/>. Two runs with the same seed must match.</summary>
        public int Seed
        {
            get => _seed;
            set => SetProperty(ref _seed, value);
        }

        public bool IsRunning
        {
            get => _running;
            private set
            {
                if (!SetProperty(ref _running, value)) return;

                // The SQL console costs a full list-box layout per statement, and a
                // running simulation emits about ten per tick. Pausing the trace while
                // the farm runs is what keeps the window responsive at high speed; the
                // workspace reports how many it skipped when the run stops.
                Workspace.SuppressTrace = value;
                OnPropertyChanged(nameof(PlayPauseCaption));
            }
        }

        public string PlayPauseCaption => _running ? "Pausar" : "Continuar";

        /// <summary>Whether the field is drawn. Turning it off is the closest this page gets to headless.</summary>
        public bool ShowFarm
        {
            get => _showFarm;
            set => SetProperty(ref _showFarm, value);
        }

        /// <summary>
        /// Ticks advanced per timer pulse. One is real time; higher values are the
        /// accelerated mode, where the drawing is decorative and only the numbers and
        /// the log count as evidence.
        /// </summary>
        public int TicksPerPulse
        {
            get => _ticksPerPulse;
            set => SetProperty(ref _ticksPerPulse, value < 1 ? 1 : value);
        }

        /// <summary>
        /// Up to this speed the run is faithful: one transaction per tick, and the
        /// field draws every worker where the simulation says it is.
        /// <para/>
        /// Twenty ticks a second is twenty transactions a second, which both engines
        /// were measured well above - SQLite around two hundred, Access around six.
        /// Access therefore stops being faithful long before this ceiling, and its
        /// dropped-pulse count is what says so.
        /// </summary>
        public const int FaithfulMax = 20;

        /// <summary>Past this the tiles stop being drawn individually.</summary>
        public const int StaticTerrainFrom = 100;

        public bool IsAccelerated => _ticksPerPulse > FaithfulMax;

        /// <summary>Whether the user wants to see the walking dots at all.</summary>
        public bool ShowWorkers
        {
            get => _showWorkers;
            set
            {
                if (SetProperty(ref _showWorkers, value))
                    OnPropertyChanged(nameof(RendersWorkers));
            }
        }

        /// <summary>
        /// Whether the field draws the walking dots. The switch is obeyed at every
        /// speed on purpose: its job is to let the same run be watched with and without
        /// them, and a control that silently overrides itself measures nothing.
        /// <para/>
        /// Accuracy is a separate matter from cost. Above <see cref="FaithfulMax"/> the
        /// dots are interpolated across a pulse that advanced many ticks at once, so
        /// they show plausible motion rather than where a worker was — the page says so
        /// while accelerated, and the log remains the record.
        /// </summary>
        public bool RendersWorkers => _showWorkers;

        /// <summary>
        /// Below this the field paints each tile at its own growth; at and above it the
        /// terrain is painted as one flat block in its crop's colour. Travel and growth
        /// are still computed either way - only the drawing gives up.
        /// </summary>
        public bool RendersTiles => _ticksPerPulse < StaticTerrainFrom;

        /// <summary>Ticks that reached the database. Compare with <see cref="SimSnapshot"/>'s tick.</summary>
        public long PersistedTicks => _persistedTicks;

        /// <summary>What the run is costing. Reported on a window, never per tick.</summary>
        public SimMetrics Metrics => _metrics;

        /// <summary>The last closed measurement window, for the panel.</summary>
        public SimMetricsWindow Measured => _metrics.Last;

        /// <summary>
        /// The most recent audited event. Written to directly during a pulse and
        /// announced once at the end of it: raising it per event repainted the page
        /// hundreds of times inside a single accelerated pulse, which was the other
        /// half of the stutter.
        /// </summary>
        public string LastEvent => _lastEvent;

        // -----------------------------------------------------------------
        // Headline numbers
        // -----------------------------------------------------------------

        public string TickText => _snapshot == null
            ? "-"
            : _snapshot.State.Tick.ToString("N0", CultureInfo.CurrentCulture);

        public string CalendarText => _snapshot == null
            ? "-"
            : "mês " + (_snapshot.State.Month + 1) +
              " · semana " + ((_snapshot.State.Week % SimClock.WeeksPerMonth) + 1) +
              " · dia " + ((_snapshot.State.Day % SimClock.DaysPerWeek) + 1);

        public string GoldText => _snapshot == null
            ? "-"
            : _snapshot.State.Gold.ToString("N0", CultureInfo.CurrentCulture) + " g";

        public string FarmText => _snapshot == null
            ? "-"
            : _snapshot.State.Slots + " slots · " +
              _snapshot.State.Workers + " workers · " +
              _snapshot.State.Terrains + " terreno(s)";

        /// <summary>
        /// The prime is shown because it is the only visible trace of the hidden
        /// market: prices move every month and this is the reason why.
        /// </summary>
        public string CycleText => _snapshot == null
            ? "-"
            : "ciclo " + _snapshot.State.Prime;

        /// <summary>Current price per crop, ordered by the catalogue.</summary>
        public IReadOnlyList<CropPrice> Prices
        {
            get
            {
                var prices = new List<CropPrice>();
                if (_snapshot == null) return prices;

                IReadOnlyList<string> chosen = _snapshot.ChosenCrops();

                foreach (Crop crop in SimRules.Crops)
                {
                    SimMarketRow row = _snapshot.MarketFor(crop.Name);
                    bool planted = false;
                    foreach (string name in chosen)
                        if (name == crop.Name) { planted = true; break; }

                    prices.Add(new CropPrice(
                        crop.Name,
                        row.Price,
                        crop.BasePrice,
                        planted,
                        _snapshot.InventoryFor(crop.Name).Quantity));
                }

                return prices;
            }
        }

        // -----------------------------------------------------------------

        private async Task StartAsync()
        {
            IsRunning = false;
            _metrics.Reset();
            _metrics.Engine = Workspace.Current?.Profile.DisplayName;

            _snapshot = await Workspace.Require().StartRunAsync(_seed).ConfigureAwait(true);
            _persistedTicks = 0;
            _lastEvent = "Semente " + _seed + " · mundo criado";
            RaiseAll();
        }

        private async Task ResumeAsync()
        {
            IsRunning = false;
            _snapshot = await Workspace.Require().LoadSimAsync().ConfigureAwait(true);

            if (_snapshot == null)
            {
                StatusMessage = "Nenhum run salvo neste banco.";
                _lastEvent = string.Empty;
            }
            else
            {
                _seed = _snapshot.State.Seed;
                _persistedTicks = _snapshot.State.Tick;
                _lastEvent = "Retomado no tick " + _snapshot.State.Tick;
                OnPropertyChanged(nameof(Seed));
            }

            RaiseAll();
        }

        private void TogglePlay()
        {
            if (_snapshot == null) return;

            IsRunning = !IsRunning;
            StatusMessage = IsRunning ? "Rodando." : "Pausado.";

            if (!IsRunning)
                _metrics.ReportTotal("pausado no tick " + _snapshot.State.Tick);
        }

        /// <summary>
        /// Advances and persists one pulse. Re-entrant calls are dropped rather than
        /// queued: if a pulse takes longer than the interval, the honest thing is to
        /// skip it, not to let ticks pile up behind a database that cannot keep pace.
        /// </summary>
        public async Task AdvanceAsync()
        {
            if (!IsRunning || _snapshot == null || !IsConnected)
                return;

            if (_ticking)
            {
                // The previous pulse is still in the database. Dropping it is the
                // honest outcome - queueing would hide that the engine cannot keep the
                // pace being asked of it - and the count is the measurement that says
                // where the requested speed stopped being real.
                _metrics.RecordDropped();
                return;
            }

            _ticking = true;

            var pulseWatch = System.Diagnostics.Stopwatch.StartNew();
            var dbWatch = new System.Diagnostics.Stopwatch();
            long statementsBefore = Workspace.StatementCount;
            long ticksBefore = _snapshot.State.Tick;

            try
            {
                FarmDb db = Workspace.Require();

                if (_ticksPerPulse <= FaithfulMax)
                {
                    // Real time and the slow speeds keep one transaction per tick, so
                    // the durability claim is tested where it is made.
                    for (int i = 0; i < _ticksPerPulse; i++)
                    {
                        TickOutcome outcome = FarmSimulation.Advance(_snapshot);

                        dbWatch.Start();
                        await db.SaveTickAsync(_snapshot, outcome).ConfigureAwait(true);
                        dbWatch.Stop();

                        Record(outcome);
                    }
                }
                else
                {
                    // Accelerated: advance the whole pulse, then commit it once. Five
                    // hundred round trips a second is beyond either engine, and the
                    // overhead was eating the speed it was supposed to deliver.
                    bool touched = false;
                    var events = new List<SimEvent>();

                    for (int i = 0; i < _ticksPerPulse; i++)
                    {
                        TickOutcome outcome = FarmSimulation.Advance(_snapshot);
                        touched |= outcome.TouchedState;
                        events.AddRange(outcome.Events);
                        Record(outcome);
                    }

                    dbWatch.Start();
                    await db.SaveBatchAsync(_snapshot, touched, events).ConfigureAwait(true);
                    dbWatch.Stop();
                }

                _persistedTicks = _snapshot.State.Tick;

                pulseWatch.Stop();
                _metrics.RecordPulse(
                    (int)(_snapshot.State.Tick - ticksBefore),
                    pulseWatch.Elapsed.TotalMilliseconds,
                    dbWatch.Elapsed.TotalMilliseconds,
                    Workspace.StatementCount - statementsBefore);

                RaiseAll();
            }
            catch (Exception ex)
            {
                IsRunning = false;
                ErrorMessage = Describe(ex);
                StatusMessage = "Parado por erro.";
                _metrics.ReportTotal("erro: " + ex.Message);
            }
            finally
            {
                _ticking = false;
            }
        }

        /// <summary>
        /// Keeps the newest audited event without announcing it. Announcing per event
        /// repainted the page hundreds of times inside one accelerated pulse.
        /// </summary>
        private void Record(TickOutcome outcome)
        {
            if (outcome.Events.Count == 0) return;

            SimEvent last = outcome.Events[outcome.Events.Count - 1];
            _lastEvent = last.Kind + " · " + last.Name + " · " + last.Reason;
        }

        private void RaiseAll()
        {
            OnPropertyChanged(nameof(Snapshot));
            OnPropertyChanged(nameof(HasRun));
            OnPropertyChanged(nameof(TickText));
            OnPropertyChanged(nameof(CalendarText));
            OnPropertyChanged(nameof(GoldText));
            OnPropertyChanged(nameof(FarmText));
            OnPropertyChanged(nameof(CycleText));
            OnPropertyChanged(nameof(Prices));
            OnPropertyChanged(nameof(PersistedTicks));
            OnPropertyChanged(nameof(LastEvent));
            RaiseCommandStates();
        }

        protected override void RaiseCommandStates()
        {
            StartCommand.RaiseCanExecuteChanged();
            ResumeCommand.RaiseCanExecuteChanged();
            PlayPauseCommand.RaiseCanExecuteChanged();
        }

        public override void OnConnectionChanged()
        {
            base.OnConnectionChanged();

            if (!IsConnected)
            {
                IsRunning = false;
                _snapshot = null;
            }

            RaiseAll();
        }
    }

    /// <summary>One row of the price panel.</summary>
    public sealed class CropPrice
    {
        public CropPrice(string crop, double price, double basePrice, bool planted, int inStock)
        {
            Crop = crop;
            Price = price;
            BasePrice = basePrice;
            Planted = planted;
            InStock = inStock;
        }

        public string Crop { get; }
        public double Price { get; }
        public double BasePrice { get; }

        /// <summary>True when a terrain is currently growing this crop.</summary>
        public bool Planted { get; }

        public int InStock { get; }

        /// <summary>0..1 of base price. The panel draws this as a bar - the market itself stays hidden.</summary>
        public double Health => BasePrice <= 0 ? 0 : Price / BasePrice;
    }
}
