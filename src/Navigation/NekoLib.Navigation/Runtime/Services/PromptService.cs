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
    /// Modal, awaitable prompt service. Blocks background interaction (when an
    /// <see cref="IInteractionBlocker"/> is available) while a prompt is live and
    /// resolves the awaited <see cref="Task{TResult}"/> through the view's
    /// completion callback.
    /// </summary>
    public sealed class PromptService :
        IPromptService,
        INavigationDiagnosticsAware,
        INavigationRuntimeTeardownAware,
        INavigationInteractionBlockerAware
    {
        private sealed class PromptEntry
        {
            public IPageView View = null!;
            public Action Cancel = null!;
            public SurfaceTraceScope? Trace;
            public bool BlockerTracked;
        }

        private readonly IViewHost _viewHost;
        private readonly PageFactory _factory;
        private readonly IInteractionBlocker? _interactionBlocker;
        private readonly object _sync = new object();

        private readonly List<PromptEntry> _entries = new List<PromptEntry>();
        private NavigationDiagnostics? _diagnostics;
        private IPageAwareInteractionBlocker? _pageAwareInteractionBlocker;
        private bool _blockerReferenceHeld;

        public PromptService(
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

        public async Task<TResult?> ShowPromptAsync<TPrompt, TResult>(
            object? payload = null)
            where TPrompt : class, IPromptView<TResult>
        {
            var prompt = _factory.Create<TPrompt>();
            var tcs = new TaskCompletionSource<TResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var entry = new PromptEntry
            {
                View = prompt,
                Cancel = () => tcs.TrySetResult(default)
            };

            try
            {
                lock (_sync)
                {
                    entry.Trace = SurfaceTraceScope.Begin(
                        _diagnostics,
                        NavigationTraceSurfaceKinds.Prompt,
                        typeof(TPrompt),
                        _entries.Count + 1);

                    var acquireBlocker =
                        _entries.Count == 0 &&
                        _interactionBlocker != null;
                    _entries.Add(entry);

                    if (acquireBlocker)
                    {
                        // Publish the entry and provisional ownership before the
                        // platform callback, which is allowed to reenter.
                        _blockerReferenceHeld = true;
                        try
                        {
                            _interactionBlocker!.Block();
                        }
                        catch
                        {
                            if (_blockerReferenceHeld)
                                _blockerReferenceHeld = false;
                            throw;
                        }
                    }
                }

                prompt.BindCompletion(result => Complete(prompt, tcs, result));

                if (!Contains(prompt))
                    return await tcs.Task;

                _viewHost.AddView(prompt.NativeView);

                if (!Contains(prompt))
                    return await tcs.Task;

                TrackView(entry, isModalSurface: true);

                if (!Contains(prompt))
                    return await tcs.Task;

                _viewHost.BringToFront(prompt.NativeView);
                _viewHost.Focus(prompt.NativeView);

                await prompt.OnShownAsync(payload);
                entry.Trace?.Opened();
            }
            catch (Exception ex)
            {
                var cleanupError = RollbackSetup(entry);
                entry.Trace?.Failed(NavigationTraceCloseReasons.SetupFailed, ex);
                if (cleanupError != null)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[PromptService] Setup rollback failed: " +
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

            PromptEntry[] snapshot;
            bool unblock;

            lock (_sync)
            {
                if (_entries.Count == 0)
                    return;

                snapshot = _entries.ToArray();
                _entries.Clear();
                unblock = ClaimBlockerRelease();
            }

            var errors = new Exception?[snapshot.Length];
            Exception? firstError = null;

            // Claim every default result before any native/user teardown.
            for (int i = 0; i < snapshot.Length; i++)
            {
                errors[i] = SurfaceCleanup.Run(
                    errors[i],
                    snapshot[i].Cancel);
                if (firstError == null)
                    firstError = errors[i];
            }

            for (int i = 0; i < snapshot.Length; i++)
            {
                errors[i] = MergeFirst(
                    errors[i],
                    TeardownView(snapshot[i]));
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

        private void Complete<TResult>(
            IPromptView<TResult> view,
            TaskCompletionSource<TResult?> tcs,
            TResult? result)
        {
            PromptEntry? owned;
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

        private bool Contains(IPageView view)
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

        private Exception? RollbackSetup(PromptEntry entry)
        {
            PromptEntry? owned;
            bool unblock;

            lock (_sync)
            {
                owned = RemoveEntry(entry.View);
                unblock =
                    owned != null &&
                    _entries.Count == 0 &&
                    ClaimBlockerRelease();
            }

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

        private Exception? TeardownView(PromptEntry entry)
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
            PromptEntry entry,
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

        private static Exception? MergeFirst(
            Exception? first,
            Exception? next)
        {
            return first ?? next;
        }

        private PromptEntry? RemoveEntry(IPageView view)
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
