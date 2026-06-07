// FILE: NekoLib.Navigation.Runtime.Core/NavigationRuntime.cs

using NekoLib.Navigation.Contracts.Guards;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Contracts.Runtime;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Runtime.Factories;
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
        private IToastService _toastService;
        private IDialogService _dialogService;
        private IPromptService _promptService;

        private readonly HashSet<IPageView> _attachedPages = new HashSet<IPageView>();
        private readonly HashSet<IPageView> _visiblePages = new HashSet<IPageView>();
        private readonly NavigationDiagnostics _diagnostics;

        public NavigationEventHub Events => _diagnostics.Hub;

        /// <summary>
        /// The current visible page.
        /// </summary>
        public IPageView Current { get; private set; }

        // Runtime-owned caches (instance scoped)
        private readonly Dictionary<Type, IPageView> _strongCache = new Dictionary<Type, IPageView>();
        private readonly Dictionary<Type, WeakReference<IPageView>> _weakCache = new Dictionary<Type, WeakReference<IPageView>>();

        // Serialize ALL runtime mutations
        private readonly SemaphoreSlim _navGate = new SemaphoreSlim(1, 1);

        // Upper bound on guard evaluation. Guards run inside the serialized section
        // (holding _navGate), so a hung guard would otherwise deadlock all navigation
        // (N-1). On timeout the navigation is denied and the gate is released.
        private const int GuardEvaluationTimeoutMs = 30_000;

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

            // Share the context's diagnostics channel rather than building a second,
            // orphaned hub. Previously the runtime published to its own hub while
            // NavigationContext.Events / .Diagnostics exposed a different, silent one,
            // so consumers reading context.Events never saw any navigation activity (D-3).
            _diagnostics = _ctx.Diagnostics;
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
            // 1) Explicit metadata: PageRole.Home (set via .AsHome()).
            // 2) Convention tag: "home".
            // 3) Name convention: any registered page whose Name is "HomePage" or
            //    "MainPage" — lets timeouts / GoHomeAsync work when the consumer
            //    forgets to call .AsHome().
            return _ctx.Registry.AllDescriptors()
                .FirstOrDefault(x => x.Role == PageRole.Home)
                ?? _ctx.Registry.AllDescriptors()
                    .FirstOrDefault(x => x.Tags.Contains("home", StringComparer.OrdinalIgnoreCase))
                ?? _ctx.Registry.AllDescriptors()
                    .FirstOrDefault(x =>
                        string.Equals(x.Name, "HomePage", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(x.Name, "MainPage", StringComparison.OrdinalIgnoreCase));
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

            if (_toastService == null && services.CanResolve(typeof(IToastService)))
            {
                _toastService = (IToastService)services.Get(typeof(IToastService));
            }

            if (_dialogService == null && services.CanResolve(typeof(IDialogService)))
            {
                _dialogService = (IDialogService)services.Get(typeof(IDialogService));
            }

            if (_promptService == null && services.CanResolve(typeof(IPromptService)))
            {
                _promptService = (IPromptService)services.Get(typeof(IPromptService));
            }
        }

        private void RunOnUi(Action action)
        {
            EnsureDispatcher();
            _dispatcher.BeginInvoke(action);
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

        /// <summary>
        /// Like <see cref="ExecuteAsync(Func{Task})"/>, but tolerant of a dead message
        /// pump. During app shutdown the host handle may already be destroyed, so
        /// <c>BeginInvoke</c> would throw (or never run) and the awaiter would hang.
        /// On failure we run the teardown inline so dispose always completes.
        /// </summary>
        private Task ExecuteSafeOnUiAsync(Func<Task> action)
        {
            EnsureDispatcher();

            var tcs = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            async void Run()
            {
                try
                {
                    await SerializeAsync(action);
                    tcs.TrySetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }

            try
            {
                _dispatcher.BeginInvoke(Run);
            }
            catch
            {
                // Message pump is gone (handle destroyed during shutdown). Run inline
                // so DisposeAsync still completes instead of hanging forever.
                Run();
            }

            return tcs.Task;
        }

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
        // ------------------------------------------------------------
        // ISP-segregated surface: Toast / Dialog / Prompt
        // ------------------------------------------------------------

        internal void ShowToast<TToast>(object payload = null, int durationMs = 3000)
            where TToast : class, IToastView
        {
            EnsureRuntimeServices();

            if (_toastService == null)
                throw new InvalidOperationException("IToastService is not registered.");

            RunOnUi(() => _toastService.ShowToast<TToast>(payload, durationMs));
        }

        internal void DismissCurrentToast()
        {
            EnsureRuntimeServices();

            if (_toastService != null)
                RunOnUi(_toastService.DismissCurrentToast);
        }

        internal Task<bool> ShowDialogAsync<TDialog>(object payload = null)
            where TDialog : class, IDialogView
        {
            EnsureRuntimeServices();

            if (_dialogService == null)
                throw new InvalidOperationException("IDialogService is not registered.");

            // Marshal to the UI thread but do NOT take the nav gate: a modal dialog
            // awaits user input, and holding _navGate would block all navigation.
            return RunOnUiAsync(() => _dialogService.ShowDialogAsync<TDialog>(payload));
        }

        internal Task<TResult> ShowPromptAsync<TPrompt, TResult>(object payload = null)
            where TPrompt : class, IPromptView<TResult>
        {
            EnsureRuntimeServices();

            if (_promptService == null)
                throw new InvalidOperationException("IPromptService is not registered.");

            return RunOnUiAsync(() => _promptService.ShowPromptAsync<TPrompt, TResult>(payload));
        }

        public Task ResetAsync()
        {
            return ExecuteAsync(async () =>
            {
                EnsureRuntimeServices();

                // Tear down any live toast/dialog/prompt surfaces first so their
                // awaiters complete and the interaction blocker is released.
                TeardownOverlayServices();

                if (Current != null)
                {
                    _ctx.Host.Detach(Current);

                    if (_ctx.Registry.TryGetDescriptor(Current.GetType(), out var desc))
                        Cleanup(Current, desc, forceDispose: true);
                    else
                        DisposePage(Current);

                    Current = null;
                    CurrentChanged?.Invoke(null);
                }

                DisposeCachedPages();

                _attachedPages.Clear();
                _visiblePages.Clear();

                OnNoPageAttached?.Invoke();
                OnNoPageVisible?.Invoke();

                _ctx.History.Clear();
                HistoryChanged?.Invoke();
            });
        }

        public ValueTask DisposeAsync()
        {
            return new ValueTask(ExecuteSafeOnUiAsync(async () =>
            {
                EnsureRuntimeServices();

                TeardownOverlayServices();

                if (Current != null)
                {
                    _ctx.Host.Detach(Current);

                    if (_ctx.Registry.TryGetDescriptor(Current.GetType(), out var desc))
                        Cleanup(Current, desc, forceDispose: true);
                    else
                        DisposePage(Current);

                    Current = null;
                }

                DisposeCachedPages();

                if (_interactionObserver != null)
                    _interactionObserver.InteractionDetected -= OnInteractionDetected;
            }));
        }

        private void TeardownOverlayServices()
        {
            _toastService?.DismissCurrentToast();
            _dialogService?.CloseAll();
            _promptService?.CloseAll();
        }

        // ---------------------------------------------------------------------
        // BACK (internal)
        // ---------------------------------------------------------------------

        private async Task<bool> GoBackInternalAsync()
        {
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
                NavigationArgs.Back(entry.State));

            if (forwardEntry != null)
                _ctx.History.PushForward(forwardEntry);

            // SwitchInternalAsync intentionally skips its Record/HistoryChanged on
            // back-navigation (see fix there); the back-path manages history itself,
            // so fire the notification once from here.
            HistoryChanged?.Invoke();

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
                        // Bound guard evaluation so a hung guard can't hold _navGate
                        // forever (N-1). The abandoned task is left to complete on its
                        // own; we simply deny the navigation and release the gate.
                        var evalTask = guard.EvaluateAsync(guardCtx);
                        var finished = await Task.WhenAny(
                            evalTask,
                            Task.Delay(GuardEvaluationTimeoutMs));

                        if (!ReferenceEquals(finished, evalTask))
                        {
                            _diagnostics.EmitGuardDenied(
                                from,
                                canonicalPageType,
                                null,
                                "Guard evaluation timed out.");
                            return;
                        }

                        result = await evalTask;
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

                // ---------------- NAVIGATION ----------------

                if (!typeof(IPageView).IsAssignableFrom(canonicalPageType))
                    throw new InvalidOperationException(
                        $"Navigation target '{canonicalPageType.FullName}' is not a page.");

                if (from != null)
                {
                    _ctx.Registry.TryGetDescriptor(from.GetType(), out fromDesc);
                }
                // Capture history state EARLY
                object fromState = null;
                if (from != null)
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

                        Cleanup(from, fromDesc, forceDispose: false);
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

                if (toDesc.LoadMode == NavigationLoadMode.ShowImmediately)
                {
                    await LoadAsync(to, navArgs.Payload);
                }
                else if (toDesc.LoadMode == NavigationLoadMode.LoadInBackground)
                {
                    _ = LoadInBackgroundSafeAsync(to, navArgs.Payload);
                }

                // On back-navigation, restore the captured state through the explicit
                // IPageStateful channel before the page's enter hook runs, so the page
                // is fully rehydrated when OnNavigatedToAsync executes (N-2).
                if (navArgs.IsBackNavigation && to is IPageStateful stateful)
                    stateful.RestoreState(navArgs.Payload);

                if (to is IPageLifecycle enter)
                    await enter.OnNavigatedToAsync(navArgs);

                // Forward navigation pushes `from` onto the back-stack so the user can
                // return to it. Back-navigation must NOT do this: GoBackInternalAsync
                // already manages the back/forward stacks itself, and recording `from`
                // here would re-push the page we are leaving (e.g. E) onto the back-
                // stack, causing the next Back to land back on E instead of stepping
                // further back to A. See history-double-push fix.
                if (from != null && !navArgs.IsBackNavigation)
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
            // Observe faults so a poisoned gate or guard failure doesn't vanish via the
            // discarded task (A-7).
            _ = OnTimeoutAsync().ContinueWith(
                t => System.Diagnostics.Debug.WriteLine(
                    $"[NavigationRuntime] Timeout navigation failed: {t.Exception}"),
                TaskContinuationOptions.OnlyOnFaulted);
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

        // Fire-and-forget wrapper for LoadInBackground. Never lets a background-load
        // failure surface as an unobserved task exception, and only applies the result
        // when the page is still the live one (A-5).
        private async Task LoadInBackgroundSafeAsync(IPageView page, object payload)
        {
            try
            {
                await LoadAsync(page, payload, guardApply: true);
            }
            catch (Exception ex)
            {
                _diagnostics.EmitFailure(page, page, NavigationArgs.Empty, desc: null);
                System.Diagnostics.Debug.WriteLine(
                    $"[NavigationRuntime] Background load failed for '{page?.GetType().FullName}': {ex}");
            }
        }

        private async Task LoadAsync(IPageView page, object payload, bool guardApply = false)
        {
            if (page is IBackgroundLoadable bg)
            {
                // The loading mask is system infrastructure, not a user toast/dialog.
                // Drive it through IViewHost directly to avoid coupling to the user-facing services.
                var maskDesc = _ctx.Registry.AllDescriptors()
                    .FirstOrDefault(d => typeof(IGlobalLoadingMask).IsAssignableFrom(d.PageType));

                var viewHost = _ctx.Host as IViewHost;
                IPageView mask = null;

                if (maskDesc != null && viewHost != null && _pageFactory != null)
                {
                    mask = _pageFactory.Create(maskDesc.PageType);
                    viewHost.AddView(mask.NativeView);
                    viewHost.BringToFront(mask.NativeView);

                    if (mask is IPageOverlay overlay)
                        await overlay.OnOverlayOpenedAsync("Loading...");
                }

                await Task.Run(async () => await bg.LoadInBackgroundAsync(payload).ConfigureAwait(false));

                if (mask != null && viewHost != null)
                {
                    if (mask is IPageOverlay overlay)
                        await overlay.OnOverlayClosingAsync();

                    viewHost.RemoveView(mask.NativeView);

                    if (!mask.IsDisposed)
                    {
                        try { mask.Dispose(); } catch { }
                    }
                }

                // For background loads the user may have navigated away (or the page may
                // have been disposed) while we were loading. Only apply the result when
                // this page is still the live, attached one (A-5).
                if (guardApply && (page.IsDisposed || !ReferenceEquals(Current, page)))
                    return;

                await bg.ApplyBackgroundResultAsync();
            }
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

                    // Drop weak entries whose page was collected/disposed so dead
                    // slots don't accumulate over the app's lifetime (L-5).
                    CompactWeakCache();

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

        // Synchronous on purpose: there is no async teardown work here, so a Task-returning
        // signature would only mislead callers (A-8).
        private void Cleanup(IPageView page, PageDescriptor descriptor, bool forceDispose)
        {
            if (page == null || page.IsDisposed)
                return;

            if (forceDispose)
            {
                if (descriptor != null)
                    RemoveFromCaches(descriptor.PageType, page);

                DisposePage(page);
                return;
            }

            if (descriptor != null && descriptor.ReusePolicy == PageReusePolicy.Transient)
            {
                DisposePage(page);
            }
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

        // Remove weak-cache entries whose target has been collected or disposed,
        // so the dictionary doesn't grow stale slots indefinitely (L-5).
        private void CompactWeakCache()
        {
            List<Type> dead = null;

            foreach (var kvp in _weakCache)
            {
                if (!kvp.Value.TryGetTarget(out var page) || page == null || page.IsDisposed)
                    (dead ??= new List<Type>()).Add(kvp.Key);
            }

            if (dead != null)
            {
                foreach (var key in dead)
                    _weakCache.Remove(key);
            }
        }

        private void OnInteractionDetected()
        {
            // intentionally empty (extension point)
        }

        // Dispose every cached page instance (singleton + live weak targets) and
        // clear the caches. Called on Reset/Dispose so cached pages holding
        // unmanaged resources don't outlive the runtime.
        private void DisposeCachedPages()
        {
            foreach (var page in _strongCache.Values)
                DisposePage(page);
            _strongCache.Clear();

            foreach (var weak in _weakCache.Values)
            {
                if (weak.TryGetTarget(out var page))
                    DisposePage(page);
            }
            _weakCache.Clear();
        }
    }
}