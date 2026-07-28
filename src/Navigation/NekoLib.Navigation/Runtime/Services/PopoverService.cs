using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Contracts.Runtime;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Runtime.Factories;

namespace NekoLib.Navigation.Runtime.Services
{
    /// <summary>
    /// Non-blocking overlay service. Mirrors <see cref="DialogService"/> but never
    /// engages an <see cref="IInteractionBlocker"/>, and wires every popover whose
    /// view implements <see cref="IUnfocusAware"/> to the platform's
    /// <see cref="IFocusObserverAdapter"/> so it can auto-dismiss on focus loss.
    /// </summary>
    public sealed class PopoverService :
        IPopoverService,
        INavigationDiagnosticsAware,
        INavigationRuntimeTeardownAware,
        INavigationInteractionBlockerAware
    {
        private sealed class PopoverEntry
        {
            public IPopoverView View = null!;
            public TaskCompletionSource<bool> Tcs = null!;
            public IDisposable? FocusSubscription;
            public SurfaceTraceScope? Trace;
            public string? PendingCloseReason;
            public bool BlockerTracked;
        }

        private readonly IViewHost _viewHost;
        private readonly PageFactory _factory;
        private readonly IFocusObserverAdapter? _focusObserver;
        private readonly object _sync = new object();

        private readonly List<PopoverEntry> _entries = new List<PopoverEntry>();
        private NavigationDiagnostics? _diagnostics;
        private IPageAwareInteractionBlocker? _pageAwareInteractionBlocker;

        public PopoverService(
            IViewHost viewHost,
            PageFactory factory,
            IFocusObserverAdapter? focusObserver = null)
        {
            _viewHost = viewHost ?? throw new ArgumentNullException(nameof(viewHost));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _focusObserver = focusObserver;
        }

        public async Task<bool> ShowPopoverAsync<TPopover>(
            object? payload = null)
            where TPopover : class, IPopoverView
        {
            var popover = _factory.Create<TPopover>();
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var entry = new PopoverEntry { View = popover, Tcs = tcs };

            try
            {
                lock (_sync)
                {
                    entry.Trace = SurfaceTraceScope.Begin(
                        _diagnostics,
                        NavigationTraceSurfaceKinds.Popover,
                        typeof(TPopover),
                        _entries.Count + 1);
                    _entries.Add(entry);
                }

                popover.BindCompletion(result => Complete(popover, tcs, result));

                if (!Contains(popover))
                    return await tcs.Task;

                _viewHost.AddView(popover.NativeView);

                if (!Contains(popover))
                    return await tcs.Task;

                TrackView(entry, isModalSurface: false);

                if (!Contains(popover))
                    return await tcs.Task;

                _viewHost.BringToFront(popover.NativeView);
                _viewHost.Focus(popover.NativeView);

            // Wire unfocus AFTER focusing the view so the focus call itself doesn't
            // immediately fire the observer's LostFocus on whatever was focused
            // before. Only views that opt in via IUnfocusAware get the hook.
                if (_focusObserver != null && popover is IUnfocusAware unfocusAware)
                {
                    var subscription = _focusObserver.Track(
                        popover.NativeView,
                        () =>
                        {
                        // Marshal nothing — observer adapter is expected to deliver
                        // on the UI thread. View decides whether to dismiss.
                            _ = SafeUnfocusAsync(entry, unfocusAware);
                        });
                    entry.FocusSubscription = subscription;

                    // A focus observer may synchronously report unfocus from Track.
                    // Complete() then owns teardown before the subscription is
                    // assigned, so release the late result here.
                    if (!Contains(popover))
                    {
                        try { subscription?.Dispose(); } catch { }
                        return await tcs.Task;
                    }
                }

                await popover.OnShownAsync(payload!);
                entry.Trace?.Opened();
            }
            catch (Exception ex)
            {
                var cleanupError = RollbackSetup(entry);
                entry.Trace?.Failed(NavigationTraceCloseReasons.SetupFailed, ex);
                if (cleanupError != null)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[PopoverService] Setup rollback failed: " +
                        cleanupError);
                }
                throw;
            }

            return await tcs.Task;
        }

        public void CloseAll()
            => CloseAll(NavigationTraceCloseReasons.ClosedByService);

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
            => CloseAll(closeReason);

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

        private void CloseAll(string closeReason)
        {
            if (closeReason == null)
                throw new ArgumentNullException(nameof(closeReason));

            PopoverEntry[] snapshot;

            lock (_sync)
            {
                if (_entries.Count == 0)
                    return;

                snapshot = _entries.ToArray();
                _entries.Clear();
            }

            // Claim every framework-owned result before focus/native teardown.
            foreach (var entry in snapshot)
                entry.Tcs.TrySetResult(false);

            Exception? firstError = null;
            var errors = new Exception?[snapshot.Length];

            for (int i = 0; i < snapshot.Length; i++)
            {
                errors[i] = TeardownEntry(snapshot[i]);
                if (firstError == null)
                    firstError = errors[i];
            }

            for (int i = 0; i < snapshot.Length; i++)
            {
                if (errors[i] == null)
                    snapshot[i].Trace?.Closed(closeReason);
                else
                    snapshot[i].Trace?.Failed(closeReason, errors[i]!);
            }

            SurfaceCleanup.Rethrow(firstError);
        }

        private void Complete(IPopoverView view, TaskCompletionSource<bool> tcs, bool result)
        {
            PopoverEntry? owned = null;

            lock (_sync)
            {
                owned = RemoveEntry(view);
            }

            // If CloseAll already reclaimed this entry, its cancellation owns
            // the awaiter and this duplicate callback is a no-op.
            if (owned != null)
            {
                var closeReason =
                    owned.PendingCloseReason ??
                    NavigationTraceCloseReasons.CompletedByView;
                var cleanupError = TeardownEntry(owned);

                if (cleanupError == null)
                {
                    owned.Trace?.Closed(closeReason);
                    tcs.TrySetResult(result);
                }
                else
                {
                    owned.Trace?.Failed(closeReason, cleanupError);
                    tcs.TrySetException(cleanupError);
                }
                return;
            }
        }

        private Exception? TeardownEntry(PopoverEntry entry)
        {
            Exception? firstError = null;
            firstError = SurfaceCleanup.Run(
                firstError,
                () => entry.FocusSubscription?.Dispose());

            firstError = SurfaceCleanup.Run(
                firstError,
                () => _viewHost.RemoveView(entry.View.NativeView));

            if (entry.BlockerTracked)
            {
                entry.BlockerTracked = false;
                firstError = SurfaceCleanup.Run(
                    firstError,
                    () => _pageAwareInteractionBlocker?.OnViewRemoved(
                        entry.View.NativeView));
            }

            if (!entry.View.IsDisposed)
            {
                firstError = SurfaceCleanup.Run(
                    firstError,
                    entry.View.Dispose);
            }

            return firstError;
        }

        private bool Contains(IPopoverView view)
        {
            lock (_sync)
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (ReferenceEquals(_entries[i].View, view))
                        return true;
                }
            }

            return false;
        }

        private Exception? RollbackSetup(PopoverEntry entry)
        {
            PopoverEntry? owned;

            lock (_sync)
            {
                owned = RemoveEntry(entry.View);
            }

            if (owned != null || !entry.View.IsDisposed)
                return TeardownEntry(owned ?? entry);

            return null;
        }

        private void TrackView(
            PopoverEntry entry,
            bool isModalSurface)
        {
            var blocker = _pageAwareInteractionBlocker;
            if (blocker == null)
                return;

            entry.BlockerTracked = true;
            blocker.OnViewAdded(
                entry.View.NativeView,
                isModalSurface);
        }

        private PopoverEntry? RemoveEntry(IPopoverView view)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (ReferenceEquals(_entries[i].View, view))
                {
                    var e = _entries[i];
                    _entries.RemoveAt(i);
                    return e;
                }
            }

            return null;
        }

        private async Task SafeUnfocusAsync(
            PopoverEntry entry,
            IUnfocusAware view)
        {
            lock (_sync)
            {
                if (!ContainsEntry(entry))
                    return;

                entry.PendingCloseReason = NavigationTraceCloseReasons.FocusLoss;
            }

            try
            {
                await view.OnUnfocusAsync();
            }
            catch
            {
                /* never break navigation */
            }
            finally
            {
                lock (_sync)
                {
                    if (ContainsEntry(entry) &&
                        entry.PendingCloseReason == NavigationTraceCloseReasons.FocusLoss)
                    {
                        entry.PendingCloseReason = null;
                    }
                }
            }
        }

        private bool ContainsEntry(PopoverEntry entry)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (ReferenceEquals(_entries[i], entry))
                    return true;
            }

            return false;
        }
    }
}
