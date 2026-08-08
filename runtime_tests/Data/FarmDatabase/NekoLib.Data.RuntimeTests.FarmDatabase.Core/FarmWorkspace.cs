#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.Providers;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.Core
{
    /// <summary>
    /// Application-wide state: which provider is open, and the SQL trace every page
    /// can read. Pages come and go with navigation, so the connection cannot live on
    /// any single page.
    /// <para/>
    /// The database files are written under LocalAppData rather than inside the
    /// repository, so the scenario leaves no ignored fixtures behind and can be reset
    /// from the UI.
    /// </summary>
    public sealed class FarmWorkspace : IDisposable
    {
        private const int MaxTraceEntries = 400;

        private readonly List<string> _sqlTrace = new List<string>();
        private readonly object _traceSync = new object();
        private FarmDb? _current;
        private bool _disposed;
        private volatile bool _suppressTrace;
        private long _suppressedCount;
        private long _statementCount;

        /// <summary>Raised whenever the open database changes (connect or disconnect).</summary>
        public event Action? ConnectionChanged;

        /// <summary>Raised for each SQL statement, already trimmed for display.</summary>
        public event Action<string>? SqlTraced;

        public FarmWorkspace()
        {
            RootDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NekoLib",
                "FarmDatabase");

            Profiles = new IFarmProviderProfile[]
            {
                new SqliteProfile(Path.Combine(RootDirectory, "farm.db")),
                new AccessProfile(Path.Combine(RootDirectory, "farm.accdb"))
            };
        }

        /// <summary>Where the database files are created.</summary>
        public string RootDirectory { get; }

        /// <summary>Both provider profiles, connected or not.</summary>
        public IReadOnlyList<IFarmProviderProfile> Profiles { get; }

        public FarmDb? Current => _current;

        public bool IsConnected => _current != null;

        /// <summary>The open database, or an exception explaining that there is none.</summary>
        public FarmDb Require() =>
            _current ?? throw new InvalidOperationException(
                "Nenhum banco conectado. Use a página de Conexão primeiro.");

        public IReadOnlyList<string> SqlTrace
        {
            get { lock (_traceSync) { return _sqlTrace.ToArray(); } }
        }

        public IFarmProviderProfile ProfileFor(FarmProvider provider)
        {
            foreach (IFarmProviderProfile profile in Profiles)
                if (profile.Provider == provider)
                    return profile;

            throw new ArgumentOutOfRangeException(nameof(provider));
        }

        /// <summary>
        /// Opens a provider, replacing any current connection. Passing
        /// <paramref name="recreate"/> deletes the file first, which is how the UI
        /// offers "recreate from scratch".
        /// </summary>
        public async Task ConnectAsync(
            FarmProvider provider,
            bool recreate,
            CancellationToken ct = default)
        {
            IFarmProviderProfile profile = ProfileFor(provider);

            FarmDb opened = await FarmDb.OpenAsync(profile, recreate, ct).ConfigureAwait(false);
            opened.SqlTraced += OnSqlTraced;

            Disconnect();
            _current = opened;
            ConnectionChanged?.Invoke();
        }

        public void Disconnect()
        {
            FarmDb? previous = _current;
            if (previous == null) return;

            previous.SqlTraced -= OnSqlTraced;
            previous.Dispose();
            _current = null;
            ConnectionChanged?.Invoke();
        }

        public void ClearTrace()
        {
            lock (_traceSync) { _sqlTrace.Clear(); }
        }

        /// <summary>
        /// Temporarily stops feeding the trace, counting what it skips.
        /// <para/>
        /// The console is one of this scenario's reasons to exist, so this is a pause
        /// rather than a switch: the simulation raises it while it runs and drops it
        /// when it stops, and the count is reported so the record stays honest about
        /// what was not shown. Without it the UI does a full list-box layout per
        /// statement, which at a hundred ticks a second is thousands of layouts a
        /// second and the window stops responding.
        /// </summary>
        public bool SuppressTrace
        {
            get => _suppressTrace;
            set
            {
                if (_suppressTrace == value) return;

                _suppressTrace = value;

                if (!value)
                {
                    long skipped = Interlocked.Exchange(ref _suppressedCount, 0);
                    if (skipped > 0)
                    {
                        Publish("· " + skipped.ToString("N0") +
                                " statement(s) suprimido(s) enquanto a simulação rodava");
                    }
                }
            }
        }

        /// <summary>Statements skipped since suppression was last raised.</summary>
        public long SuppressedCount => Interlocked.Read(ref _suppressedCount);

        /// <summary>
        /// Every statement the gateway has generated or dispatched since the workspace
        /// opened, suppressed or not.
        /// <para/>
        /// Counted unconditionally because it is the one measurement that cannot be
        /// recovered afterwards: the trace is capped and suppressible, so a number
        /// incremented here is the only record of how much SQL a run actually emitted.
        /// </summary>
        public long StatementCount => Interlocked.Read(ref _statementCount);

        private void OnSqlTraced(string sql)
        {
            Interlocked.Increment(ref _statementCount);

            if (_suppressTrace)
            {
                Interlocked.Increment(ref _suppressedCount);
                return;
            }

            Publish(DateTime.Now.ToString("HH:mm:ss.fff") + "  " + Flatten(sql));
        }

        private void Publish(string line)
        {
            lock (_traceSync)
            {
                _sqlTrace.Add(line);
                if (_sqlTrace.Count > MaxTraceEntries)
                    _sqlTrace.RemoveRange(0, _sqlTrace.Count - MaxTraceEntries);
            }

            SqlTraced?.Invoke(line);
        }

        /// <summary>Collapses a multi-line statement so it fits one trace row.</summary>
        private static string Flatten(string sql)
        {
            string flat = sql
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("\t", " ");

            while (flat.Contains("  "))
                flat = flat.Replace("  ", " ");

            return flat.Trim();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Disconnect();
        }
    }
}
