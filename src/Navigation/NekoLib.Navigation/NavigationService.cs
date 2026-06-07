// FILE: PageNav.Core/Services/NavigationService.cs
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Runtime.Core;
using NekoLib.Navigation.Runtime.History;
using NekoLib.Navigation.Runtime.Session;

using System;
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

        /// <summary>
        /// The framework-owned mutable session. Use
        /// <c>NavigationService.Session.SignIn("admin")</c> / <c>SignOut()</c> from
        /// view-models; role/auth guards see the change immediately on the next
        /// navigation.
        /// </summary>
        public static NavigationSession Session => EnsureContext().Session;

        /// <summary>Back/forward stacks for the active context.</summary>
        public static NavigationHistory History => EnsureContext().History;

        /// <summary>True when there is at least one entry on the back-stack.</summary>
        public static bool CanGoBack => EnsureContext().History.CanGoBack;

        /// <summary>
        /// Navigation diagnostics hub (NavigationLogged / GuardDenied). Use this
        /// instead of holding a reference to the NavigationContext just to subscribe.
        /// </summary>
        public static NavigationEventHub Events => EnsureContext().Events;

        private static NavigationContext EnsureContext()
        {
            if (_context == null)
                throw new InvalidOperationException(
                    "NavigationService.UseContext must be called first.");
            return _context;
        }

        // -------------------------------------------------------------------------
        // PUBLIC EVENTS (forwarded from context)
        // -------------------------------------------------------------------------
        // These are static events, which means every subscriber is a GC root for
        // the AppDomain lifetime unless it unsubscribes explicitly (L-4).
        // Shutdown() nulls all of them so that a login/logout cycle (UseContext →
        // Shutdown → UseContext) releases all subscribers without requiring callers
        // to unsubscribe manually. Callers that span multiple sessions SHOULD still
        // unsubscribe when they no longer need the events, but a missed unsubscribe
        // will not leak past the next Shutdown() call.

        public static event Action<IPageView, Type, NavigationArgs> Navigating;
        public static event Action<IPageView, IPageView, NavigationArgs> Navigated;
        public static event Action<IPageView, Type, Exception> NavigationFailed;
        public static event Action<IPageView> CurrentChanged;
        public static event Action HistoryChanged;
        public static event Action<IPageView> OnFirstPageAttached;
        public static event Action OnNoPageAttached;
        public static event Action OnNoPageVisible;

        // -------------------------------------------------------------------------
        // INIT / SHUTDOWN
        // -------------------------------------------------------------------------

        internal static void UseContext(NavigationContext context)
        {
            // Release-safe guards (S-2). Initializing twice without Shutdown() would
            // leak the previous runtime's event subscriptions and services, so this
            // must throw in Release as well as Debug.
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (_context != null)
                throw new InvalidOperationException(
                    "NavigationService.UseContext called twice without Shutdown().");

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

            // Release all external subscribers so they are not kept alive past this
            // session. A subsequent UseContext() starts with a clean slate (L-4).
            Navigating = null;
            Navigated = null;
            NavigationFailed = null;
            CurrentChanged = null;
            HistoryChanged = null;
            OnFirstPageAttached = null;
            OnNoPageAttached = null;
            OnNoPageVisible = null;
        }

        // -------------------------------------------------------------------------
        // PUBLIC API (forwarders)
        // -------------------------------------------------------------------------

        public static Task SwitchPage<T>(object args = null) where T : IPageView => EnsureRuntime().NavigateAsync(typeof(T), NavigationArgs.Default(args));

        public static Task SwitchPage(Type type, object args = null) => EnsureRuntime().NavigateAsync(type, NavigationArgs.Default(args));

        public static Task SwitchTransient<T>(object args = null) where T : IPageView => EnsureRuntime().NavigateAsync(typeof(T), NavigationArgs.Transient(args));

        public static Task SwitchTransient(Type type, object args = null) => EnsureRuntime().NavigateAsync(type, NavigationArgs.Transient(args));



        public async static Task GoIdleAsync() => await EnsureRuntime().GoIdleAsync();
        public async static Task<bool> GoBackAsync() => await EnsureRuntime().GoBackAsync();

        /// <summary>
        /// Tears down every page (current, cached, overlays), clears history, and
        /// leaves the runtime ready to navigate again. The context, session and
        /// adapter stay alive — use <see cref="Shutdown"/> if you want a full
        /// teardown.
        /// </summary>
        public static Task ResetAsync() => EnsureRuntime().ResetAsync();

        // ------------------------------------------------------------
        // Toast (fire-and-forget, ephemeral)
        // ------------------------------------------------------------

        public static void ShowToast<TToast>(object payload = null, int durationMs = 3000)
            where TToast : class, IToastView
        {
            EnsureRuntime().ShowToast<TToast>(payload, durationMs);
        }

        public static void DismissCurrentToast()
        {
            EnsureRuntime().DismissCurrentToast();
        }

        // ------------------------------------------------------------
        // Dialog (modal, binary outcome -> bool)
        // ------------------------------------------------------------

        public static Task<bool> ShowDialogAsync<TDialog>(object payload = null)
            where TDialog : class, IDialogView
        {
            return EnsureRuntime().ShowDialogAsync<TDialog>(payload);
        }

        // ------------------------------------------------------------
        // Prompt (modal, typed user input)
        // ------------------------------------------------------------

        public static Task<TResult> ShowPromptAsync<TPrompt, TResult>(object payload = null)
            where TPrompt : class, IPromptView<TResult>
        {
            return EnsureRuntime().ShowPromptAsync<TPrompt, TResult>(payload);
        }

        // ------------------------------------------------------------
        // Popover (non-blocking, light-dismiss via IUnfocusAware)
        // ------------------------------------------------------------

        public static Task<bool> ShowPopoverAsync<TPopover>(object payload = null)
            where TPopover : class, IPopoverView
        {
            return EnsureRuntime().ShowPopoverAsync<TPopover>(payload);
        }


#if DEBUG
        public static void AssertFrameworkIsDown()
        {
            if (_context != null)
                throw new InvalidOperationException("NavigationContext is still alive.");
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
            _runtime.OnFirstPageAttached += OnFirstPageAttachedInternal;
            _runtime.OnNoPageAttached += OnNoPageAttachedInternal;
            _runtime.OnNoPageVisible += OnNoPageVisibleInternal;
        }

        private static void UnwireRuntimeEvents()
        {
            _runtime.Navigating -= OnNavigating;
            _runtime.Navigated -= OnNavigated;
            _runtime.NavigationFailed -= OnNavigationFailed;
            _runtime.CurrentChanged -= OnCurrentChanged;
            _runtime.HistoryChanged -= OnHistoryChanged;
            _runtime.OnFirstPageAttached -= OnFirstPageAttachedInternal;
            _runtime.OnNoPageAttached -= OnNoPageAttachedInternal;
            _runtime.OnNoPageVisible -= OnNoPageVisibleInternal;
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

        private static void OnFirstPageAttachedInternal(IPageView page)
            => OnFirstPageAttached?.Invoke(page);

        private static void OnNoPageAttachedInternal()
            => OnNoPageAttached?.Invoke();

        private static void OnNoPageVisibleInternal()
            => OnNoPageVisible?.Invoke();
    
    }
}