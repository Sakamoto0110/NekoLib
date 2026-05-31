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
    /// Modal, awaitable prompt service. Blocks background interaction (when an
    /// <see cref="IInteractionBlocker"/> is available) while a prompt is live and
    /// resolves the awaited <see cref="Task{TResult}"/> through the view's
    /// completion callback.
    /// </summary>
    public sealed class PromptService : IPromptService
    {
        private sealed class PromptEntry
        {
            public IPageView View;
            public Action Cancel;
        }

        private readonly IViewHost _viewHost;
        private readonly PageFactory _factory;
        private readonly IInteractionBlocker _interactionBlocker;
        private readonly object _sync = new object();

        private readonly List<PromptEntry> _entries = new List<PromptEntry>();

        public PromptService(IViewHost viewHost, PageFactory factory, IInteractionBlocker interactionBlocker = null)
        {
            _viewHost = viewHost ?? throw new ArgumentNullException(nameof(viewHost));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _interactionBlocker = interactionBlocker;
        }

        public async Task<TResult> ShowPromptAsync<TPrompt, TResult>(object payload = null)
            where TPrompt : class, IPromptView<TResult>
        {
            var prompt = _factory.Create<TPrompt>();
            var tcs = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            prompt.BindCompletion(result => Complete(prompt, tcs, result));

            lock (_sync)
            {
                if (_entries.Count == 0)
                    _interactionBlocker?.Block();

                _entries.Add(new PromptEntry
                {
                    View = prompt,
                    Cancel = () => tcs.TrySetResult(default(TResult))
                });
            }

            _viewHost.AddView(prompt.NativeView);
            _viewHost.BringToFront(prompt.NativeView);
            _viewHost.Focus(prompt.NativeView);

            await prompt.OnShownAsync(payload);

            return await tcs.Task;
        }

        public void CloseAll()
        {
            PromptEntry[] snapshot;

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

                // Resolve the awaiter with the default result for its type.
                try { entry.Cancel(); } catch { }
            }

            _interactionBlocker?.Unblock();
        }

        private void Complete<TResult>(IPromptView<TResult> view, TaskCompletionSource<TResult> tcs, TResult result)
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

        private bool RemoveEntry(IPageView view)
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
