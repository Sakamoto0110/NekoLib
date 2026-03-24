// FILE: NekoLib.Navigation.Runtime.Core/NavigationRuntime.cs

using NekoLib.Diagnostics;
using NekoLib.Diagnostics.Contracts;
using NekoLib.Navigation.Contracts.Guards;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Contracts.Runtime;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Runtime.Factories;
using NekoLib.Navigation.Runtime.Guards;
using NekoLib.Navigation.Runtime.Registry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Runtime.Core
{
    internal sealed class NavigationRuntime : IAsyncDisposable
    {
        private readonly NavigationContext _ctx;

        private IEventDispatcherAdapter _dispatcher;
        private IInteractionObserverService _interactionObserver;
        private PageFactory _pageFactory;
        private IInteractionBlocker _interactionBlocker; // optional; used for ModalOverlay if available
        private IOverlayService _overlays;


        private readonly HashSet<IPageView> _attachedPages = new HashSet<IPageView>();
        private readonly HashSet<IPageView> _visiblePages = new HashSet<IPageView>();
        private readonly NavigationDiagnostics _diagnostics;

        public NavigationEventHub Events => _diagnostics.Hub;

        /// <summary>
        /// Base (Replace) page. Overlays do not change this.
        /// </summary>
        public IPageView Current { get; private set; }

        // Runtime-owned caches (instance scoped)
        private readonly Dictionary<Type, IPageView> _strongCache = new Dictionary<Type, IPageView>();
        private readonly Dictionary<Type, WeakReference<IPageView>> _weakCache = new Dictionary<Type, WeakReference<IPageView>>();

        /// <summary>
        /// Unified presentation stack: base Replace pages + Overlay + ModalOverlay.
        /// Top = currently top-most presented layer.
        /// </summary>
        private readonly Stack<PresentationEntry> _stack = new Stack<PresentationEntry>();

        // Serialize ALL runtime mutations
        private readonly SemaphoreSlim _navGate = new SemaphoreSlim(1, 1);

        // ---------------------------------------------------------------------
        // EVENTS
        // ---------------------------------------------------------------------
        public event Action<IPageView, Type, NavigationArgs> Navigating;
        public event Action<IPageView, IPageView, NavigationArgs> Navigated;
        public event Action<IPageView, Type, Exception> NavigationFailed;
        public event Action<IPageView> CurrentChanged;
        public event Action HistoryChanged;
        public event Action TimeoutReached;
        public event Action<IPageView> OnFirstPageAttached;
        public event Action OnNoPageAttached;
        public event Action OnNoPageVisible;

        // ---------------------------------------------------------------------
        // CTOR
        // ---------------------------------------------------------------------
        public NavigationRuntime(NavigationContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));

            var hub = new NavigationEventHub();

            INavigationDiagnosticsSink? sink =
                _ctx.DiagnosticsContext != null
                    ? new DiagnosticsNavigationSink(_ctx.DiagnosticsContext)
                    : null;

            _diagnostics = new NavigationDiagnostics(hub, sink);
        }

        // ---------------------------------------------------------------------
        // EXECUTION CORE (UI marshaling + serialization)
        // ---------------------------------------------------------------------

        private void EnsureDispatcher()
        {
            if (_dispatcher != null)
                return;

            var services = _ctx.Services
                ?? throw new InvalidOperationException("NavigationContext.Services is not initialized.");

            if (!services.CanResolve(typeof(IEventDispatcherAdapter)))
                throw new InvalidOperationException("IEventDispatcherAdapter is required but not registered.");

            _dispatcher = (IEventDispatcherAdapter)services.Get(typeof(IEventDispatcherAdapter));
        }

        private PageDescriptor ResolveHomeDescriptor()
        {
            return _ctx.Registry.AllDescriptors()
                .FirstOrDefault(x => x.Role == PageRole.Home)
                ?? _ctx.Registry.AllDescriptors()
                    .FirstOrDefault(x => x.Tags.Contains("home", StringComparer.OrdinalIgnoreCase));
        }

        public async Task GoHomeAsync(object args = null)
        {
            var desc = ResolveHomeDescriptor();
            if (desc == null)
                return;

            await NavigateAsync(desc.PageType, NavigationArgs.Default(args));
        }

        private void EnsureRuntimeServices()
        {
            EnsureDispatcher();

            var services = _ctx.Services
                ?? throw new InvalidOperationException("NavigationContext.Services is not initialized.");

            if (_interactionObserver == null &&
                services.CanResolve(typeof(IInteractionObserverService)))
            {
                _interactionObserver = (IInteractionObserverService)services.Get(typeof(IInteractionObserverService));
                _interactionObserver.InteractionDetected += OnInteractionDetected;
            }

            if (_pageFactory == null)
            {
                if (!services.CanResolve(typeof(PageFactory)))
                    throw new InvalidOperationException("PageFactory is required but not registered.");

                _pageFactory = (PageFactory)services.Get(typeof(PageFactory));
            }

            if (_interactionBlocker == null && services.CanResolve(typeof(IInteractionBlocker)))
            {
                _interactionBlocker = (IInteractionBlocker)services.Get(typeof(IInteractionBlocker));
            }
            if (_overlays == null && services.CanResolve(typeof(IOverlayService)))
            {
                _overlays = (IOverlayService)services.Get(typeof(IOverlayService));
            }
        }

        private Task RunOnUiAsync(Func<Task> action)
        {
            EnsureDispatcher();

            var tcs = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            _dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    // IMPORTANT: do not ConfigureAwait(false) here.
                    await action();
                    tcs.TrySetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            return tcs.Task;
        }

        private Task<T> RunOnUiAsync<T>(Func<Task<T>> action)
        {
            EnsureDispatcher();

            var tcs = new TaskCompletionSource<T>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            _dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    var result = await action();
                    tcs.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            return tcs.Task;
        }

        private async Task SerializeAsync(Func<Task> action)
        {
            await _navGate.WaitAsync();
            try
            {
                await action();
            }
            finally
            {
                _navGate.Release();
            }
        }

        private async Task<T> SerializeAsync<T>(Func<Task<T>> action)
        {
            await _navGate.WaitAsync();
            try
            {
                return await action();
            }
            finally
            {
                _navGate.Release();
            }
        }

        private Task ExecuteAsync(Func<Task> action)
            => RunOnUiAsync(() => SerializeAsync(action));

        private Task<T> ExecuteAsync<T>(Func<Task<T>> action)
            => RunOnUiAsync(() => SerializeAsync(action));

        // ---------------------------------------------------------------------
        // PUBLIC API (all entry points are gated)
        // ---------------------------------------------------------------------

        public Task NavigateAsync(Type pageType, NavigationArgs args = null)
        {
            return ExecuteAsync(() =>
            {
                EnsureRuntimeServices();
                return SwitchInternalAsync(pageType, args ?? NavigationArgs.Empty);
            });
        }

        internal Task<bool> GoBackAsync()
        {
            return ExecuteAsync(() =>
            {
                EnsureRuntimeServices();
                return GoBackInternalAsync();
            });
        }
        internal   void ShowOverlay<TOverlay>() where TOverlay : class, IPageOverlay
        {
            _overlays.Show<TOverlay>();
        }

        internal   void ShowOverlay<TOverlay>(object payload) where TOverlay : class, IPageOverlay
        {
            _overlays.Show<TOverlay>(payload);
        }

        internal   void ShowOverlay(Type overlayType, object payload = null)
        {
            _overlays.Show(overlayType, payload);
        }

        // ------------------------------------------------------------
        // Awaitable Dialogs (Modals returning a result)
        // ------------------------------------------------------------

        internal   Task<TResult> ShowDialogAsync<TOverlay, TResult>() where TOverlay : class, IPageOverlay<TResult>
        {
            return _overlays.ShowAsync<TOverlay, TResult>();
        }

        internal   Task<TResult> ShowDialogAsync<TOverlay, TResult>(object payload) where TOverlay : class, IPageOverlay<TResult>
        {
            return _overlays.ShowAsync<TOverlay, TResult>(payload);
        }

        internal void CloseTopOverlay()
        {
            _overlays.CloseTop();
        }

        internal void CloseAllOverlays()
        {
            _overlays.CloseAll();
        }



























        /// <summary>
        /// Explicit modal API. Returns a result when the modal closes.
        /// </summary>
        public Task<ModalResult> ShowModalAsync(Type pageType, NavigationArgs args = null)
        {
            return ExecuteAsync(async () =>
            {
                EnsureRuntimeServices();

                if (pageType == null)
                    throw new ArgumentNullException(nameof(pageType));

                if (!_ctx.Registry.TryGetDescriptor(pageType, out var desc))
                    throw new InvalidOperationException($"Type '{pageType.FullName}' is not a registered page.");

                if (desc.Presentation != PagePresentationMode.ModalOverlay)
                    throw new InvalidOperationException($"Page '{desc.PageType.FullName}' is not marked as ModalOverlay.");

                return await ShowModalInternalAsync(desc, args ?? NavigationArgs.Empty);
            });
        }

        public Task<bool> CloseTopModalAsync(ModalResult result)
        {
            return ExecuteAsync(() =>
            {
                EnsureRuntimeServices();
                return CloseTopModalInternalAsync(result);
            });
        }

        public Task ResetAsync()
        {
            return ExecuteAsync(async () =>
            {
                EnsureRuntimeServices();

                // Close all overlays (Overlay + ModalOverlay), then dispose base page
                await CloseAllOverlaysInternalAsync(ModalResult.Cancel());

                if (Current != null)
                {
                    // detach base
                    _ctx.Host.Detach(Current);

                    if (_ctx.Registry.TryGetDescriptor(Current.GetType(), out var desc))
                        await CleanupAsync(Current, desc, forceDispose: true);
                    else
                        DisposePage(Current);

                    Current = null;
                    CurrentChanged?.Invoke(null);
                }

                _attachedPages.Clear();
                _visiblePages.Clear();

                OnNoPageAttached?.Invoke();
                OnNoPageVisible?.Invoke();

                _ctx.History.Clear();
                HistoryChanged?.Invoke();

                _stack.Clear();
            });
        }

        public ValueTask DisposeAsync()
        {
            return new ValueTask(ExecuteAsync(async () =>
            {
                EnsureRuntimeServices();

                await CloseAllOverlaysInternalAsync(ModalResult.Cancel());

                if (Current != null)
                {
                    _ctx.Host.Detach(Current);

                    if (_ctx.Registry.TryGetDescriptor(Current.GetType(), out var desc))
                        await CleanupAsync(Current, desc, forceDispose: true);
                    else
                        DisposePage(Current);

                    Current = null;
                }

                if (_interactionObserver != null)
                    _interactionObserver.InteractionDetected -= OnInteractionDetected;

                _stack.Clear();
            }));
        }

        // ---------------------------------------------------------------------
        // BACK (internal)
        // ---------------------------------------------------------------------

        private async Task<bool> GoBackInternalAsync()
        {
            // If any overlay is on top, close it first
            if (_stack.Count > 0 && _stack.Peek().IsOverlay)
            {
                if (_stack.Peek().IsModalOverlay)
                {
                    await CloseTopModalInternalAsync(ModalResult.Cancel());
                    return true;
                }

                await CloseTopOverlayInternalAsync(); // non-modal overlay
                return true;
            }

            if (!_ctx.History.TryPopBack(out PageHistoryEntry entry))
                return false;

            PageHistoryEntry forwardEntry = null;

            if (Current != null)
            {
                forwardEntry = new PageHistoryEntry(
                    Current.GetType(),
                    Current.Name,
                    (Current as IPageStateful)?.CaptureState()
                );
            }

            await SwitchInternalAsync(
                entry.PageType,
                NavigationArgs.Default(entry.State));

            if (forwardEntry != null)
                _ctx.History.PushForward(forwardEntry);

            return true;
        }

        // ---------------------------------------------------------------------
        // CORE NAVIGATION (ASSUMES already UI-thread + serialized)
        // ---------------------------------------------------------------------

        private async Task SwitchInternalAsync(Type pageType,NavigationArgs navArgs,int redirectDepth = 0,HashSet<Type> visited = null)
        {
            if (pageType == null)
                throw new ArgumentNullException(nameof(pageType));

            IPageView from = Current;
            IPageView to = null;
            PageDescriptor toDesc = null;
            PageDescriptor fromDesc = null;

            try
            {
                if (!_ctx.Registry.TryGetDescriptor(pageType, out toDesc))
                    throw new InvalidOperationException(
                        $"Type '{pageType.FullName}' is not a registered page.");

                var canonicalPageType = toDesc.PageType;

                visited ??= new HashSet<Type>();

                if (!visited.Add(canonicalPageType))
                {
                    _diagnostics.EmitGuardDenied(
                        from,
                        canonicalPageType,
                        null,
                        "Guard redirect cycle detected.");
                    return;
                }

                if (redirectDepth > 8)
                {
                    _diagnostics.EmitGuardDenied(
                        from,
                        canonicalPageType,
                        null,
                        "Max guard redirect depth exceeded.");
                    return;
                }

                // ---------------- GUARDS ----------------
                var guard = toDesc.Guard;

                if (guard != null)
                {
                    var guardCtx = new GuardContext(canonicalPageType, _ctx.User);

                    GuardResult result;

                    try
                    {
                        result = await guard.EvaluateAsync(guardCtx);
                    }
                    catch (Exception ex)
                    {
                        _diagnostics.EmitGuardDenied(
                            from,
                            canonicalPageType,
                            null,
                            $"Guard exception: {ex.Message}");
                        return;
                    }

                    if (!result.Allowed)
                    {
                        _diagnostics.EmitGuardDenied(
                            from,
                            canonicalPageType,
                            result.RedirectPage,
                            result.Reason);

                        if (result.RedirectPage != null)
                        {
                            await SwitchInternalAsync(
                                result.RedirectPage,
                                navArgs,
                                redirectDepth + 1,
                                visited);
                        }

                        return;
                    }
                }

                // ---------------- PRESENTATION (UNIFIED) ----------------
                switch (toDesc.Presentation)
                {
                    case PagePresentationMode.Overlay:
                        await ShowOverlayInternalAsync(toDesc, navArgs);
                        return;

                    case PagePresentationMode.ModalOverlay:
                        // NavigateAsync can open modals, but doesn't return a result.
                        _ = await ShowModalInternalAsync(toDesc, navArgs);
                        return;

                    case PagePresentationMode.Replace:

                    default:
                        break;
                }

                // Replace cancels overlays (kiosk-friendly, matches your previous behavior)
                await CloseAllOverlaysInternalAsync(ModalResult.Cancel());

                // ---------------- NORMAL NAVIGATION (Replace) ----------------

                if (!typeof(IPageView).IsAssignableFrom(canonicalPageType))
                    throw new InvalidOperationException(
                        $"Navigation target '{canonicalPageType.FullName}' is not a page.");


                if (from != null)
                {
                    _ctx.Registry.TryGetDescriptor(from.GetType(), out fromDesc);
                }
                // Capture history state EARLY
                object fromState = null;
                if (toDesc.Presentation == PagePresentationMode.Replace && from != null)
                    fromState = (from as IPageStateful)?.CaptureState();

                Navigating?.Invoke(Current, canonicalPageType, navArgs);

                to = ResolvePage(toDesc);

                if (toDesc.LoadMode == NavigationLoadMode.LoadBeforeShow)
                {
                    await LoadAsync(to, navArgs.Payload);
                }

                // Hide base page
                if (from is IPageVisibility fromVis)
                    fromVis.HidePage();

                if (from is IPageLifecycle leave)
                    await leave.OnNavigatedFromAsync();

                // Detach or keep attached depending on descriptor
                if (from != null)
                {
                    bool keepAttached =
                        fromDesc != null &&
                        fromDesc.KeepAttachedWhenHidden &&
                        fromDesc.ReusePolicy != PageReusePolicy.Transient &&
                        !from.IsDisposed;

                    if (!keepAttached)
                    {
                        _ctx.Host.Detach(from);

                        if (_visiblePages.Remove(from) && _visiblePages.Count == 0)
                            OnNoPageVisible?.Invoke();

                        if (_attachedPages.Remove(from) && _attachedPages.Count == 0)
                            OnNoPageAttached?.Invoke();

                        await CleanupAsync(from, fromDesc, forceDispose: false);
                    }
                    else
                    {
                        // still attached but not visible
                        _visiblePages.Remove(from);
                        if (_visiblePages.Count == 0)
                            OnNoPageVisible?.Invoke();
                    }
                }

                bool firstAttach = _attachedPages.Count == 0;

                _ctx.Host.Attach(to);
                _ctx.Host.BringToFront(to);

                if (_attachedPages.Add(to) && firstAttach)
                    OnFirstPageAttached?.Invoke(to);

                if (to is IPageVisibility toVis)
                {
                    toVis.ShowPage();
                    _visiblePages.Add(to);
                }

                Current = to;
                CurrentChanged?.Invoke(Current);

                // Update unified stack base pointer:
                // Remove any existing base entries, keep overlays already closed above.
                PushOrReplaceBaseEntry(to, toDesc);

                if (toDesc.LoadMode == NavigationLoadMode.ShowImmediately)
                {
                    await LoadAsync(to, navArgs.Payload);
                }
                else if (toDesc.LoadMode == NavigationLoadMode.LoadInBackground)
                {
                    _ = LoadAsync(to, navArgs.Payload);
                }

                if (to is IPageLifecycle enter)
                    await enter.OnNavigatedToAsync(navArgs);



                if (toDesc.Presentation == PagePresentationMode.Replace && from != null)
                {
                    _ctx.History.Record(new PageHistoryEntry(
                        from.GetType(),
                        from.Name,
                        fromState
                    ));

                    HistoryChanged?.Invoke();
                }


                Navigated?.Invoke(from, to, navArgs);
                _diagnostics.EmitSuccess(from, to, navArgs, desc: toDesc);
            }
            catch (Exception ex)
            {
                NavigationFailed?.Invoke(from, pageType, ex);
                _diagnostics.EmitFailure(from, to, navArgs, desc: toDesc);
                throw;
            }
        }

        // Keep a sync signature if something calls it, but route to safe async path.
        private void OnTimeout()
        {
            _ = OnTimeoutAsync();
        }

        private Task OnTimeoutAsync()
        {
            return ExecuteAsync(async () =>
            {
                TimeoutReached?.Invoke();

                var current = Current;

                if (current != null &&
                    _ctx.Registry.TryGetDescriptor(current.GetType(), out var desc))
                {
                    switch (desc.TimeoutPolicy)
                    {
                        case PageTimeoutPolicy.Disabled:
                            return;

                        case PageTimeoutPolicy.IsTimeoutTarget:
                            return; // already at timeout page

                        case PageTimeoutPolicy.ResetOnEnter:
                        case PageTimeoutPolicy.Inherit:
                        default:
                            break;
                    }
                }

                var timeoutTarget = _ctx.Registry.AllDescriptors()
                    .FirstOrDefault(x => x.TimeoutPolicy == PageTimeoutPolicy.IsTimeoutTarget);

                if (timeoutTarget == null)
                    timeoutTarget = ResolveHomeDescriptor();

                if (timeoutTarget == null)
                    return;

                await SwitchInternalAsync(timeoutTarget.PageType, NavigationArgs.Default());
            });
        }

        private async Task LoadAsync(IPageView page, object payload)
        {
            if (page is IBackgroundLoadable bg)
            {
                // 1. Resolve the mask from the registry
                var maskDesc = _ctx.Registry.AllDescriptors()
                    .FirstOrDefault(d => typeof(IGlobalLoadingMask).IsAssignableFrom(d.PageType));

                var overlays = _ctx.Services.CanResolve(typeof(IOverlayService))
                    ? _ctx.Services.Get(typeof(IOverlayService)) as IOverlayService
                    : null;

                // 2. SHOW THE MASK
                if (overlays != null && maskDesc != null)
                    overlays.Show(maskDesc.PageType, "Loading...");

                // 3. DO THE HEAVY EXCEL WORK
                await Task.Run(async () => await bg.LoadInBackgroundAsync(payload).ConfigureAwait(false));

                // 4. HIDE THE MASK
                if (overlays != null && maskDesc != null)
                    overlays.CloseTop();

                await bg.ApplyBackgroundResultAsync();
            }
        }

        // ---------------------------------------------------------------------
        // OVERLAYS (UNIFIED STACK)
        // ---------------------------------------------------------------------

        private IModalHost EnsureModalHost()
        {
            // Prefer host implementing it (your current setup)
            if (_ctx.Host is IModalHost mh)
                return mh;

            // Fallback: allow resolving via services
            var services = _ctx.Services;
            if (services != null && services.CanResolve(typeof(IModalHost)))
                return (IModalHost)services.Get(typeof(IModalHost));

            throw new InvalidOperationException("Modal host missing: IModalHost not available.");
        }

        private async Task ShowOverlayInternalAsync(PageDescriptor descriptor, NavigationArgs args)
        {
            var modalHost = EnsureModalHost();

            var view = ResolvePage(descriptor);

            // Push overlay entry (no TCS)
            _stack.Push(PresentationEntry.ForOverlay(view, descriptor));

            await modalHost.ShowModalAsync(view);

            // Overlay lifecycle if implemented
            if (view is IPageOverlay overlay)
                await overlay.OnOverlayOpenedAsync(args?.Payload);

            // No blocking for Overlay mode
        }

        private async Task<ModalResult> ShowModalInternalAsync(PageDescriptor descriptor, NavigationArgs args)
        {
            var modalHost = EnsureModalHost();

            var view = ResolvePage(descriptor);

            var tcs = new TaskCompletionSource<ModalResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            _stack.Push(PresentationEntry.ForModal(view, descriptor, tcs));

            // Block interaction if available (host overlay panel may also block physically)
            _interactionBlocker?.Block();

            await modalHost.ShowModalAsync(view);

            if (view is IPageOverlay overlay)
                await overlay.OnOverlayOpenedAsync(args?.Payload);

            return await tcs.Task;
        }

        private async Task CloseTopOverlayInternalAsync()
        {
            if (_stack.Count == 0)
                return;

            var top = _stack.Peek();
            if (!top.IsOverlay)
                return;

            var modalHost = EnsureModalHost();

            // overlay lifecycle
            if (top.View is IPageOverlay overlay)
                await overlay.OnOverlayClosingAsync();

            await modalHost.HideModalAsync(top.View);

            _stack.Pop();

            // cleanup overlay instance depending on reuse policy
            if (top.Descriptor.ReusePolicy == PageReusePolicy.Transient)
                DisposePage(top.View);
        }

        private async Task<bool> CloseTopModalInternalAsync(ModalResult result)
        {
            if (_stack.Count == 0)
                return false;

            var top = _stack.Peek();
            if (!top.IsModalOverlay)
                return false;

            var modalHost = EnsureModalHost();

            if (top.View is IPageOverlay overlay)
                await overlay.OnOverlayClosingAsync();

            await modalHost.HideModalAsync(top.View);

            _stack.Pop();

            // Unblock if no modal overlays remain
            if (!_stack.Any(x => x.IsModalOverlay))
                _interactionBlocker?.Unblock();

            if (top.Descriptor.ReusePolicy == PageReusePolicy.Transient)
                DisposePage(top.View);

            top.ModalTcs.TrySetResult(result);
            return true;
        }

        private async Task CloseAllOverlaysInternalAsync(ModalResult cancelResult)
        {
            // Close overlays until top is base (Replace) or stack empty
            while (_stack.Count > 0 && _stack.Peek().IsOverlay)
            {
                if (_stack.Peek().IsModalOverlay)
                    await CloseTopModalInternalAsync(cancelResult);
                else
                    await CloseTopOverlayInternalAsync();
            }

            _interactionBlocker?.Unblock();
        }

        private void PushOrReplaceBaseEntry(IPageView baseView, PageDescriptor baseDesc)
        {
            // Remove any existing base entries from stack.
            // In this runtime, we only keep overlays above base, and base as single entry.
            // Since Replace closes overlays before navigation, the stack should be empty or already contain only base.
            // We'll ensure the top base entry matches current.
            if (_stack.Count == 0)
            {
                _stack.Push(PresentationEntry.ForBase(baseView, baseDesc));
                return;
            }

            // If the stack currently has base on top (no overlays), replace it.
            if (_stack.Count == 1 && !_stack.Peek().IsOverlay)
            {
                _stack.Pop();
                _stack.Push(PresentationEntry.ForBase(baseView, baseDesc));
                return;
            }

            // If something unexpected remains, rebuild base-only.
            var overlays = _stack.Where(e => e.IsOverlay).Reverse().ToList();
            _stack.Clear();
            _stack.Push(PresentationEntry.ForBase(baseView, baseDesc));
            foreach (var ov in overlays)
                _stack.Push(ov);
        }

        // ---------------------------------------------------------------------
        // PAGE RESOLUTION (reuse policy caches)
        // ---------------------------------------------------------------------

        private IPageView ResolvePage(PageDescriptor d)
        {
            var factory = _pageFactory;

            switch (d.ReusePolicy)
            {
                case PageReusePolicy.Transient:
                    return factory.Create(d.PageType);

                case PageReusePolicy.Singleton:
                    if (_strongCache.TryGetValue(d.PageType, out var strong) &&
                        strong != null &&
                        !strong.IsDisposed)
                    {
                        return strong;
                    }

                    strong = factory.Create(d.PageType);
                    _strongCache[d.PageType] = strong;
                    return strong;

                case PageReusePolicy.Cached:
                    if (_weakCache.TryGetValue(d.PageType, out var weak) &&
                        weak.TryGetTarget(out var target) &&
                        target != null &&
                        !target.IsDisposed)
                    {
                        return target;
                    }

                    var newPage = factory.Create(d.PageType);
                    _weakCache[d.PageType] = new WeakReference<IPageView>(newPage);
                    return newPage;

                default:
                    return factory.Create(d.PageType);
            }
        }

        // ---------------------------------------------------------------------
        // LIFECYCLE + CLEANUP
        // ---------------------------------------------------------------------

        private Task CleanupAsync(IPageView page, PageDescriptor descriptor, bool forceDispose)
        {
            if (page == null || page.IsDisposed)
                return Task.CompletedTask;

            if (forceDispose)
            {
                if (descriptor != null)
                    RemoveFromCaches(descriptor.PageType, page);

                DisposePage(page);
                return Task.CompletedTask;
            }

            if (descriptor != null && descriptor.ReusePolicy == PageReusePolicy.Transient)
            {
                DisposePage(page);
            }

            return Task.CompletedTask;
        }

        private void RemoveFromCaches(Type pageType, IPageView page)
        {
            if (_strongCache.TryGetValue(pageType, out var strong))
            {
                if (strong == null || ReferenceEquals(strong, page))
                    _strongCache.Remove(pageType);
            }

            if (_weakCache.TryGetValue(pageType, out var weak))
            {
                if (!weak.TryGetTarget(out var target) || ReferenceEquals(target, page))
                    _weakCache.Remove(pageType);
            }
        }

        private void DisposePage(IPageView page)
        {
            if (page == null || page.IsDisposed)
                return;

            try { page.Dispose(); }
            catch { }
        }

        private void OnInteractionDetected()
        {
            // intentionally empty (extension point)
        }

        // ---------------------------------------------------------------------
        // INTERNAL SUPPORT TYPES
        // ---------------------------------------------------------------------

        public readonly struct ModalResult
        {
            public bool Confirmed { get; }
            public object Value { get; }

            public ModalResult(bool confirmed, object value = null)
            {
                Confirmed = confirmed;
                Value = value;
            }

            public static ModalResult Ok(object value = null) => new ModalResult(true, value);
            public static ModalResult Cancel() => new ModalResult(false, null);
        }

        private sealed class PresentationEntry
        {
            public IPageView View { get; }
            public PageDescriptor Descriptor { get; }
            public TaskCompletionSource<ModalResult> ModalTcs { get; }

            private PresentationEntry(IPageView view, PageDescriptor descriptor, TaskCompletionSource<ModalResult> modalTcs)
            {
                View = view;
                Descriptor = descriptor;
                ModalTcs = modalTcs;
            }

            public bool IsOverlay => Descriptor.Presentation != PagePresentationMode.Replace;
            public bool IsModalOverlay => Descriptor.Presentation == PagePresentationMode.ModalOverlay;

            public static PresentationEntry ForBase(IPageView view, PageDescriptor desc)
                => new PresentationEntry(view, desc, null);

            public static PresentationEntry ForOverlay(IPageView view, PageDescriptor desc)
                => new PresentationEntry(view, desc, null);

            public static PresentationEntry ForModal(IPageView view, PageDescriptor desc, TaskCompletionSource<ModalResult> tcs)
                => new PresentationEntry(view, desc, tcs);
        }
    }
}