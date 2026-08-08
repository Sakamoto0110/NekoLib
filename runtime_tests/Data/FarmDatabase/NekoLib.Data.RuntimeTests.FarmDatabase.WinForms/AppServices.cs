using System;
using System.IO;
using NekoLib.Core.Logging;
using NekoLib.Logging;
using NekoLib.Logging.Sinks;
using NekoLib.Telemetry;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.Simulation;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.WinForms
{
    /// <summary>
    /// Process-wide access to the workspace and the view-models.
    /// <para/>
    /// This is a static locator rather than constructor injection, and that is forced
    /// by two requirements pulling in opposite directions. The WinForms designer can
    /// only instantiate a type through a parameterless constructor, so a page cannot
    /// take its dependencies as constructor arguments and still be designable.
    /// Navigation's <c>PageFactory</c> agrees - its fallback path is exactly a
    /// default-constructor call. Pages therefore pull what they need, and they pull
    /// it lazily so that a designer-hosted instance never touches a database.
    /// </summary>
    public static class AppServices
    {
        private static FarmWorkspace _workspace;
        private static Logger _logger;

        /// <summary>
        /// Where the simulation's measurements are written. Beside the databases rather
        /// than inside the repository, for the same reason they are.
        /// </summary>
        public static string MetricsLogPath { get; private set; }

        /// <summary>Live operation timings, bounded in memory. Null before <see cref="Start"/>.</summary>
        public static TelemetryPipeline Telemetry { get; private set; }

        /// <summary>
        /// True once <see cref="Start"/> has run. Pages check this before touching
        /// anything: at design time it is false, and it must stay harmless.
        /// </summary>
        public static bool IsRunning => _workspace != null;

        public static FarmWorkspace Workspace =>
            _workspace ?? throw new InvalidOperationException(
                "AppServices.Start() não foi chamado.");

        public static ViewModels ViewModelsBundle { get; private set; }

        /// <summary>Creates the workspace and the shared view-models.</summary>
        public static IDisposable Start()
        {
            if (_workspace != null)
                throw new InvalidOperationException("AppServices já foi iniciado.");

            _workspace = new FarmWorkspace();

            // Rolling file for the history, bounded telemetry for the live window.
            // Neither can do the other's job: the sink persists but keeps no summary,
            // and the pipeline summarises but does not persist - "no persistence in v1"
            // is its documented boundary.
            //
            // The sink opens and closes the file per entry, so nothing writes to it per
            // tick. SimMetrics accumulates in memory and emits one rolled-up line per
            // window.
            MetricsLogPath = Path.Combine(_workspace.RootDirectory, "simulacao.log");

            var file = new RollingFileLogSink(new RollingFileLogSinkOptions
            {
                FilePath = MetricsLogPath,
                MaximumFileBytes = 2 * 1024 * 1024,
                RetainedFileCount = 4
            });

            _logger = new Logger(
                new LoggerOptions { MinimumLevel = LogLevel.Info, DisposeSinks = true },
                file);

            Telemetry = new TelemetryPipeline(new TelemetryPipelineOptions
            {
                RecentOperationCapacity = 256
            });

            var metrics = new SimMetrics(_logger, Telemetry, TimeSpan.FromSeconds(10));

            ViewModelsBundle = new ViewModels(_workspace, metrics);

            // Every view-model re-reads connection-derived state from one place, so a
            // connect or disconnect on the Connection page is immediately visible to
            // pages that are not even attached yet.
            _workspace.ConnectionChanged += ViewModelsBundle.NotifyConnectionChanged;

            return new Shutdown();
        }

        private sealed class Shutdown : IDisposable
        {
            public void Dispose()
            {
                if (_workspace == null) return;

                _workspace.ConnectionChanged -= ViewModelsBundle.NotifyConnectionChanged;
                _workspace.Dispose();
                _workspace = null;
                ViewModelsBundle = null;

                // Disposing the logger flushes and then disposes the sink, because the
                // options asked it to own them. Anything still buffered reaches the file
                // here rather than being lost with the process.
                if (_logger != null)
                {
                    _logger.Dispose();
                    _logger = null;
                }

                Telemetry = null;
            }
        }
    }

    /// <summary>
    /// The view-models, created once and shared by the pages that display them.
    /// They outlive individual page instances on purpose: a page can be transient and
    /// still show the same table selection when the user navigates back to it.
    /// </summary>
    public sealed class ViewModels
    {
        public ViewModels(FarmWorkspace workspace, SimMetrics metrics)
        {
            Connection = new Core.ViewModels.ConnectionViewModel(workspace);
            Browse = new Core.ViewModels.BrowseViewModel(workspace);
            RawQuery = new Core.ViewModels.RawQueryViewModel(workspace);
            Stock = new Core.ViewModels.StockViewModel(workspace);
            Log = new Core.ViewModels.LogViewModel(workspace);
            Simulation = new Core.ViewModels.SimulationViewModel(workspace, metrics);
        }

        public Core.ViewModels.ConnectionViewModel Connection { get; }
        public Core.ViewModels.BrowseViewModel Browse { get; }
        public Core.ViewModels.RawQueryViewModel RawQuery { get; }
        public Core.ViewModels.StockViewModel Stock { get; }
        public Core.ViewModels.LogViewModel Log { get; }
        public Core.ViewModels.SimulationViewModel Simulation { get; }

        public void NotifyConnectionChanged()
        {
            Connection.OnConnectionChanged();
            Browse.OnConnectionChanged();
            RawQuery.OnConnectionChanged();
            Stock.OnConnectionChanged();
            Log.OnConnectionChanged();
            Simulation.OnConnectionChanged();
        }
    }
}
