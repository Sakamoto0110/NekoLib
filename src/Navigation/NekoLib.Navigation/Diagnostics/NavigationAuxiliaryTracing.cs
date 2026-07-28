#nullable enable
using System;
using System.Diagnostics;
using System.Threading;

namespace NekoLib.Navigation.Diagnostics
{
    /// <summary>
    /// Internal opt-in seam used by runtime-owned services without changing their
    /// public constructors or adding diagnostics to the public service contracts.
    /// </summary>
    internal interface INavigationDiagnosticsAware
    {
        void AttachDiagnostics(NavigationDiagnostics diagnostics);
    }

    /// <summary>
    /// Lets the runtime distinguish an explicit reset from final runtime teardown.
    /// </summary>
    internal interface INavigationRuntimeTeardownAware
    {
        void TeardownForRuntime(string closeReason);
    }

    internal static class NavigationTraceSurfaceKinds
    {
        public const string Toast = "Toast";
        public const string Dialog = "Dialog";
        public const string Prompt = "Prompt";
        public const string Popover = "Popover";
    }

    internal static class NavigationTraceCloseReasons
    {
        public const string Timeout = "Timeout";
        public const string Replaced = "Replaced";
        public const string DismissedByView = "DismissedByView";
        public const string CompletedByView = "CompletedByView";
        public const string DismissedByService = "DismissedByService";
        public const string ClosedByService = "ClosedByService";
        public const string FocusLoss = "FocusLoss";
        public const string SetupFailed = "SetupFailed";
        public const string Reset = "Reset";
        public const string Shutdown = "Shutdown";
        public const string RuntimeTeardown = "RuntimeTeardown";
        public const string NavigationRollback = "NavigationRollback";
    }

    /// <summary>
    /// Correlates the scalar lifecycle emitted for one transient surface. The
    /// terminal transition is guarded so replacement, callbacks and rollback
    /// races cannot publish both Closed and Failed.
    /// </summary>
    internal sealed class SurfaceTraceScope
    {
        private readonly NavigationDiagnostics _diagnostics;
        private readonly Stopwatch _watch;
        private readonly string _surfaceId;
        private readonly string _surfaceKind;
        private readonly string _surfaceType;
        private readonly int _surfaceDepth;

        private int _opened;
        private int _terminal;

        private SurfaceTraceScope(
            NavigationDiagnostics diagnostics,
            string surfaceKind,
            Type surfaceType,
            int surfaceDepth)
        {
            _diagnostics = diagnostics;
            _watch = Stopwatch.StartNew();
            _surfaceId = Guid.NewGuid().ToString("N");
            _surfaceKind = surfaceKind;
            _surfaceType = surfaceType.FullName ?? surfaceType.Name;
            _surfaceDepth = surfaceDepth;
        }

        public static SurfaceTraceScope? Begin(
            NavigationDiagnostics? diagnostics,
            string surfaceKind,
            Type surfaceType,
            int surfaceDepth)
        {
            if (diagnostics == null || !diagnostics.TraceEventsEnabled)
                return null;

            if (surfaceKind == null)
                throw new ArgumentNullException(nameof(surfaceKind));
            if (surfaceType == null)
                throw new ArgumentNullException(nameof(surfaceType));
            if (surfaceDepth <= 0)
                throw new ArgumentOutOfRangeException(nameof(surfaceDepth));

            var scope = new SurfaceTraceScope(
                diagnostics,
                surfaceKind,
                surfaceType,
                surfaceDepth);
            scope.Emit(NavigationTraceKind.SurfaceOpening);
            return scope;
        }

        public void Opened()
        {
            if (Volatile.Read(ref _terminal) != 0 ||
                Interlocked.Exchange(ref _opened, 1) != 0)
            {
                return;
            }

            Emit(
                NavigationTraceKind.SurfaceOpened,
                outcome: NavigationTraceOutcome.Succeeded,
                success: true);
        }

        public void Closed(string closeReason)
        {
            if (closeReason == null)
                throw new ArgumentNullException(nameof(closeReason));
            if (Interlocked.Exchange(ref _terminal, 1) != 0)
                return;

            Emit(
                NavigationTraceKind.SurfaceClosed,
                outcome: NavigationTraceOutcome.Succeeded,
                success: true,
                closeReason: closeReason);
        }

        public void Failed(string closeReason, Exception error)
        {
            if (closeReason == null)
                throw new ArgumentNullException(nameof(closeReason));
            if (error == null)
                throw new ArgumentNullException(nameof(error));
            if (Interlocked.Exchange(ref _terminal, 1) != 0)
                return;

            Emit(
                NavigationTraceKind.SurfaceFailed,
                outcome: NavigationTraceOutcome.Failed,
                success: false,
                closeReason: closeReason,
                errorType: error.GetType().FullName ?? error.GetType().Name);
        }

        private void Emit(
            NavigationTraceKind kind,
            NavigationTraceOutcome outcome = NavigationTraceOutcome.None,
            bool? success = null,
            string? closeReason = null,
            string? errorType = null)
        {
            _diagnostics.EmitTrace(new NavigationTraceEvent(
                kind,
                _diagnostics.RuntimeId,
                outcome: outcome,
                targetPage: _surfaceType,
                errorType: errorType,
                success: success,
                surfaceId: _surfaceId,
                surfaceKind: _surfaceKind,
                surfaceDepth: _surfaceDepth,
                closeReason: closeReason,
                timestampUtc: DateTime.UtcNow,
                elapsedMilliseconds: _watch.ElapsedMilliseconds));
        }
    }
}
