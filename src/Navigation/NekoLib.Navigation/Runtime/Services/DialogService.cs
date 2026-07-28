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
    /// Modal, awaitable dialog service. Mirrors <see cref="PromptService"/> semantics
    /// but is constrained to a boolean outcome.
    /// </summary>
    public sealed class DialogService :
        IDialogService,
        INavigationDiagnosticsAware,
        INavigationRuntimeTeardownAware,
        INavigationInteractionBlockerAware
    {
        private sealed class DialogEntry
        {
            public IDialogView View = null!;
            public TaskCompletionSource<bool> Tcs = null!;
            public SurfaceTraceScope? Trace;
            public bool BlockerTracked;
        }

        private readonly IViewHost _viewHost;
        private readonly PageFactory _factory;
        private readonly IInteractionBlocker? _interactionBlocker;
        private readonly object _sync = new object();

        private readonly List<DialogEntry> _entries = new List<DialogEntry>();
        private NavigationDiagnostics? _diagnostics;
        private IPageAwareInteractionBlocker? _pageAwareInteractionBlocker;
        private bool _blockerReferenceHeld;

        public DialogService(
            IViewHost viewHost,
            PageFactory factory,
            IInteractionBlocker? interactionBlocker = null)
        {
            _viewHost = viewHost ?? throw new ArgumentNullException(nameof(viewHost));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _interactionBlocker = interactionBlocker;
            _pageAwareInteractionBlocker =
                interactionBlocker as IPageAwareInteractionBlocker;
        }

        public async Task<bool> ShowDialogAsync<TDialog>(
            object? payload = null)
            where TDialog : class, IDialogView
        {
            var dialog = _factory.Create<TDialog>();
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var entry = new DialogEntry { View = dialog, Tcs = tcs };

            try
            {
                lock (_sync)
                {
                    entry.Trace = SurfaceTraceScope.Begin(
                        _diagnostics,
                        NavigationTraceSurfaceKinds.Dialog,
                        typeof(TDialog),
                        _entries.Count + 1);

                    var acquireBlocker =
                        _entries.Count == 0 &&
                        _interactionBlocker != null;
                    _entries.Add(entry);

                    if (acquireBlocker)
                    {
                        // Publish both the entry and provisional ownership before
                        // invoking platform code: Block may reenter this service.
                        _blockerReferenceHeld = true;
                        try
                        {
                            _interactionBlocker!.Block();
                        }
                        catch
                        {
                            // A reentrant CloseAll may already have claimed it.
                            if (_blockerReferenceHeld)
                                _blockerReferenceHeld = false;
                            throw;
                        }
                    }
                }

                dialog.BindCompletion(result => Complete(dialog, tcs, result));

                // A custom view may complete while binding the callback. Do not
                // attach a view that the completion path already reclaimed.
                if (!Contains(dialog))
                    return await tcs.Task;

                _viewHost.AddView(dialog.NativeView);

                if (!Contains(dialog))
                    return await tcs.Task;

                TrackView(entry, isModalSurface: true);

                if (!Contains(dialog))
                    return await tcs.Task;

                _viewHost.BringToFront(dialog.NativeView);
                _viewHost.Focus(dialog.NativeView);

                await dialog.OnShownAsync(payload!);
                entry.Trace?.Opened();
            }
            catch (Exception ex)
            {
                var cleanupError = RollbackSetup(entry);
                entry.Trace?.Failed(NavigationTraceCloseReasons.SetupFailed, ex);
                if (cleanupError != null)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[DialogService] Setup rollback failed: " +
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

            DialogEntry[] snapshot;
            bool unblock;

            lock (_sync)
            {
                if (_entries.Count == 0)
                    return;

                snapshot = _entries.ToArray();
                _entries.Clear();
                unblock = ClaimBlockerRelease();
            }

            // Claim every framework-owned result before any native/user teardown.
            // A Dispose implementation may invoke its callback reentrantly.
            foreach (var entry in snapshot)
                entry.Tcs.TrySetResult(false);

            var errors = new Exception?[snapshot.Length];
            Exception? firstError = null;

            for (int i = 0; i < snapshot.Length; i++)
            {
                errors[i] = TeardownView(snapshot[i]);
                if (firstError == null)
                    firstError = errors[i];
            }

            var unblockError = unblock
                ? TryUnblock()
                : null;
            if (firstError == null)
                firstError = unblockError;

            for (int i = 0; i < snapshot.Length; i++)
            {
                var error = errors[i] ?? unblockError;
                if (error == null)
                    snapshot[i].Trace?.Closed(closeReason);
                else
                    snapshot[i].Trace?.Failed(closeReason, error);
            }

            SurfaceCleanup.Rethrow(firstError);
        }

        private void Complete(IDialogView view, TaskCompletionSource<bool> tcs, bool result)
        {
            DialogEntry? owned;
            bool unblock;

            lock (_sync)
            {
                owned = RemoveEntry(view);
                unblock =
                    owned != null &&
                    _entries.Count == 0 &&
                    ClaimBlockerRelease();
            }

            // If CloseAll already reclaimed this entry, its cancellation owns
            // the awaiter and this duplicate callback is a no-op.
            if (owned != null)
            {
                var cleanupError = TeardownView(owned);

                if (unblock)
                {
                    cleanupError = SurfaceCleanup.Run(
                        cleanupError,
                        () => _interactionBlocker?.Unblock());
                }

                if (cleanupError == null)
                {
                    owned.Trace?.Closed(
                        NavigationTraceCloseReasons.CompletedByView);
                }
                else
                {
                    owned.Trace?.Failed(
                        NavigationTraceCloseReasons.CompletedByView,
                        cleanupError);
                    tcs.TrySetException(cleanupError);
                    return;
                }

                tcs.TrySetResult(result);
                return;
            }
        }

        private bool Contains(IDialogView view)
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

        private Exception? RollbackSetup(DialogEntry entry)
        {
            DialogEntry? owned;
            bool unblock;

            lock (_sync)
            {
                owned = RemoveEntry(entry.View);
                unblock =
                    owned != null &&
                    _entries.Count == 0 &&
                    ClaimBlockerRelease();
            }

            // If Block itself failed, the entry was never inserted, but the
            // factory result is still owned by this call.
            Exception? cleanupError = null;
            if (owned != null || !entry.View.IsDisposed)
                cleanupError = TeardownView(owned ?? entry);

            if (unblock)
            {
                cleanupError = SurfaceCleanup.Run(
                    cleanupError,
                    () => _interactionBlocker?.Unblock());
            }

            return cleanupError;
        }

        private Exception? TeardownView(DialogEntry entry)
        {
            Exception? firstError = null;
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

        private Exception? TryUnblock()
        {
            return SurfaceCleanup.Run(
                null,
                () => _interactionBlocker?.Unblock());
        }

        // Must be called while holding _sync.
        private bool ClaimBlockerRelease()
        {
            if (!_blockerReferenceHeld)
                return false;

            _blockerReferenceHeld = false;
            return true;
        }

        private void TrackView(
            DialogEntry entry,
            bool isModalSurface)
        {
            var blocker = _pageAwareInteractionBlocker;
            if (blocker == null)
                return;

            // Set first so a partially-mutating implementation that throws is
            // still balanced by setup rollback.
            entry.BlockerTracked = true;
            blocker.OnViewAdded(
                entry.View.NativeView,
                isModalSurface);
        }

        private DialogEntry? RemoveEntry(IDialogView view)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (ReferenceEquals(_entries[i].View, view))
                {
                    var entry = _entries[i];
                    _entries.RemoveAt(i);
                    return entry;
                }
            }

            return null;
        }
    }
}
