// FILE: PageNav.Core/Services/NavigationService.cs
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Runtime.Core;
using NekoLib.Navigation.Runtime.Registry;

using System;
using System.Linq;
using System.Threading.Tasks;

namespace NekoLib.Navigation 
{
    /// <summary>
    /// Thin static facade over a default NavigationContext.
    /// Keeps legacy call sites working while the framework becomes instance-based.
    /// </summary>
    public static partial class NavigationService
    {
        private static NavigationContext _context;
        private static NavigationRuntime _runtime;
        // -------------------------------------------------------------------------
        // PUBLIC STATE
        // -------------------------------------------------------------------------

        public static IPageView Current => _runtime?.Current;

        // -------------------------------------------------------------------------
        // PUBLIC EVENTS (forwarded from context)
        // -------------------------------------------------------------------------

        public static event Action<IPageView, Type, NavigationArgs> Navigating;
        public static event Action<IPageView, IPageView, NavigationArgs> Navigated;
        public static event Action<IPageView, Type, Exception> NavigationFailed;
        public static event Action<IPageView> CurrentChanged;
        public static event Action HistoryChanged;
        public static event Action<IPageView> OnFirstPageAttached;
        public static event Action OnNoPageAttached;
        public static event Action OnNoPageVisible;
 
        private static int _attachedPages;
        private static int _visiblePages;

         
        // -------------------------------------------------------------------------
        // INIT / SHUTDOWN
        // -------------------------------------------------------------------------

        public static void UseContext(NavigationContext context)
        {
#if DEBUG
            if(context == null)
                throw new ArgumentNullException(nameof(context));
            if (_context != null)
                throw new InvalidOperationException(
                    "NavigationService.Initialize called twice without Shutdown().");
#endif
            _context = context;
            _runtime = new NavigationRuntime(context);

            WireRuntimeEvents();

        }
     
        public static async Task Shutdown()
        {
            if (_runtime != null)
            {
                UnwireRuntimeEvents();
                await _runtime.DisposeAsync();
                _runtime = null;
            }

            _context = null;
        }

        // -------------------------------------------------------------------------
        // PUBLIC API (forwarders)
        // -------------------------------------------------------------------------

        public static Task SwitchPage<T>(object args = null)
            where T : IPageView
            => EnsureRuntime().NavigateAsync(typeof(T), NavigationArgs.Default(args));

        public static Task SwitchPage(Type type, object args = null)
            => EnsureRuntime().NavigateAsync(type, NavigationArgs.Default(args));

        public static Task SwitchTransient<T>(object args = null)
            where T : IPageView
            => EnsureRuntime().NavigateAsync(typeof(T), NavigationArgs.Transient(args));

        public static Task SwitchTransient(Type type, object args = null)
            => EnsureRuntime().NavigateAsync(type, NavigationArgs.Transient(args));

        public static async Task GoHomeAsync(object args = null)
        {
            // This still relies on PageRegistry metadata (static is fine).
            var desc = PageRegistry.ResolveTimeoutTarget();
            if (desc == null)
                return;

            await EnsureRuntime().NavigateAsync(desc.PageType, NavigationArgs.Default(args));
        }

        public static Task<bool> GoBackAsync()
    => EnsureRuntime().GoBackAsync();


#if DEBUG
        public static void AssertFrameworkIsDown()
        {
            if (_context != null)
                throw new InvalidOperationException("NavigationContext is still alive.");

            var leaks = PageLifecycleTracker
                .SuspectedLeaks(TimeSpan.FromSeconds(1))
                .ToList();

            if (leaks.Count > 0)
            {
                throw new InvalidOperationException(
                    "Leaked pages detected:\n" +
                    string.Join("\n", leaks.Select(l =>
                        $"{l.Name} ({l.State}, policy={l.CachePolicy})")));
            }
        }
#endif

        // -------------------------------------------------------------------------
        // INTERNALS
        // -------------------------------------------------------------------------

        private static NavigationRuntime EnsureRuntime()
        {
            if (_runtime == null)
                throw new InvalidOperationException(
                    "NavigationService.Initialize must be called first.");

            return _runtime;
        }

        private static void WireRuntimeEvents()
        {
            _runtime.Navigating += OnNavigating;
            _runtime.Navigated += OnNavigated;
            _runtime.NavigationFailed += OnNavigationFailed;
            _runtime.CurrentChanged += OnCurrentChanged;
            _runtime.HistoryChanged += OnHistoryChanged;
             
            _runtime.TimeoutReached += OnTimeout;
        }

        private static void UnwireRuntimeEvents()
        {
            _runtime.TimeoutReached -= OnTimeout;

            _runtime.Navigating -= OnNavigating;
            _runtime.Navigated -= OnNavigated;
            _runtime.NavigationFailed -= OnNavigationFailed;
            _runtime.CurrentChanged -= OnCurrentChanged;
            _runtime.HistoryChanged -= OnHistoryChanged;
        }
        
        private static void OnNavigating(IPageView from, Type to, NavigationArgs args)
            => Navigating?.Invoke(from, to, args);

        private static void OnNavigated(IPageView from, IPageView to, NavigationArgs args)
            => Navigated?.Invoke(from, to, args);

        private static void OnNavigationFailed(IPageView from, Type to, Exception ex)
            => NavigationFailed?.Invoke(from, to, ex);

        private static void OnCurrentChanged(IPageView current)
            => CurrentChanged?.Invoke(current);

        private static void OnHistoryChanged()
            => HistoryChanged?.Invoke();
    }
}
