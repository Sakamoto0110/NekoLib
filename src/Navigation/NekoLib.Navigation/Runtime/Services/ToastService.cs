using System;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Contracts.Runtime;
using NekoLib.Navigation.Runtime.Factories;

namespace NekoLib.Navigation.Runtime.Services
{
    /// <summary>
    /// Fire-and-forget toast service. Keeps at most one toast on screen.
    /// New calls cancel the previous timer and replace the live view to avoid leaks.
    /// </summary>
    public sealed class ToastService : IToastService
    {
        private readonly IViewHost _viewHost;
        private readonly PageFactory _factory;
        private readonly IEventDispatcherAdapter _dispatcher;
        private readonly object _sync = new object();

        private IToastView _currentToast;
        private CancellationTokenSource _currentCts;

        public ToastService(IViewHost viewHost, PageFactory factory, IEventDispatcherAdapter dispatcher = null)
        {
            _viewHost = viewHost ?? throw new ArgumentNullException(nameof(viewHost));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _dispatcher = dispatcher;
        }

        public void ShowToast<TToast>(object payload = null, int durationMs = 3000)
            where TToast : class, IToastView
        {
            IToastView nextToast;
            CancellationTokenSource nextCts;

            lock (_sync)
            {
                DismissCurrentInternal();

                nextToast = _factory.Create<TToast>();
                nextCts = new CancellationTokenSource();

                _currentToast = nextToast;
                _currentCts = nextCts;
            }

            nextToast.BindDismiss(DismissCurrentToast);

            _viewHost.AddView(nextToast.NativeView);
            _viewHost.BringToFront(nextToast.NativeView);

            nextToast.OnShown(payload);

            // Fire-and-forget auto-dismiss timer.
            _ = RunDismissTimerAsync(nextToast, nextCts.Token, durationMs);
        }

        public void DismissCurrentToast()
        {
            lock (_sync)
            {
                DismissCurrentInternal();
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
                _dispatcher.BeginInvoke(() => DismissIfCurrent(toast));
            else
                DismissIfCurrent(toast);
        }

        private void DismissIfCurrent(IToastView toast)
        {
            lock (_sync)
            {
                if (!ReferenceEquals(_currentToast, toast))
                    return;

                DismissCurrentInternal();
            }
        }

        private void DismissCurrentInternal()
        {
            if (_currentToast == null)
                return;

            var toRemove = _currentToast;
            var cts = _currentCts;

            _currentToast = null;
            _currentCts = null;

            try { cts?.Cancel(); } catch { }
            try { cts?.Dispose(); } catch { }

            try { _viewHost.RemoveView(toRemove.NativeView); } catch { }

            if (!toRemove.IsDisposed)
            {
                try { toRemove.Dispose(); } catch { }
            }
        }
    }
}
