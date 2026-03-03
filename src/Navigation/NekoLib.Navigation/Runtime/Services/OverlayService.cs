//using NekoLib.Navigation.Contracts.Pages;
//using NekoLib.Navigation.Contracts.Platform;
//using NekoLib.Navigation.Contracts.Runtime;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading;
//using System.Threading.Tasks;

//namespace NekoLib.Navigation.Runtime.Services
//{
//    public sealed class OverlayService : IOverlayService
//    {
//        private readonly IServiceProvider _services;
//        private readonly IModalHost _modalHost;
//        private readonly IInteractionBlocker _blocker;

//        private readonly Stack<OverlayEntry> _stack = new();
//        private readonly SemaphoreSlim _gate = new(1, 1);

//        public bool HasOverlay => _stack.Count > 0;
//        public int OverlayCount => _stack.Count;

//        public OverlayService(
//            IServiceProvider services,
//            IModalHost modalHost,
//            IInteractionBlocker blocker)
//        {
//            _services = services ?? throw new ArgumentNullException(nameof(services));
//            _modalHost = modalHost ?? throw new ArgumentNullException(nameof(modalHost));
//            _blocker = blocker ?? throw new ArgumentNullException(nameof(blocker));
//        }

//        // ------------------------------------------------------------
//        // Non-modal overlay
//        // ------------------------------------------------------------

//        public void Show<TOverlay>()
//            where TOverlay : class, IPageOverlay
//            => Show<TOverlay>(null);

//        public void Show<TOverlay>(object payload)
//            where TOverlay : class, IPageOverlay
//        {
//            _ = ShowInternalAsync<TOverlay>(payload, modal: false);
//        }

//        // ------------------------------------------------------------
//        // Modal overlay (awaitable)
//        // ------------------------------------------------------------

//        public Task<TResult> ShowAsync<TOverlay, TResult>()
//            where TOverlay : class, IPageOverlay<TResult>
//            => ShowAsync<TOverlay, TResult>(null);

//        public Task<TResult> ShowAsync<TOverlay, TResult>(object payload)
//            where TOverlay : class, IPageOverlay<TResult>
//        {
//            return ShowInternalAsync<TOverlay, TResult>(payload);
//        }

//        // ------------------------------------------------------------
//        // Close
//        // ------------------------------------------------------------

//        public void CloseTop()
//        {
//            _ = CloseTopInternalAsync();
//        }

//        public void CloseAll()
//        {
//            _ = CloseAllInternalAsync();
//        }

//        // ------------------------------------------------------------
//        // Internals
//        // ------------------------------------------------------------

//        private async Task ShowInternalAsync<TOverlay>(object payload, bool modal)
//            where TOverlay : class, IPageOverlay
//        {
//            await _gate.WaitAsync();
//            try
//            {
//                var overlay = CreateOverlay<TOverlay>();

//                var entry = new OverlayEntry(overlay, modal, null);
//                _stack.Push(entry);

//                if (modal)
//                    _blocker.Block();

//                await _modalHost.ShowModalAsync(overlay);
//                await overlay.OnOverlayOpenedAsync(payload);
//            }
//            finally
//            {
//                _gate.Release();
//            }
//        }

//        private async Task<TResult> ShowInternalAsync<TOverlay, TResult>(object payload)
//            where TOverlay : class, IPageOverlay<TResult>
//        {
//            await _gate.WaitAsync();
//            try
//            {
//                var overlay = CreateOverlay<TOverlay>();

//                var tcs = new TaskCompletionSource<TResult>(
//                    TaskCreationOptions.RunContinuationsAsynchronously);

//                var entry = new OverlayEntry(overlay, modal: true, tcs);
//                _stack.Push(entry);

//                _blocker.Block();

//                await _modalHost.ShowModalAsync(overlay);
//                await overlay.OnOverlayOpenedAsync(payload);

//                return await tcs.Task;
//            }
//            finally
//            {
//                _gate.Release();
//            }
//        }

//        private async Task CloseTopInternalAsync()
//        {
//            await _gate.WaitAsync();
//            try
//            {
//                if (_stack.Count == 0)
//                    return;

//                var entry = _stack.Peek();

//                await entry.Overlay.OnOverlayClosingAsync();
//                await _modalHost.HideModalAsync(entry.Overlay);

//                _stack.Pop();

//                if (entry.IsModal && !_stack.Any(x => x.IsModal))
//                    _blocker.Unblock();

//                entry.TryComplete();
//            }
//            finally
//            {
//                _gate.Release();
//            }
//        }

//        private async Task CloseAllInternalAsync()
//        {
//            while (_stack.Count > 0)
//                await CloseTopInternalAsync();
//        }

//        private TOverlay CreateOverlay<TOverlay>()
//            where TOverlay : class
//        {
//            var overlay = (TOverlay)_services.GetService(typeof(TOverlay));

//            if (overlay == null)
//                throw new InvalidOperationException(
//                    $"Overlay type '{typeof(TOverlay).FullName}' not registered.");

//            return overlay;
//        }

//        // ------------------------------------------------------------
//        // Entry
//        // ------------------------------------------------------------

//        private sealed class OverlayEntry
//        {
//            public IPageOverlay Overlay { get; }
//            public bool IsModal { get; }

//            private readonly object _tcs;

//            public OverlayEntry(IPageOverlay overlay, bool modal, object tcs)
//            {
//                Overlay = overlay;
//                IsModal = modal;
//                _tcs = tcs;
//            }

//            public void TryComplete()
//            {
//                if (_tcs == null)
//                    return;

//                var overlayType = Overlay.GetType();

//                var resultProp = overlayType.GetProperty("Result");
//                var result = resultProp?.GetValue(Overlay);

//                dynamic dynTcs = _tcs;
//                dynTcs.TrySetResult((dynamic)result);
//            }
//        }
//    }
//}