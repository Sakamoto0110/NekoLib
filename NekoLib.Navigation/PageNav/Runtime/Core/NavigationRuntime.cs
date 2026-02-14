using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Plataform;
using NekoLib.Navigation.Contracts.Runtime;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Runtime.Factories;
using NekoLib.Navigation.Runtime.Registry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Runtime.Core
{
    internal sealed class NavigationRuntime : IAsyncDisposable
    {
        private bool _isNavigating;
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
        private readonly Dictionary<Type, Stack<IPageView>> _stackCache = new();

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
        // PUBLIC API
        // ---------------------------------------------------------------------

        public Task NavigateAsync(Type pageType, NavigationArgs args)
        {
            EnsureRuntimeServices();
            return SwitchInternalAsync(pageType, args ?? NavigationArgs.Empty);
        }

        public async Task GoBackAsync()
        {
            EnsureRuntimeServices();

            var entry = _ctx.History.PopBack();
            if (entry == null)
                return;

            if (Current != null)
            {
                _ctx.History.PushForward(new PageHistoryEntry(
                    Current.GetType(),
                    Current.Name,
                    (Current as IPageStateful)?.CaptureState()
                ));
            }

            await SwitchInternalAsync(
                entry.PageType,
                NavigationArgs.Default(entry.State));
        }

        public async Task ResetAsync()
        {
            if (Current != null)
            {
                _ctx.Host.Detach(Current);

                if (PageRegistry.TryGetDescriptor(Current.GetType(), out var desc))
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
        }

        public async ValueTask DisposeAsync()
        {
            if (Current != null)
            {
                _ctx.Host.Detach(Current);

                if (PageRegistry.TryGetDescriptor(Current.GetType(), out var desc))
                    await CleanupAsync(Current, desc, forceDispose: true);
                else
                    DisposePage(Current);

                Current = null;
            }

            if (_interactionObserver != null)
                _interactionObserver.InteractionDetected -= OnInteractionDetected;
        }

        // ---------------------------------------------------------------------
        // CORE NAVIGATION
        // ---------------------------------------------------------------------

        private async Task SwitchInternalAsync(Type pageType, NavigationArgs navArgs)
        {
            if (pageType == null)
                throw new ArgumentNullException(nameof(pageType));

            if (_isNavigating)
                return;

            _isNavigating = true;

            IPageView from = Current;
            IPageView to = null;

            try
            {
                if (!PageRegistry.TryGetDescriptor(pageType, out var toDesc))
                    throw new InvalidOperationException(
                        $"Type '{pageType.FullName}' is not a registered page.");

                var canonicalPageType = toDesc.PageType;

                if (!typeof(IPageView).IsAssignableFrom(canonicalPageType))
                    throw new InvalidOperationException(
                        $"Navigation target '{canonicalPageType.FullName}' is not a page.");

                PageRegistry.TryGetDescriptor(from?.GetType(), out var fromDesc);

                Navigating?.Invoke(Current, canonicalPageType, navArgs);

                var factory = EnsurePageFactory();
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
            finally
            {
                _isNavigating = false;
            }
        }

        private static async Task LoadAsync(IPageView page, object payload)
        {
            if (page is IBackgroundLoadable bg)
            {
                await Task.Run(() => bg.LoadInBackgroundAsync(payload))
                          .ConfigureAwait(true);

                await bg.ApplyBackgroundResultAsync()
                        .ConfigureAwait(true);
            }
        }

        private IPageView ResolvePage(PageDescriptor d)
        {
            var factory = EnsurePageFactory();

            switch (d.ReusePolicy)
            {
                case PageReusePolicy.Transient:
                    return factory.Create(d.PageType);

                case PageReusePolicy.StrongSingleton:
                    if (_strongCache.TryGetValue(d.PageType, out var strong))
                    {
                        if (!strong.IsDisposed)
                            return strong;

                        _strongCache.Remove(d.PageType);
                    }

                    strong = factory.Create(d.PageType);
                    _strongCache[d.PageType] = strong;
                    return strong;

                case PageReusePolicy.WeakSingleton:
                    if (_weakCache.TryGetValue(d.PageType, out var weak) &&
                        weak.TryGetTarget(out var target) &&
                        !target.IsDisposed)
                    {
                        return target;
                    }

                    var newPage = factory.Create(d.PageType);
                    _weakCache[d.PageType] =
                        new WeakReference<IPageView>(newPage);
                    return newPage;

                case PageReusePolicy.Stackable:
                    var page = factory.Create(d.PageType);

                    if (!_stackCache.TryGetValue(d.PageType, out var stack))
                    {
                        stack = new Stack<IPageView>();
                        _stackCache[d.PageType] = stack;
                    }

                    stack.Push(page);
                    return page;

                default:
                    return factory.Create(d.PageType);
            }
        }



        // ---------------------------------------------------------------------
        // LIFECYCLE (MERGED)
        // ---------------------------------------------------------------------

        private Task CleanupAsync(
    IPageView page,
    PageDescriptor descriptor,
    bool forceDispose)
        {
            if (page == null || page.IsDisposed)
                return Task.CompletedTask;

            if (forceDispose)
            {
                RemoveFromCaches(descriptor.PageType, page);
                DisposePage(page);
                return Task.CompletedTask;
            }

            if (descriptor.ReusePolicy == PageReusePolicy.Transient)
            {
                DisposePage(page);
            }

            return Task.CompletedTask;
        }

        private void RemoveFromCaches(Type pageType, IPageView page)
        {
            // ------------------------------------------------------------
            // Strong cache
            // ------------------------------------------------------------
            if (_strongCache.TryGetValue(pageType, out var strong))
            {
                if (ReferenceEquals(strong, page))
                    _strongCache.Remove(pageType);
            }

            // ------------------------------------------------------------
            // Weak cache
            // ------------------------------------------------------------
            if (_weakCache.TryGetValue(pageType, out var weak))
            {
                if (weak.TryGetTarget(out var target))
                {
                    if (ReferenceEquals(target, page))
                        _weakCache.Remove(pageType);
                }
                else
                {
                    // Weak reference already dead
                    _weakCache.Remove(pageType);
                }
            }

            // ------------------------------------------------------------
            // Stack cache
            // ------------------------------------------------------------
            if (_stackCache.TryGetValue(pageType, out var stack) && stack.Count > 0)
            {
                var newStack = new Stack<IPageView>(
                    stack.Where(p => !ReferenceEquals(p, page))
                         .Reverse() // preserve original order
                );

                if (newStack.Count == 0)
                    _stackCache.Remove(pageType);
                else
                    _stackCache[pageType] = newStack;
            }
        }

        private void DisposePage(IPageView page)
        {
            if (page == null || page.IsDisposed)
                return;

            try
            {
                _dispatcher?.Invoke(() =>
                {
                    try { page.Dispose(); }
                    catch { }
                });
            }
            catch { }
        }

        // ---------------------------------------------------------------------
        // RUNTIME SERVICES
        // ---------------------------------------------------------------------

        private void EnsureRuntimeServices()
        {
            var services = _ctx.Services
                ?? throw new InvalidOperationException(
                    "NavigationContext.Services is not initialized.");

            if (_dispatcher == null)
            {
                if (!services.CanResolve(typeof(IEventDispatcherAdapter)))
                    throw new InvalidOperationException(
                        "IEventDispatcherAdapter is required but not registered.");

                _dispatcher =
                    (IEventDispatcherAdapter)services.Get(typeof(IEventDispatcherAdapter));
            }

            if (_interactionObserver == null &&
                services.CanResolve(typeof(IInteractionObserverService)))
            {
                _interactionObserver =
                    (IInteractionObserverService)services.Get(typeof(IInteractionObserverService));

                _interactionObserver.InteractionDetected += OnInteractionDetected;
            }
        }

        private PageFactory EnsurePageFactory()
        {
            if (_pageFactory != null)
                return _pageFactory;

            if (!_ctx.Services.CanResolve(typeof(PageFactory)))
                throw new InvalidOperationException(
                    "PageFactory is required but not registered.");

            _pageFactory = (PageFactory)_ctx.Services.Get(typeof(PageFactory));
            return _pageFactory;
        }

        private void OnInteractionDetected()
        {
            // intentionally empty (extension point)
        }
    }
}
