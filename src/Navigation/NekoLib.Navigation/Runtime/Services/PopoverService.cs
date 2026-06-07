using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Contracts.Runtime;
using NekoLib.Navigation.Runtime.Factories;

namespace NekoLib.Navigation.Runtime.Services
{
    /// <summary>
    /// Non-blocking overlay service. Mirrors <see cref="DialogService"/> but never
    /// engages an <see cref="IInteractionBlocker"/>, and wires every popover whose
    /// view implements <see cref="IUnfocusAware"/> to the platform's
    /// <see cref="IFocusObserverAdapter"/> so it can auto-dismiss on focus loss.
    /// </summary>
    public sealed class PopoverService : IPopoverService
    {
        private sealed class PopoverEntry
        {
            public IPopoverView View;
            public TaskCompletionSource<bool> Tcs;
            public IDisposable FocusSubscription;
        }

        private readonly IViewHost _viewHost;
        private readonly PageFactory _factory;
        private readonly IFocusObserverAdapter _focusObserver;
        private readonly object _sync = new object();

        private readonly List<PopoverEntry> _entries = new List<PopoverEntry>();

        public PopoverService(IViewHost viewHost, PageFactory factory, IFocusObserverAdapter focusObserver = null)
        {
            _viewHost = viewHost ?? throw new ArgumentNullException(nameof(viewHost));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _focusObserver = focusObserver;
        }

        public async Task<bool> ShowPopoverAsync<TPopover>(object payload = null)
            where TPopover : class, IPopoverView
        {
            var popover = _factory.Create<TPopover>();
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var entry = new PopoverEntry { View = popover, Tcs = tcs };

            popover.BindCompletion(result => Complete(popover, tcs, result));

            lock (_sync)
            {
                _entries.Add(entry);
            }

            _viewHost.AddView(popover.NativeView);
            _viewHost.BringToFront(popover.NativeView);
            _viewHost.Focus(popover.NativeView);

            // Wire unfocus AFTER focusing the view so the focus call itself doesn't
            // immediately fire the observer's LostFocus on whatever was focused
            // before. Only views that opt in via IUnfocusAware get the hook.
            if (_focusObserver != null && popover is IUnfocusAware unfocusAware)
            {
                entry.FocusSubscription = _focusObserver.Track(
                    popover.NativeView,
                    () =>
                    {
                        // Marshal nothing — observer adapter is expected to deliver
                        // on the UI thread. View decides whether to dismiss.
                        _ = SafeUnfocusAsync(unfocusAware);
                    });
            }

            await popover.OnShownAsync(payload);

            return await tcs.Task;
        }

        public void CloseAll()
        {
            PopoverEntry[] snapshot;

            lock (_sync)
            {
                if (_entries.Count == 0)
                    return;

                snapshot = _entries.ToArray();
                _entries.Clear();
            }

            foreach (var entry in snapshot)
            {
                TeardownEntry(entry);
                entry.Tcs.TrySetResult(false);
            }
        }

        private void Complete(IPopoverView view, TaskCompletionSource<bool> tcs, bool result)
        {
            PopoverEntry owned = null;

            lock (_sync)
            {
                owned = RemoveEntry(view);
            }

            // If CloseAll already reclaimed this entry, skip the duplicate teardown
            // and just resolve the (already-completed) awaiter.
            if (owned != null)
                TeardownEntry(owned);

            tcs.TrySetResult(result);
        }

        private void TeardownEntry(PopoverEntry entry)
        {
            try { entry.FocusSubscription?.Dispose(); } catch { }

            try { _viewHost.RemoveView(entry.View.NativeView); } catch { }

            if (!entry.View.IsDisposed)
            {
                try { entry.View.Dispose(); } catch { }
            }
        }

        private PopoverEntry RemoveEntry(IPopoverView view)
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

        private static async Task SafeUnfocusAsync(IUnfocusAware view)
        {
            try { await view.OnUnfocusAsync(); }
            catch { /* never break navigation */ }
        }
    }
}
