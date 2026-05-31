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
    /// Modal, awaitable dialog service. Mirrors <see cref="PromptService"/> semantics
    /// but is constrained to a boolean outcome.
    /// </summary>
    public sealed class DialogService : IDialogService
    {
        private sealed class DialogEntry
        {
            public IDialogView View;
            public TaskCompletionSource<bool> Tcs;
        }

        private readonly IViewHost _viewHost;
        private readonly PageFactory _factory;
        private readonly IInteractionBlocker _interactionBlocker;
        private readonly object _sync = new object();

        private readonly List<DialogEntry> _entries = new List<DialogEntry>();

        public DialogService(IViewHost viewHost, PageFactory factory, IInteractionBlocker interactionBlocker = null)
        {
            _viewHost = viewHost ?? throw new ArgumentNullException(nameof(viewHost));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _interactionBlocker = interactionBlocker;
        }

        public async Task<bool> ShowDialogAsync<TDialog>(object payload = null)
            where TDialog : class, IDialogView
        {
            var dialog = _factory.Create<TDialog>();
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            dialog.BindCompletion(result => Complete(dialog, tcs, result));

            lock (_sync)
            {
                if (_entries.Count == 0)
                    _interactionBlocker?.Block();

                _entries.Add(new DialogEntry { View = dialog, Tcs = tcs });
            }

            _viewHost.AddView(dialog.NativeView);
            _viewHost.BringToFront(dialog.NativeView);
            _viewHost.Focus(dialog.NativeView);

            await dialog.OnShownAsync(payload);

            return await tcs.Task;
        }

        public void CloseAll()
        {
            DialogEntry[] snapshot;

            lock (_sync)
            {
                if (_entries.Count == 0)
                    return;

                snapshot = _entries.ToArray();
                _entries.Clear();
            }

            foreach (var entry in snapshot)
            {
                try { _viewHost.RemoveView(entry.View.NativeView); } catch { }

                if (!entry.View.IsDisposed)
                {
                    try { entry.View.Dispose(); } catch { }
                }

                entry.Tcs.TrySetResult(false);
            }

            _interactionBlocker?.Unblock();
        }

        private void Complete(IDialogView view, TaskCompletionSource<bool> tcs, bool result)
        {
            bool owned;
            bool unblock;

            lock (_sync)
            {
                owned = RemoveEntry(view);
                unblock = owned && _entries.Count == 0;
            }

            // If CloseAll already reclaimed this entry, skip the duplicate teardown and
            // just resolve the (already-completed) awaiter — TrySetResult is a no-op then.
            if (owned)
            {
                try { _viewHost.RemoveView(view.NativeView); } catch { }

                if (!view.IsDisposed)
                {
                    try { view.Dispose(); } catch { }
                }

                if (unblock)
                    _interactionBlocker?.Unblock();
            }

            tcs.TrySetResult(result);
        }

        private bool RemoveEntry(IDialogView view)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (ReferenceEquals(_entries[i].View, view))
                {
                    _entries.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }
    }
}
