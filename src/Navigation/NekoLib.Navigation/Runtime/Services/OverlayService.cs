using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Contracts.Runtime;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Runtime.Factories;
using NekoLib.Navigation.Runtime.Registry;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Runtime.Services
{
    public sealed class OverlayService : IOverlayService
    {
        private readonly IViewHost _host;
        private readonly PageFactory _factory;
        private readonly PageRegistry _registry;
        private readonly Stack<OverlayEntry> _stack = new();
        private readonly SemaphoreSlim _gate = new(1, 1);

        public bool HasOverlay => _stack.Count > 0;
        public int OverlayCount => _stack.Count;

        public OverlayService(IViewHost host, PageFactory factory, PageRegistry registry)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        // ------------------------------------------------------------
        // Show (fire-and-forget generic)
        // ------------------------------------------------------------

        public void Show<TOverlay>() where TOverlay : class, IPageOverlay
        {
            _ = ShowInternalAsync<TOverlay, object>(null, false);
        }

        public void Show<TOverlay>(object payload) where TOverlay : class, IPageOverlay
        {
            _ = ShowInternalAsync<TOverlay, object>(payload, false);
        }

        // ------------------------------------------------------------
        // Show (fire-and-forget runtime type - NEW!)
        // ------------------------------------------------------------

        public void Show(Type overlayType, object payload = null)
        {
            if (overlayType == null) throw new ArgumentNullException(nameof(overlayType));

            _ = ShowTypeInternalAsync(overlayType, payload);
        }

        // ------------------------------------------------------------
        // Show (awaitable result)
        // ------------------------------------------------------------

        public Task<TResult> ShowAsync<TOverlay, TResult>() where TOverlay : class, IPageOverlay<TResult>
        {
            return ShowInternalAsync<TOverlay, TResult>(null, true);
        }

        public Task<TResult> ShowAsync<TOverlay, TResult>(object payload) where TOverlay : class, IPageOverlay<TResult>
        {
            return ShowInternalAsync<TOverlay, TResult>(payload, true);
        }

        // ------------------------------------------------------------
        // Core Logic (Generic)
        // ------------------------------------------------------------

        private async Task<TResult> ShowInternalAsync<TOverlay, TResult>(object payload, bool expectResult)
            where TOverlay : class, IPageOverlay
        {
            await _gate.WaitAsync();
            try
            {
                var descriptor = _registry.GetDescriptor(typeof(TOverlay));
                var view = _factory.Create<TOverlay>();

                var tcs = expectResult ? new TaskCompletionSource<TResult>() : null;
                var entry = new OverlayEntry(view, descriptor, tcs);

                _stack.Push(entry);

                // Flattened Z-order injection
                _host.AddView(view.NativeView);
                _host.BringToFront(view.NativeView);
                _host.Focus(view.NativeView);

                await view.OnOverlayOpenedAsync(payload);

                if (expectResult && tcs != null)
                {
                    return await tcs.Task;
                }

                return default;
            }
            finally
            {
                _gate.Release();
            }
        }

        // ------------------------------------------------------------
        // Core Logic (Runtime Type - NEW!)
        // ------------------------------------------------------------

        private async Task ShowTypeInternalAsync(Type overlayType, object payload)
        {
            await _gate.WaitAsync();
            try
            {
                var descriptor = _registry.GetDescriptor(overlayType);

                // Uses the non-generic Create method on your factory
                var view = (IPageOverlay)_factory.Create(overlayType);

                var entry = new OverlayEntry(view, descriptor, null);

                _stack.Push(entry);

                _host.AddView(view.NativeView);
                _host.BringToFront(view.NativeView);
                _host.Focus(view.NativeView);

                await view.OnOverlayOpenedAsync(payload);
            }
            finally
            {
                _gate.Release();
            }
        }

        // ------------------------------------------------------------
        // Close Logic
        // ------------------------------------------------------------

        public void CloseTop()
        {
            _gate.Wait();
            try
            {
                if (_stack.Count == 0) return;

                var entry = _stack.Pop();

                // Allow the view to perform cleanup
                _ = entry.View.OnOverlayClosingAsync();

                _host.RemoveView(entry.View.NativeView);

                if (entry.ResultTcs != null)
                {
                    var result = GetResultFromView(entry.View);
                    entry.Complete(result);
                }

                if (entry.View is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        public void CloseAll()
        {
            while (HasOverlay) CloseTop();
        }

        private object GetResultFromView(IPageOverlay view)
        {
            // Specifically looks for the Result property on IPageOverlay<TResult>
            var property = view.GetType().GetProperty("Result");
            return property?.GetValue(view);
        }

        private sealed class OverlayEntry
        {
            public IPageOverlay View { get; }
            public PageDescriptor Descriptor { get; }
            public object ResultTcs { get; }

            public OverlayEntry(IPageOverlay view, PageDescriptor descriptor, object tcs)
            {
                View = view;
                Descriptor = descriptor;
                ResultTcs = tcs;
            }

            public void Complete(object result)
            {
                if (ResultTcs == null) return;
                var method = ResultTcs.GetType().GetMethod("TrySetResult");
                method?.Invoke(ResultTcs, new[] { result });
            }
        }
    }
}