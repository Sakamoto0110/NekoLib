using System;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Contracts.Runtime;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Runtime.Factories;

namespace NekoLib.Navigation.Runtime.Services
{
    /// <summary>
    /// Fire-and-forget toast service. Keeps at most one toast on screen.
    /// New calls cancel the previous timer and replace the live view to avoid leaks.
    /// </summary>
    public sealed class ToastService :
        IToastService,
        INavigationDiagnosticsAware,
        INavigationRuntimeTeardownAware,
        INavigationInteractionBlockerAware
    {
        private readonly IViewHost _viewHost;
        private readonly PageFactory _factory;
        private readonly IEventDispatcherAdapter? _dispatcher;
        private readonly object _sync = new object();

        private IToastView? _currentToast;
        private CancellationTokenSource? _currentCts;
        private SurfaceTraceScope? _currentTrace;
        private NavigationDiagnostics? _diagnostics;
        private IPageAwareInteractionBlocker? _pageAwareInteractionBlocker;
        private bool _currentBlockerTracked;

        public ToastService(
            IViewHost viewHost,
            PageFactory factory,
            IEventDispatcherAdapter? dispatcher = null)
        {
            _viewHost = viewHost ?? throw new ArgumentNullException(nameof(viewHost));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _dispatcher = dispatcher;
        }

        public void ShowToast<TToast>(
            object? payload = null,
            int durationMs = 3000)
            where TToast : class, IToastView
        {
            if (durationMs < Timeout.Infinite)
                throw new ArgumentOutOfRangeException(nameof(durationMs));

            var nextToast = _factory.Create<TToast>();
            var nextCts = new CancellationTokenSource();
            var nextToken = nextCts.Token;
            SurfaceTraceScope? nextTrace;
            Exception? replacementError;

            lock (_sync)
            {
                replacementError = DismissCurrentInternal(
                    NavigationTraceCloseReasons.Replaced);

                if (replacementError != null)
                {
                    nextTrace = null;
                }
                else
                {
                    nextTrace = SurfaceTraceScope.Begin(
                        _diagnostics,
                        NavigationTraceSurfaceKinds.Toast,
                        typeof(TToast),
                        1);
                    _currentToast = nextToast;
                    _currentCts = nextCts;
                    _currentTrace = nextTrace;
                    _currentBlockerTracked = false;
                }
            }

            if (replacementError != null)
            {
                try { nextCts.Dispose(); } catch { }
                if (!nextToast.IsDisposed)
                {
                    try { nextToast.Dispose(); } catch { }
                }

                SurfaceCleanup.Rethrow(replacementError);
            }

            try
            {
                // Bind the callback to this concrete instance. A delayed callback
                // from a toast that has already been replaced must not dismiss the
                // newer toast.
                nextToast.BindDismiss(() => DismissIfCurrentSafely(
                    nextToast,
                    NavigationTraceCloseReasons.DismissedByView));

                // A custom BindDismiss implementation may invoke the callback
                // synchronously. In that case the toast has already been reclaimed
                // and must not subsequently be added to the host.
                if (!IsCurrent(nextToast))
                    return;

                _viewHost.AddView(nextToast.NativeView);

                if (!IsCurrent(nextToast))
                    return;

                TrackCurrentView(nextToast, isModalSurface: false);

                if (!IsCurrent(nextToast))
                    return;

                _viewHost.BringToFront(nextToast.NativeView);

                nextToast.OnShown(payload!);

                // OnShown is allowed to dismiss synchronously. In that case its CTS
                // has already been reclaimed and there is no timer left to start.
                if (!IsCurrent(nextToast))
                    return;

                nextTrace?.Opened();

                // Fire-and-forget auto-dismiss timer.
                _ = RunDismissTimerAsync(nextToast, nextToken, durationMs);
            }
            catch (Exception ex)
            {
                // AddView/BringToFront/OnShown may fail after partially mutating the
                // native host. Reclaim only this toast; a concurrent replacement
                // remains owned by the service.
                var cleanupError = DismissIfCurrentInternal(
                    nextToast,
                    NavigationTraceCloseReasons.SetupFailed,
                    ex);
                nextTrace?.Failed(NavigationTraceCloseReasons.SetupFailed, ex);
                if (cleanupError != null)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[ToastService] Setup rollback failed: " +
                        cleanupError);
                }
                throw;
            }
        }

        public void DismissCurrentToast()
        {
            Exception? cleanupError;
            lock (_sync)
            {
                cleanupError = DismissCurrentInternal(
                    NavigationTraceCloseReasons.DismissedByService);
            }

            SurfaceCleanup.Rethrow(cleanupError);
        }

        void INavigationDiagnosticsAware.AttachDiagnostics(NavigationDiagnostics diagnostics)
        {
            if (diagnostics == null)
                throw new ArgumentNullException(nameof(diagnostics));

            lock (_sync)
            {
                _diagnostics = diagnostics;
            }
        }

        void INavigationRuntimeTeardownAware.TeardownForRuntime(string closeReason)
        {
            if (closeReason == null)
                throw new ArgumentNullException(nameof(closeReason));

            Exception? cleanupError;
            lock (_sync)
            {
                cleanupError = DismissCurrentInternal(closeReason);
            }

            SurfaceCleanup.Rethrow(cleanupError);
        }

        void INavigationInteractionBlockerAware.AttachInteractionBlocker(
            IPageAwareInteractionBlocker interactionBlocker)
        {
            if (interactionBlocker == null)
                throw new ArgumentNullException(nameof(interactionBlocker));

            lock (_sync)
            {
                if (_pageAwareInteractionBlocker == null)
                    _pageAwareInteractionBlocker = interactionBlocker;
            }
        }

        private async Task RunDismissTimerAsync(IToastView toast, CancellationToken token, int durationMs)
        {
            // Avoid Task.Delay(ms, token) — when the token is cancelled (e.g. rapid-fire
            // ShowToast calls supersede a live toast), Task.Delay throws TaskCanceled-
            // Exception. The throw is caught here, but the debugger logs it as a
            // first-chance exception and pollutes the output. Use a non-throwing
            // wait-or-cancel pattern instead.
            var cancelTcs = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            using (token.Register(() => cancelTcs.TrySetResult(true)))
            {
                var delay = Task.Delay(durationMs);
                var completed = await Task.WhenAny(delay, cancelTcs.Task).ConfigureAwait(false);
                if (completed != delay)
                    return; // superseded / disposed — silent exit, no exception
            }

            // The timer continuation runs on the thread pool; RemoveView/Dispose touch
            // native UI, so marshal the dismissal back to the UI thread (A-2).
            if (_dispatcher != null)
            {
                _dispatcher.BeginInvoke(() => DismissIfCurrentSafely(
                    toast,
                    NavigationTraceCloseReasons.Timeout));
            }
            else
            {
                DismissIfCurrentSafely(
                    toast,
                    NavigationTraceCloseReasons.Timeout);
            }
        }

        private void DismissIfCurrentSafely(
            IToastView toast,
            string closeReason)
        {
            var cleanupError = DismissIfCurrentInternal(
                toast,
                closeReason);
            if (cleanupError != null)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[ToastService] Timed dismissal failed: " +
                    cleanupError);
            }
        }

        private Exception? DismissIfCurrentInternal(
            IToastView toast,
            string closeReason,
            Exception? terminalError = null)
        {
            lock (_sync)
            {
                if (!ReferenceEquals(_currentToast, toast))
                    return null;

                return DismissCurrentInternal(
                    closeReason,
                    terminalError);
            }
        }

        private bool IsCurrent(IToastView toast)
        {
            lock (_sync)
            {
                return ReferenceEquals(_currentToast, toast);
            }
        }

        private Exception? DismissCurrentInternal(
            string closeReason,
            Exception? terminalError = null)
        {
            if (_currentToast == null)
                return null;

            var toRemove = _currentToast;
            var cts = _currentCts;
            var trace = _currentTrace;
            var blockerTracked = _currentBlockerTracked;

            _currentToast = null;
            _currentCts = null;
            _currentTrace = null;
            _currentBlockerTracked = false;

            Exception? cleanupError = null;
            cleanupError = SurfaceCleanup.Run(
                cleanupError,
                () => cts?.Cancel());
            cleanupError = SurfaceCleanup.Run(
                cleanupError,
                () => cts?.Dispose());

            cleanupError = SurfaceCleanup.Run(
                cleanupError,
                () => _viewHost.RemoveView(toRemove.NativeView));

            if (blockerTracked)
            {
                cleanupError = SurfaceCleanup.Run(
                    cleanupError,
                    () => _pageAwareInteractionBlocker?.OnViewRemoved(
                        toRemove.NativeView));
            }

            if (!toRemove.IsDisposed)
            {
                cleanupError = SurfaceCleanup.Run(
                    cleanupError,
                    toRemove.Dispose);
            }

            var traceError = terminalError ?? cleanupError;
            if (traceError == null)
                trace?.Closed(closeReason);
            else
                trace?.Failed(closeReason, traceError);

            return cleanupError;
        }

        private void TrackCurrentView(
            IToastView toast,
            bool isModalSurface)
        {
            lock (_sync)
            {
                if (!ReferenceEquals(_currentToast, toast) ||
                    _pageAwareInteractionBlocker == null)
                {
                    return;
                }

                _currentBlockerTracked = true;
                _pageAwareInteractionBlocker.OnViewAdded(
                    toast.NativeView,
                    isModalSurface);
            }
        }
    }
}
