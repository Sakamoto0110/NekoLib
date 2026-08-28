// FILE: PageNav.Core/Services/NavigationService.cs
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Runtime.Core;
using NekoLib.Navigation.Runtime.History;
using NekoLib.Navigation.Runtime.Session;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace NekoLib.Navigation 
{
    /// <summary>
    /// Static facade over the active navigation context created by
    /// <see cref="Bootstrap.PageNavBootstrap.Start"/>. This is the intended
    /// application-facing entrypoint for view-models and code-behind after
    /// bootstrap has completed.
    /// </summary>
    public static partial class NavigationService
    {
        private static NavigationContext? _context;
        private static NavigationRuntime? _runtime;
        private static IDisposable? _observationLifetime;
        private static IDisposable? _bootstrapLifetime;
        private static readonly object _lifecycleSync = new object();
        private static Task? _shutdownTask;
        private static int _shuttingDown;
        private static int _activeRuntimeOperations;
        private static TaskCompletionSource<bool>? _runtimeOperationsDrained;
        // -------------------------------------------------------------------------
        // PUBLIC STATE
        // -------------------------------------------------------------------------

        /// <summary>Gets the current page, or <see langword="null"/> before mounting or after teardown.</summary>
        public static IPageView? Current => _runtime?.Current;

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
        // Shutdown() nulls all of them so that a login/logout cycle (UseContext ->
        // Shutdown -> UseContext) releases all subscribers without requiring callers
        // to unsubscribe manually. Callers that span multiple sessions SHOULD still
        // unsubscribe when they no longer need the events, but a missed unsubscribe
        // will not leak past the next Shutdown() call.

        /// <summary>
        /// Raised on the UI thread after target resolution and before guard
        /// evaluation. Arguments contain descriptor-effective metadata.
        /// Subscriber exceptions are isolated.
        /// </summary>
        public static event Action<IPageView?, Type, NavigationArgs>? Navigating;

        /// <summary>
        /// Raised on the UI thread after the target completes its synchronous
        /// navigation lifecycle. Subscriber exceptions are isolated.
        /// </summary>
        public static event Action<IPageView?, IPageView, NavigationArgs>? Navigated;

        /// <summary>
        /// Raised for navigation exceptions after diagnostics capture. Guard
        /// denials and normal redirects are outcomes, not failures.
        /// </summary>
        public static event Action<IPageView?, Type, Exception>? NavigationFailed;

        /// <summary>Raised after the current page reference changes; the value may be <see langword="null"/> during teardown.</summary>
        public static event Action<IPageView?>? CurrentChanged;

        /// <summary>Raised after a committed change to the active context's history stacks.</summary>
        public static event Action? HistoryChanged;

        /// <summary>Raised when the first native page view becomes attached to an empty host.</summary>
        public static event Action<IPageView>? OnFirstPageAttached;

        /// <summary>Raised when no page views remain attached to the host.</summary>
        public static event Action? OnNoPageAttached;

        /// <summary>Raised when attached page views exist but none is visible.</summary>
        public static event Action? OnNoPageVisible;

        // -------------------------------------------------------------------------
        // INIT / SHUTDOWN
        // -------------------------------------------------------------------------

        internal static void UseContext(NavigationContext context)
            => UseContext(context, null, null);

        internal static void UseContext(
            NavigationContext context,
            IDisposable? observationLifetime)
            => UseContext(context, observationLifetime, null);

        internal static void UseContext(
            NavigationContext context,
            IDisposable? observationLifetime,
            IDisposable? bootstrapLifetime)
        {
            Exception? mountError = null;

            // Release-safe guards (S-2). Initializing twice without Shutdown() would
            // leak the previous runtime's event subscriptions and services, so this
            // must throw in Release as well as Debug.
            lock (_lifecycleSync)
            {
                if (context == null)
                {
                    mountError = new ArgumentNullException(nameof(context));
                }
                else if (_shuttingDown != 0 || _shutdownTask != null)
                {
                    mountError = new InvalidOperationException(
                        "NavigationService cannot mount a context while shutdown is in progress.");
                }
                else if (_context != null)
                {
                    mountError = new InvalidOperationException(
                        "NavigationService.UseContext called twice without Shutdown().");
                }
                else
                {
                    try
                    {
                        _context = context;
                        _runtime = new NavigationRuntime(context);
                        _observationLifetime = observationLifetime;
                        _bootstrapLifetime = bootstrapLifetime;
                        WireRuntimeEvents(_runtime);
                    }
                    catch (Exception ex)
                    {
                        _context = null!;
                        _runtime = null!;
                        _observationLifetime = null;
                        _bootstrapLifetime = null;
                        mountError = ex;
                    }
                }
            }

            if (mountError == null)
                return;

            // The caller has already created context-scoped native resources and
            // observability subscriptions. A rejected mount still owns those
            // arguments and must release them, but never while holding the
            // lifecycle lock because disposal may call back into the facade.
            DisposeMountHandles(observationLifetime, bootstrapLifetime);
            throw mountError;
        }
     
        /// <summary>
        /// Stops admission, waits for admitted operations, tears down the runtime,
        /// releases composition lifetimes, and clears every static subscriber.
        /// Concurrent callers receive the same task.
        /// </summary>
        /// <returns>A task that completes when the static facade is fully unmounted.</returns>
        public static Task Shutdown()
        {
            TaskCompletionSource<bool> completion;
            Task sharedTask;
            NavigationContext? context;
            NavigationRuntime? runtime;
            IDisposable? observationLifetime;
            IDisposable? bootstrapLifetime;
            Task runtimeOperationsDrained;

            lock (_lifecycleSync)
            {
                if (_shutdownTask != null)
                    return _shutdownTask;

                Volatile.Write(ref _shuttingDown, 1);
                completion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                sharedTask = completion.Task;
                _shutdownTask = sharedTask;

                context = _context;
                runtime = _runtime;
                observationLifetime = _observationLifetime;
                bootstrapLifetime = _bootstrapLifetime;

                if (_activeRuntimeOperations == 0)
                {
                    runtimeOperationsDrained = Task.CompletedTask;
                    _runtimeOperationsDrained = null;
                }
                else
                {
                    _runtimeOperationsDrained = new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    runtimeOperationsDrained = _runtimeOperationsDrained.Task;
                }
            }

            _ = CompleteShutdownAsync(
                context,
                runtime,
                observationLifetime,
                bootstrapLifetime,
                runtimeOperationsDrained,
                completion);

            return sharedTask;
        }

        private static async Task CompleteShutdownAsync(
            NavigationContext? context,
            NavigationRuntime? runtime,
            IDisposable? observationLifetime,
            IDisposable? bootstrapLifetime,
            Task runtimeOperationsDrained,
            TaskCompletionSource<bool> completion)
        {
            Exception? shutdownError = null;

            try
            {
                // Stop idle callbacks before disposing the runtime. The full
                // lifetime stays alive until afterwards because the runtime still
                // owns a subscription to the shared interaction observer.
                if (bootstrapLifetime is Bootstrap.NavigationBootstrapLifetime lifetime)
                    lifetime.StopIdle();
            }
            catch (Exception ex)
            {
                shutdownError = ex;
            }

            try
            {
                // An operation admitted before Shutdown owns a lease on this exact
                // runtime. Let it finish before teardown; admission is already closed.
                await runtimeOperationsDrained.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                shutdownError ??= ex;
            }

            try
            {
                // Keep the facade forwarding and Inspection subscriptions alive for
                // the runtime teardown itself. This lets teardown diagnostics reach
                // the same consumers as ordinary navigation operations.
                if (runtime != null)
                    await runtime.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                shutdownError ??= ex;
            }

            lock (_lifecycleSync)
            {
                try
                {
                    if (runtime != null && ReferenceEquals(_runtime, runtime))
                        UnwireRuntimeEvents(runtime);
                }
                catch (Exception ex)
                {
                    shutdownError ??= ex;
                }

                if (ReferenceEquals(_runtime, runtime))
                    _runtime = null!;
                if (ReferenceEquals(_context, context))
                    _context = null!;
                if (ReferenceEquals(_observationLifetime, observationLifetime))
                    _observationLifetime = null;
                if (ReferenceEquals(_bootstrapLifetime, bootstrapLifetime))
                    _bootstrapLifetime = null;

                // Release all external subscribers so they are not kept alive past
                // this session. A subsequent UseContext() starts clean (L-4).
                Navigating = null;
                Navigated = null;
                NavigationFailed = null;
                CurrentChanged = null;
                HistoryChanged = null;
                OnFirstPageAttached = null;
                OnNoPageAttached = null;
                OnNoPageVisible = null;
            }

            // Dispose the bootstrap observer/timer only after the runtime has
            // unsubscribed from it. Both handles are idempotent.
            DisposeMountHandles(observationLifetime, bootstrapLifetime);

            lock (_lifecycleSync)
            {
                Volatile.Write(ref _shuttingDown, 0);
                if (ReferenceEquals(_shutdownTask, completion.Task))
                    _shutdownTask = null;
                _runtimeOperationsDrained = null;

                if (shutdownError == null)
                    completion.TrySetResult(true);
                else
                    completion.TrySetException(shutdownError);
            }
        }

        // -------------------------------------------------------------------------
        // PUBLIC API (forwarders)
        // -------------------------------------------------------------------------

        /// <summary>
        /// Navigate to a registered page using an immutable request and return its
        /// normal success, denial, or redirect outcome.
        /// </summary>
        public static Task<NavigationResult> SwitchPage<T>(NavigationArgs? args = null)
            where T : IPageView =>
            InvokeRuntimeAsync(runtime =>
                runtime.NavigateAsync(typeof(T), args));

        /// <summary>
        /// Navigate to a registered runtime page type using an immutable request.
        /// </summary>
        public static Task<NavigationResult> SwitchPage(
            Type type,
            NavigationArgs? args = null) =>
            InvokeRuntimeAsync(runtime =>
                runtime.NavigateAsync(type, args));

        /// <summary>Navigate to the configured idle page if one can be resolved.</summary>
        public async static Task GoIdleAsync() =>
            await InvokeRuntimeAsync(runtime => runtime.GoIdleAsync());

        /// <summary>Navigate to the most recent back-stack entry, if any.</summary>
        public async static Task<bool> GoBackAsync() =>
            await InvokeRuntimeAsync(runtime => runtime.GoBackAsync());

        /// <summary>
        /// Tears down every page (current, cached, overlays), clears history, and
        /// leaves the runtime ready to navigate again. The context, session and
        /// adapter stay alive; use <see cref="Shutdown"/> if you want a full
        /// teardown.
        /// </summary>
        public static Task ResetAsync() =>
            InvokeRuntimeAsync(runtime => runtime.ResetAsync());

        // ------------------------------------------------------------
        // Toast (fire-and-forget, ephemeral)
        // ------------------------------------------------------------

        /// <summary>Show a non-blocking toast surface.</summary>
        public static void ShowToast<TToast>(object? payload = null, int durationMs = 3000)
            where TToast : class, IToastView
        {
            AdmitRuntimeAction((runtime, admissionCompleted) =>
                runtime.ShowToast<TToast>(
                    payload!,
                    durationMs,
                    admissionCompleted));
        }

        /// <summary>Dismiss the current toast, if one is visible.</summary>
        public static void DismissCurrentToast()
        {
            AdmitRuntimeAction((runtime, admissionCompleted) =>
                runtime.DismissCurrentToast(admissionCompleted));
        }

        // ------------------------------------------------------------
        // Dialog (modal, binary outcome -> bool)
        // ------------------------------------------------------------

        /// <summary>Show a modal binary dialog and await its confirm/cancel result.</summary>
        public static Task<bool> ShowDialogAsync<TDialog>(object? payload = null)
            where TDialog : class, IDialogView
        {
            return AdmitRuntimeTask((runtime, admissionCompleted) =>
                runtime.ShowDialogAsync<TDialog>(
                    payload!,
                    admissionCompleted));
        }

        // ------------------------------------------------------------
        // Prompt (modal, typed user input)
        // ------------------------------------------------------------

        /// <summary>Show a modal prompt and await its typed result.</summary>
        public static Task<TResult?> ShowPromptAsync<TPrompt, TResult>(object? payload = null)
            where TPrompt : class, IPromptView<TResult>
        {
            return AdmitRuntimeTask((runtime, admissionCompleted) =>
                runtime.ShowPromptAsync<TPrompt, TResult>(
                    payload!,
                    admissionCompleted));
        }

        // ------------------------------------------------------------
        // Popover (non-blocking, light-dismiss via IUnfocusAware)
        // ------------------------------------------------------------

        /// <summary>Show a non-blocking popover and await its completion result.</summary>
        public static Task<bool> ShowPopoverAsync<TPopover>(object? payload = null)
            where TPopover : class, IPopoverView
        {
            return AdmitRuntimeTask((runtime, admissionCompleted) =>
                runtime.ShowPopoverAsync<TPopover>(
                    payload!,
                    admissionCompleted));
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

        private static RuntimeLease AcquireRuntimeLease()
        {
            lock (_lifecycleSync)
            {
                if (_shuttingDown != 0 || _shutdownTask != null)
                    throw new InvalidOperationException(
                        "NavigationService is shutting down and cannot accept new operations.");

                if (_runtime == null)
                    throw new InvalidOperationException(
                        "NavigationService is not mounted. Call PageNavBootstrap.Start() " +
                        "to mount a NavigationContext before using navigation, and await " +
                        "NavigationService.Shutdown() before mounting a new one.");

                _activeRuntimeOperations++;
                return new RuntimeLease(_runtime);
            }
        }

        private static Task InvokeRuntimeAsync(
            Func<NavigationRuntime, Task> operation)
        {
            var lease = AcquireRuntimeLease();
            try
            {
                return AwaitRuntimeOperationAsync(operation(lease.Runtime), lease);
            }
            catch
            {
                lease.Dispose();
                throw;
            }
        }

        private static Task<TResult> InvokeRuntimeAsync<TResult>(
            Func<NavigationRuntime, Task<TResult>> operation)
        {
            var lease = AcquireRuntimeLease();
            try
            {
                return AwaitRuntimeOperationAsync(operation(lease.Runtime), lease);
            }
            catch
            {
                lease.Dispose();
                throw;
            }
        }

        private static async Task AwaitRuntimeOperationAsync(
            Task operation,
            RuntimeLease lease)
        {
            try
            {
                await operation.ConfigureAwait(false);
            }
            finally
            {
                lease.Dispose();
            }
        }

        private static async Task<TResult> AwaitRuntimeOperationAsync<TResult>(
            Task<TResult> operation,
            RuntimeLease lease)
        {
            try
            {
                return await operation.ConfigureAwait(false);
            }
            finally
            {
                lease.Dispose();
            }
        }

        private static void AdmitRuntimeAction(
            Action<NavigationRuntime, Action> operation)
        {
            var lease = AcquireRuntimeLease();
            try
            {
                operation(lease.Runtime, lease.Dispose);
            }
            catch
            {
                lease.Dispose();
                throw;
            }
        }

        private static Task<TResult> AdmitRuntimeTask<TResult>(
            Func<NavigationRuntime, Action, Task<TResult>> operation)
        {
            var lease = AcquireRuntimeLease();
            try
            {
                return operation(lease.Runtime, lease.Dispose);
            }
            catch
            {
                lease.Dispose();
                throw;
            }
        }

        private static void ReleaseRuntimeLease()
        {
            TaskCompletionSource<bool>? drained = null;

            lock (_lifecycleSync)
            {
                if (_activeRuntimeOperations <= 0)
                    throw new InvalidOperationException(
                        "NavigationService runtime lease accounting underflow.");

                _activeRuntimeOperations--;
                if (_activeRuntimeOperations == 0 && _shuttingDown != 0)
                    drained = _runtimeOperationsDrained;
            }

            drained?.TrySetResult(true);
        }

        internal static bool IsCurrentContext(NavigationContext context)
            => context != null &&
               Volatile.Read(ref _shuttingDown) == 0 &&
               ReferenceEquals(_context, context);

        private static void DisposeMountHandles(
            IDisposable? observationLifetime,
            IDisposable? bootstrapLifetime)
        {
            try { observationLifetime?.Dispose(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[NavigationService] Inspection observer disposal failed: " + ex);
            }

            try { bootstrapLifetime?.Dispose(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[NavigationService] Bootstrap lifetime disposal failed: " + ex);
            }
        }

        private sealed class RuntimeLease : IDisposable
        {
            private NavigationRuntime? _runtime;

            internal RuntimeLease(NavigationRuntime runtime)
            {
                _runtime = runtime ??
                    throw new ArgumentNullException(nameof(runtime));
            }

            internal NavigationRuntime Runtime =>
                _runtime ??
                throw new ObjectDisposedException(nameof(RuntimeLease));

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _runtime, null) != null)
                    ReleaseRuntimeLease();
            }
        }

        private static void WireRuntimeEvents(NavigationRuntime runtime)
        {
            runtime.Navigating += OnNavigating;
            runtime.Navigated += OnNavigated;
            runtime.NavigationFailed += OnNavigationFailed;
            runtime.CurrentChanged += OnCurrentChanged;
            runtime.HistoryChanged += OnHistoryChanged;
            runtime.OnFirstPageAttached += OnFirstPageAttachedInternal;
            runtime.OnNoPageAttached += OnNoPageAttachedInternal;
            runtime.OnNoPageVisible += OnNoPageVisibleInternal;
        }

        private static void UnwireRuntimeEvents(NavigationRuntime runtime)
        {
            runtime.Navigating -= OnNavigating;
            runtime.Navigated -= OnNavigated;
            runtime.NavigationFailed -= OnNavigationFailed;
            runtime.CurrentChanged -= OnCurrentChanged;
            runtime.HistoryChanged -= OnHistoryChanged;
            runtime.OnFirstPageAttached -= OnFirstPageAttachedInternal;
            runtime.OnNoPageAttached -= OnNoPageAttachedInternal;
            runtime.OnNoPageVisible -= OnNoPageVisibleInternal;
        }

        private static void OnNavigating(IPageView? from, Type to, NavigationArgs args)
            => InvokeSubscribers(
                Navigating,
                nameof(Navigating),
                subscriber => ((Action<IPageView?, Type, NavigationArgs>)subscriber)(from, to, args));

        private static void OnNavigated(IPageView? from, IPageView to, NavigationArgs args)
            => InvokeSubscribers(
                Navigated,
                nameof(Navigated),
                subscriber => ((Action<IPageView?, IPageView, NavigationArgs>)subscriber)(from, to, args));

        private static void OnNavigationFailed(IPageView? from, Type to, Exception ex)
            => InvokeSubscribers(
                NavigationFailed,
                nameof(NavigationFailed),
                subscriber => ((Action<IPageView?, Type, Exception>)subscriber)(from, to, ex));

        private static void OnCurrentChanged(IPageView? current)
            => InvokeSubscribers(
                CurrentChanged,
                nameof(CurrentChanged),
                subscriber => ((Action<IPageView?>)subscriber)(current));

        private static void OnHistoryChanged()
            => InvokeSubscribers(
                HistoryChanged,
                nameof(HistoryChanged),
                subscriber => ((Action)subscriber)());

        private static void OnFirstPageAttachedInternal(IPageView page)
            => InvokeSubscribers(
                OnFirstPageAttached,
                nameof(OnFirstPageAttached),
                subscriber => ((Action<IPageView>)subscriber)(page));

        private static void OnNoPageAttachedInternal()
            => InvokeSubscribers(
                OnNoPageAttached,
                nameof(OnNoPageAttached),
                subscriber => ((Action)subscriber)());

        private static void OnNoPageVisibleInternal()
            => InvokeSubscribers(
                OnNoPageVisible,
                nameof(OnNoPageVisible),
                subscriber => ((Action)subscriber)());

        private static void InvokeSubscribers(
            Delegate? subscribers,
            string eventName,
            Action<Delegate> invoke)
        {
            if (subscribers == null)
                return;

            foreach (var subscriber in subscribers.GetInvocationList())
            {
                try
                {
                    invoke(subscriber);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[NavigationService] " + eventName +
                        " subscriber failed: " + ex);
                }
            }
        }
    
    }
}
