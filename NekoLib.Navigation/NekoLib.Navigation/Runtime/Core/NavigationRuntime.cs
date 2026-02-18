// FILE: NekoLib.Navigation.Runtime.Core/NavigationRuntime.cs

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

        private readonly HashSet<IPageView> _attachedPages = new();
        private readonly HashSet<IPageView> _visiblePages = new();

        public IPageView Current { get; private set; }

        // Runtime-owned caches (instance scoped)
        private readonly Dictionary<Type, IPageView> _strongCache = new();
        private readonly Dictionary<Type, WeakReference<IPageView>> _weakCache = new();

        // Modal stack (presentation stack)
        private readonly Stack<PageInstance> _modalStack = new();
        private readonly Stack<TaskCompletionSource<ModalResult>> _modalTcs = new();

        // Serialize ALL runtime mutations
        private readonly SemaphoreSlim _navGate = new(1, 1);

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
                .FirstOrDefault(x => x.Kind == PageKind.Home)
                ?? _ctx.Registry.AllDescriptors()
                    .FirstOrDefault(x => x.Tags.Contains("home", StringComparer.OrdinalIgnoreCase));
        }
        public async Task GoHomeAsync(   object args = null)
        {
            if (_ctx == null)
                throw new ArgumentNullException(nameof(_ctx));

            var desc = _ctx.Registry.ResolveTimeoutTarget();
            if (desc == null)
                return;

            var runtime = _ctx.Services.Get(typeof(NavigationRuntime)) as NavigationRuntime;
            await runtime.NavigateAsync(desc.PageType, NavigationArgs.Default(args));
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
                    // We want continuations to remain on the UI thread.
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
                    // IMPORTANT: do not ConfigureAwait(false) here.
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

        public Task<bool> GoBackAsync()
        {
            return ExecuteAsync(() =>
            {
                EnsureRuntimeServices();
                return GoBackInternalAsync();
            });
        }

        /// <summary>
        /// Explicit modal API. Returns a result when the modal closes.
        /// Requires the host to implement IModalHost.
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

                if (desc.Presentation != PagePresentation.Modal)
                    throw new InvalidOperationException($"Page '{desc.PageType.FullName}' is not marked as Modal.");

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

                await CloseAllModalsInternalAsync(ModalResult.Cancel());

                if (Current != null)
                {
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
            });
        }

        public ValueTask DisposeAsync()
        {
            return new ValueTask(ExecuteAsync(async () =>
            {
                EnsureRuntimeServices();

                await CloseAllModalsInternalAsync(ModalResult.Cancel());

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
            }));
        }

        // ---------------------------------------------------------------------
        // BACK (internal)
        // ---------------------------------------------------------------------

        private async Task<bool> GoBackInternalAsync()
        {
            // Modal takes priority
            if (_modalStack.Count > 0)
            {
                await CloseTopModalInternalAsync(ModalResult.Cancel());
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

        private async Task SwitchInternalAsync(
            Type pageType,
            NavigationArgs navArgs,
            int redirectDepth = 0,
            HashSet<Type> visited = null)
        {
            if (pageType == null)
                throw new ArgumentNullException(nameof(pageType));

            IPageView from = Current;
            IPageView to = null;

            try
            {
                if (!_ctx.Registry.TryGetDescriptor(pageType, out var toDesc))
                    throw new InvalidOperationException(
                        $"Type '{pageType.FullName}' is not a registered page.");

                var canonicalPageType = toDesc.PageType;

                visited ??= new HashSet<Type>();

                if (!visited.Add(canonicalPageType))
                {
                    NavigationDiagnostics.EmitGuardDenied(
                        from,
                        canonicalPageType,
                        null,
                        "Guard redirect cycle detected.");
                    return;
                }

                if (redirectDepth > 8)
                {
                    NavigationDiagnostics.EmitGuardDenied(
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
                        NavigationDiagnostics.EmitGuardDenied(
                            from,
                            canonicalPageType,
                            null,
                            $"Guard exception: {ex.Message}");
                        return;
                    }

                    if (!result.Allowed)
                    {
                        NavigationDiagnostics.EmitGuardDenied(
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

                // ---------------- PRESENTATION ----------------

                if (toDesc.Presentation == PagePresentation.Modal)
                {
                    // NavigateAsync can open modals, but doesn't return a result.
                    _ = await ShowModalInternalAsync(toDesc, navArgs);
                    return;
                }

                // Normal navigation cancels modals (kiosk-friendly)
                await CloseAllModalsInternalAsync(ModalResult.Cancel());

                // ---------------- NORMAL NAVIGATION ----------------

                if (!typeof(IPageView).IsAssignableFrom(canonicalPageType))
                    throw new InvalidOperationException(
                        $"Navigation target '{canonicalPageType.FullName}' is not a page.");

                _ctx.Registry.TryGetDescriptor(from?.GetType(), out var fromDesc);

                Navigating?.Invoke(Current, canonicalPageType, navArgs);

                to = ResolvePage(toDesc);

                if (from is IPageVisibility fromVis)
                    fromVis.HidePage();

                if (from is IPageLifecycle leave)
                    await leave.OnNavigatedFromAsync();

                if (from != null)
                {
                    _ctx.Host.Detach(from);

                    if (_visiblePages.Remove(from) && _visiblePages.Count == 0)
                        OnNoPageVisible?.Invoke();

                    if (_attachedPages.Remove(from) && _attachedPages.Count == 0)
                        OnNoPageAttached?.Invoke();

                    await CleanupAsync(from, fromDesc, forceDispose: false);
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

                await LoadAsync(to, navArgs.Payload);

                if (to is IPageLifecycle enter)
                    await enter.OnNavigatedToAsync(navArgs);

                if (!navArgs.Behavior.HasFlag(NavigationBehavior.NoHistory) && from != null)
                {
                    _ctx.History.Record(new PageHistoryEntry(
                        from.GetType(),
                        from.Name,
                        (from as IPageStateful)?.CaptureState()
                    ));

                    HistoryChanged?.Invoke();
                }

                Navigated?.Invoke(from, to, navArgs);
                NavigationDiagnostics.EmitSuccess(from, to, navArgs);
            }
            catch (Exception ex)
            {
                NavigationFailed?.Invoke(from, pageType, ex);
                NavigationDiagnostics.EmitFailure(from, to, navArgs);
                throw;
            }
        }

        private static async Task LoadAsync(IPageView page, object payload)
        {
            if (page is IBackgroundLoadable bg)
            {
                // Heavy work off-thread (no nested async delegate)
                await Task.Run(() => bg.LoadInBackgroundAsync(payload));

                // Apply should run back on UI (runtime already dispatches to UI)
                await bg.ApplyBackgroundResultAsync();
            }
        }

        // ---------------------------------------------------------------------
        // MODALS (ASSUME already UI-thread + serialized)
        // ---------------------------------------------------------------------

        private async Task<ModalResult> ShowModalInternalAsync(PageDescriptor descriptor, NavigationArgs args)
        {
            if (!(_ctx.Host is IModalHost modalHost))
                throw new InvalidOperationException("Current host does not support modal pages (IModalHost missing).");

            var view = ResolvePage(descriptor);

            var tcs = new TaskCompletionSource<ModalResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            _modalTcs.Push(tcs);
            _modalStack.Push(new PageInstance(view, descriptor));

            await modalHost.ShowModalAsync(view);

            return await tcs.Task;
        }

        private async Task<bool> CloseTopModalInternalAsync(ModalResult result)
        {
            if (_modalStack.Count == 0)
                return false;

            if (!(_ctx.Host is IModalHost modalHost))
                throw new InvalidOperationException("Current host does not support modal pages (IModalHost missing).");

            var instance = _modalStack.Pop();
            var tcs = _modalTcs.Pop();

            await modalHost.HideModalAsync(instance.View);

            if (instance.Descriptor.ReusePolicy == PageReusePolicy.Transient)
                DisposePage(instance.View);

            tcs.TrySetResult(result);
            return true;
        }

        private async Task CloseAllModalsInternalAsync(ModalResult result)
        {
            while (_modalStack.Count > 0)
                await CloseTopModalInternalAsync(result);
        }

        // ---------------------------------------------------------------------
        // PAGE RESOLUTION (lifecycle only)
        // ---------------------------------------------------------------------

        private IPageView ResolvePage(PageDescriptor d)
        {
            var factory = EnsurePageFactory();

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
        // LIFECYCLE
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

            try
            {
                EnsureDispatcher();
                _dispatcher.Invoke(() =>
                {
                    try { page.Dispose(); }
                    catch { }
                });
            }
            catch { }
        }

        // ---------------------------------------------------------------------
        // FACTORIES / SERVICES
        // ---------------------------------------------------------------------

        private PageFactory EnsurePageFactory()
        {
            if (_pageFactory != null)
                return _pageFactory;

            var services = _ctx.Services
                ?? throw new InvalidOperationException("NavigationContext.Services is not initialized.");

            if (!services.CanResolve(typeof(PageFactory)))
                throw new InvalidOperationException("PageFactory is required but not registered.");

            _pageFactory = (PageFactory)services.Get(typeof(PageFactory));
            return _pageFactory;
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

        private sealed class PageInstance
        {
            public IPageView View { get; }
            public PageDescriptor Descriptor { get; }

            public PageInstance(IPageView view, PageDescriptor descriptor)
            {
                View = view;
                Descriptor = descriptor;
            }
        }
    }
}
