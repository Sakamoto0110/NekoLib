// FILE: NekoLib.Navigation.Runtime.Core/NavigationRuntime.cs

using NekoLib.Navigation.Contracts.Guards;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Contracts.Runtime;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Runtime;
using NekoLib.Navigation.Runtime.Factories;
using NekoLib.Navigation.Runtime.Services;
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
        private readonly object _runtimeServicesSync = new object();

        private IEventDispatcherAdapter _dispatcher;
        private IInteractionObserverService _interactionObserver;
        private PageFactory _pageFactory;
        private IToastService _toastService;
        private IDialogService _dialogService;
        private IPromptService _promptService;
        private IPopoverService _popoverService;
        private IPageAwareInteractionBlocker? _pageAwareInteractionBlocker;

        private readonly HashSet<IPageView> _attachedPages = new HashSet<IPageView>();
        private readonly HashSet<IPageView> _visiblePages = new HashSet<IPageView>();
        private readonly NavigationDiagnostics _diagnostics;
        private readonly object _backgroundLoadSync = new object();
        private readonly HashSet<Task> _backgroundLoadWrappers =
            new HashSet<Task>();
        private TaskCompletionSource<string> _backgroundLoadCancellation =
            CreateBackgroundLoadCancellation();
        private bool _backgroundLoadsEnded;
        private int _queuedRequestCount;
        private int _backgroundLoadCount;
        private bool _firstPageAttachedRaised;

        private sealed class BackgroundLoadRegistration
        {
            internal TaskCompletionSource<string> Cancellation { get; } =
                CreateBackgroundLoadCancellation();

            internal Task Wrapper { get; set; } = Task.CompletedTask;
        }

        public NavigationEventHub Events => _diagnostics.Hub;
        internal string RuntimeId => _diagnostics.RuntimeId;

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
            EmitRuntimeState("Created");
        }

        // ---------------------------------------------------------------------
        // EXECUTION CORE (UI marshaling + serialization)
        // ---------------------------------------------------------------------

        private void EnsureDispatcher()
        {
            if (_dispatcher != null)
                return;

            lock (_runtimeServicesSync)
            {
                if (_dispatcher != null)
                    return;

                var services = _ctx.Services
                    ?? throw new InvalidOperationException("NavigationContext.Services is not initialized.");

                if (!services.CanResolve(typeof(IEventDispatcherAdapter)))
                    throw new InvalidOperationException("IEventDispatcherAdapter is required but not registered.");

                _dispatcher = (IEventDispatcherAdapter)services.Get(typeof(IEventDispatcherAdapter));
            }
        }

        // Resolution priority (role -> "idle" tag -> name contains "idle") lives in
        // IdlePageRules so the bootstrap's idle-timeout wiring and [PageTimeout]
        // placement validation share the exact same notion of "the idle page".
        private PageDescriptor ResolveIdleDescriptor()
            => IdlePageRules.Resolve(_ctx.Registry.AllDescriptors());

        public Task GoIdleAsync(object args = null)
        {
            var desc = ResolveIdleDescriptor();
            if (desc == null)
                return Task.CompletedTask;

            return StartNavigateRequest(
                desc.PageType,
                NavigationArgs.Default(args),
                NavigationTraceTrigger.Idle);
        }

        private void EnsureRuntimeServices()
        {
            lock (_runtimeServicesSync)
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

            if (_pageAwareInteractionBlocker == null &&
                services.CanResolve(typeof(IInteractionBlocker)))
            {
                _pageAwareInteractionBlocker =
                    services.Get(typeof(IInteractionBlocker))
                    as IPageAwareInteractionBlocker;
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
                AttachDiagnostics(_toastService);
                AttachInteractionBlocker(_toastService);
            }

            if (_dialogService == null && services.CanResolve(typeof(IDialogService)))
            {
                _dialogService = (IDialogService)services.Get(typeof(IDialogService));
                AttachDiagnostics(_dialogService);
                AttachInteractionBlocker(_dialogService);
            }

            if (_promptService == null && services.CanResolve(typeof(IPromptService)))
            {
                _promptService = (IPromptService)services.Get(typeof(IPromptService));
                AttachDiagnostics(_promptService);
                AttachInteractionBlocker(_promptService);
            }

                if (_popoverService == null && services.CanResolve(typeof(IPopoverService)))
                {
                    _popoverService = (IPopoverService)services.Get(typeof(IPopoverService));
                    AttachDiagnostics(_popoverService);
                    AttachInteractionBlocker(_popoverService);
                }
            }
        }

        private void AttachDiagnostics(object service)
        {
            if (service is INavigationDiagnosticsAware aware)
                aware.AttachDiagnostics(_diagnostics);
        }

        private void AttachInteractionBlocker(object service)
        {
            if (_pageAwareInteractionBlocker != null &&
                service is INavigationInteractionBlockerAware aware)
            {
                aware.AttachInteractionBlocker(
                    _pageAwareInteractionBlocker);
            }
        }

        private void AttachPageToHost(IPageView page)
        {
            _ctx.Host.Attach(page);

            var blocker = _pageAwareInteractionBlocker;
            if (blocker == null)
                return;

            try
            {
                blocker.OnViewAdded(
                    page.NativeView,
                    isModalSurface: false);
            }
            catch
            {
                // OnViewAdded may have partially captured/disabled the view.
                // Balance both host and blocker before preserving that failure.
                try { _ctx.Host.Detach(page); }
                catch (Exception cleanupError)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[NavigationRuntime] Page attach rollback failed: " +
                        cleanupError);
                }

                try { blocker.OnViewRemoved(page.NativeView); }
                catch (Exception cleanupError)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[NavigationRuntime] Blocker attach rollback failed: " +
                        cleanupError);
                }

                throw;
            }
        }

        private Exception? DetachPageFromHost(
            IPageView page,
            out bool hostDetached)
        {
            hostDetached = false;

            try
            {
                _ctx.Host.Detach(page);
                hostDetached = true;
            }
            catch (Exception ex)
            {
                return ex;
            }

            try
            {
                _pageAwareInteractionBlocker?.OnViewRemoved(
                    page.NativeView);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        private void RunOnUi(
            Action action,
            Action? admissionCompleted = null)
        {
            EnsureDispatcher();
            _dispatcher.BeginInvoke(() =>
            {
                try
                {
                    action();
                }
                finally
                {
                    admissionCompleted?.Invoke();
                }
            });
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

        private Task<T> RunOnUiAsync<T>(
            Func<Task<T>> action,
            Action? admissionCompleted = null)
        {
            EnsureDispatcher();

            var tcs = new TaskCompletionSource<T>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            _dispatcher.BeginInvoke(async () =>
            {
                try
                {
                    Task<T> pending;
                    try
                    {
                        pending = action();
                    }
                    finally
                    {
                        // Surface admission ends once its UI callback has
                        // registered the operation with the owning service. Do
                        // not hold the facade lease until the user completes a
                        // modal task, or Shutdown would deadlock waiting for a
                        // result that runtime teardown itself must provide.
                        admissionCompleted?.Invoke();
                    }

                    var result = await pending;
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

        private async Task<T> ExecuteRequestAsync<T>(
            NavigationTraceScope? trace,
            Func<Task<T>> action)
        {
            trace?.SetStage(NavigationTraceStage.Dispatch);

            return await RunOnUiAsync(async () =>
            {
                var queueDepth = Interlocked.Increment(ref _queuedRequestCount);
                trace?.SetStage(NavigationTraceStage.GateWait, queueDepth);
                EmitRuntimeState("RequestQueued", queueDepth: queueDepth);

                var acquired = false;
                try
                {
                    await _navGate.WaitAsync();
                    acquired = true;
                }
                finally
                {
                    var remaining = Interlocked.Decrement(ref _queuedRequestCount);
                    EmitRuntimeState("RequestDequeued", queueDepth: remaining);
                }

                trace?.SetStage(
                    NavigationTraceStage.Processing,
                    Volatile.Read(ref _queuedRequestCount));

                try
                {
                    return await action();
                }
                finally
                {
                    if (acquired)
                        _navGate.Release();
                }
            });
        }

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
            => StartNavigateRequest(
                pageType,
                args ?? NavigationArgs.Empty,
                NavigationTraceTrigger.Navigate);

        internal Task<bool> GoBackAsync()
        {
            var args = NavigationArgs.Back();
            var trace = _diagnostics.StartRequest(
                RuntimeId,
                Current,
                null,
                "<history>",
                args,
                NavigationTraceTrigger.Back);

            return ExecuteBackRequestAsync(trace);
        }

        private Task StartNavigateRequest(
            Type? pageType,
            NavigationArgs args,
            NavigationTraceTrigger trigger)
        {
            var requestedTarget = pageType?.FullName ?? "<null>";
            var trace = _diagnostics.StartRequest(
                RuntimeId,
                Current,
                pageType,
                requestedTarget,
                args,
                trigger);

            return ExecuteNavigateRequestAsync(trace, pageType, args);
        }

        private async Task ExecuteNavigateRequestAsync(
            NavigationTraceScope? trace,
            Type? pageType,
            NavigationArgs args)
        {
            try
            {
                var result = await ExecuteRequestAsync(trace, async () =>
                {
                    EnsureRuntimeServices();
                    return await SwitchInternalAsync(
                        pageType,
                        args,
                        trace,
                        redirectDepth: 0,
                        visited: null,
                        parentAttemptId: null);
                });

                trace?.Complete(
                    result.RequestOutcome,
                    result.Decision,
                    targetPage: result.TerminalTarget);
            }
            catch (Exception ex)
            {
                trace?.Complete(
                    NavigationTraceOutcome.Failed,
                    errorType: ex.GetType().FullName,
                    targetPage: pageType?.FullName);
                throw;
            }
        }

        private async Task<bool> ExecuteBackRequestAsync(NavigationTraceScope? trace)
        {
            try
            {
                var result = await ExecuteRequestAsync(trace, async () =>
                {
                    EnsureRuntimeServices();
                    return await GoBackInternalAsync(trace);
                });

                trace?.Complete(
                    result.RequestOutcome,
                    result.Decision,
                    targetPage: result.TerminalTarget);
                return result.TargetNavigated;
            }
            catch (Exception ex)
            {
                trace?.Complete(
                    NavigationTraceOutcome.Failed,
                    errorType: ex.GetType().FullName);
                throw;
            }
        }
        // ------------------------------------------------------------
        // ISP-segregated surface: Toast / Dialog / Prompt
        // ------------------------------------------------------------

        internal void ShowToast<TToast>(
            object payload = null,
            int durationMs = 3000,
            Action? admissionCompleted = null)
            where TToast : class, IToastView
        {
            EnsureRuntimeServices();

            if (_toastService == null)
                throw new InvalidOperationException("IToastService is not registered.");

            RunOnUi(
                () => _toastService.ShowToast<TToast>(payload, durationMs),
                admissionCompleted);
        }

        internal void DismissCurrentToast(
            Action? admissionCompleted = null)
        {
            EnsureRuntimeServices();

            if (_toastService != null)
            {
                RunOnUi(
                    _toastService.DismissCurrentToast,
                    admissionCompleted);
            }
            else
            {
                admissionCompleted?.Invoke();
            }
        }

        internal Task<bool> ShowDialogAsync<TDialog>(
            object payload = null,
            Action? admissionCompleted = null)
            where TDialog : class, IDialogView
        {
            EnsureRuntimeServices();

            if (_dialogService == null)
                throw new InvalidOperationException("IDialogService is not registered.");

            // Marshal to the UI thread but do NOT take the nav gate: a modal dialog
            // awaits user input, and holding _navGate would block all navigation.
            return RunOnUiAsync(
                () => _dialogService.ShowDialogAsync<TDialog>(payload),
                admissionCompleted);
        }

        internal Task<TResult> ShowPromptAsync<TPrompt, TResult>(
            object payload = null,
            Action? admissionCompleted = null)
            where TPrompt : class, IPromptView<TResult>
        {
            EnsureRuntimeServices();

            if (_promptService == null)
                throw new InvalidOperationException("IPromptService is not registered.");

            return RunOnUiAsync(
                () => _promptService.ShowPromptAsync<TPrompt, TResult>(payload),
                admissionCompleted);
        }

        internal Task<bool> ShowPopoverAsync<TPopover>(
            object payload = null,
            Action? admissionCompleted = null)
            where TPopover : class, IPopoverView
        {
            EnsureRuntimeServices();

            if (_popoverService == null)
                throw new InvalidOperationException("IPopoverService is not registered.");

            return RunOnUiAsync(
                () => _popoverService.ShowPopoverAsync<TPopover>(payload),
                admissionCompleted);
        }

        public Task ResetAsync()
        {
            return ExecuteAsync(async () =>
            {
                EnsureRuntimeServices();
                var watch = _diagnostics.TraceEventsEnabled
                    ? System.Diagnostics.Stopwatch.StartNew()
                    : null;
                EmitRuntimeState(
                    "ResetStarted",
                    stage: NavigationTraceStage.Reset);

                try
                {
                    var hadAttached =
                        _attachedPages.Count != 0 ||
                        Current != null;
                    var hadVisible =
                        _visiblePages.Count != 0 ||
                        Current != null;

                    await CancelBackgroundLoadsAsync(
                        NavigationTraceCloseReasons.Reset,
                        renewGeneration: true);

                    // Tear down any live toast/dialog/prompt surfaces first so their
                    // awaiters complete and the interaction blocker is released.
                    var overlayError = TeardownOverlayServices(
                        NavigationTraceCloseReasons.Reset);

                    var teardownError = await DetachAndDisposeTrackedPagesAsync("Reset");
                    Current = null;
                    RaiseSafely(CurrentChanged, Current!, nameof(CurrentChanged));
                    var cacheError = DisposeCachedPages("Reset");

                    RaiseBlankStateIfNeeded(hadAttached, hadVisible);

                    _ctx.History.Clear();
                    RaiseSafely(HistoryChanged, nameof(HistoryChanged));

                    var teardownFailure =
                        overlayError ??
                        teardownError ??
                        cacheError;
                    if (teardownFailure != null)
                        throw teardownFailure;

                    EmitRuntimeState(
                        "ResetCompleted",
                        stage: NavigationTraceStage.Reset,
                        success: true,
                        elapsedMilliseconds: watch?.ElapsedMilliseconds ?? 0);

                }
                catch (Exception ex)
                {
                    EmitRuntimeState(
                        "ResetFailed",
                        stage: NavigationTraceStage.Reset,
                        success: false,
                        errorType: ex.GetType().FullName,
                        elapsedMilliseconds: watch?.ElapsedMilliseconds ?? 0);
                    throw;
                }
            });
        }

        public ValueTask DisposeAsync()
        {
            return new ValueTask(ExecuteSafeOnUiAsync(async () =>
            {
                EnsureRuntimeServices();
                var watch = _diagnostics.TraceEventsEnabled
                    ? System.Diagnostics.Stopwatch.StartNew()
                    : null;
                EmitRuntimeState(
                    "DisposeStarted",
                    stage: NavigationTraceStage.Dispose);

                try
                {
                    await CancelBackgroundLoadsAsync(
                        NavigationTraceCloseReasons.RuntimeTeardown,
                        renewGeneration: false);

                    var overlayError = TeardownOverlayServices(
                        NavigationTraceCloseReasons.RuntimeTeardown);

                    var teardownError = await DetachAndDisposeTrackedPagesAsync("Dispose");
                    Current = null;
                    var cacheError = DisposeCachedPages("Dispose");

                    if (_interactionObserver != null)
                        _interactionObserver.InteractionDetected -= OnInteractionDetected;

                    var teardownFailure =
                        overlayError ??
                        teardownError ??
                        cacheError;
                    if (teardownFailure != null)
                        throw teardownFailure;

                    EmitRuntimeState(
                        "DisposeCompleted",
                        stage: NavigationTraceStage.Dispose,
                        success: true,
                        elapsedMilliseconds: watch?.ElapsedMilliseconds ?? 0);

                }
                catch (Exception ex)
                {
                    EmitRuntimeState(
                        "DisposeFailed",
                        stage: NavigationTraceStage.Dispose,
                        success: false,
                        errorType: ex.GetType().FullName,
                        elapsedMilliseconds: watch?.ElapsedMilliseconds ?? 0);
                    throw;
                }
            }));
        }

        private Exception? TeardownOverlayServices(string closeReason)
        {
            Exception? firstError = null;

            void TryClose(Action action)
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    firstError ??= ex;
                    System.Diagnostics.Debug.WriteLine(
                        "[NavigationRuntime] Surface teardown failed: " + ex);
                }
            }

            if (_toastService != null)
                TryClose(() => TeardownService(
                    _toastService,
                    _toastService.DismissCurrentToast,
                    closeReason));
            if (_dialogService != null)
                TryClose(() => TeardownService(
                    _dialogService,
                    _dialogService.CloseAll,
                    closeReason));
            if (_promptService != null)
                TryClose(() => TeardownService(
                    _promptService,
                    _promptService.CloseAll,
                    closeReason));
            if (_popoverService != null)
                TryClose(() => TeardownService(
                    _popoverService,
                    _popoverService.CloseAll,
                    closeReason));

            return firstError;
        }

        private static void TeardownService(
            object service,
            Action fallback,
            string closeReason)
        {
            if (service is INavigationRuntimeTeardownAware aware)
                aware.TeardownForRuntime(closeReason);
            else
                fallback();
        }

        // ---------------------------------------------------------------------
        // BACK (internal)
        // ---------------------------------------------------------------------

        private async Task<NavigationAttemptResult> GoBackInternalAsync(
            NavigationTraceScope? trace)
        {
            trace?.SetStage(NavigationTraceStage.HistoryLookup);

            // Inspect first and commit the stack mutation only after the requested
            // navigation succeeds. A denied guard or pre-show failure therefore
            // leaves both stacks exactly as they were.
            var entry = _ctx.History.HistoryBack.FirstOrDefault();
            if (entry == null)
            {
                EmitRuntimeState("BackNoHistory");
                return NavigationAttemptResult.NoHistory();
            }

            PageHistoryEntry forwardEntry = null;

            if (Current != null)
            {
                _ctx.Registry.TryGetDescriptor(Current.GetType(), out var currentDescriptor);
                forwardEntry = new PageHistoryEntry(
                    Current.GetType(),
                    currentDescriptor?.Name ?? Current.Name,
                    (Current as IPageStateful)?.CaptureState()
                );
            }

            var result = await SwitchInternalAsync(
                entry.PageType,
                NavigationArgs.Back(entry.State),
                trace,
                redirectDepth: 0,
                visited: null,
                parentAttemptId: null);

            if (!result.TargetNavigated)
                return result;

            // Lifecycle/event callbacks can mutate the public history object while
            // the navigation gate is held. Commit only the entry that was inspected;
            // never pop a newer entry or manufacture a forward entry for a history
            // transition that another callback superseded.
            if (!_ctx.History.TryPopExpectedBack(entry, out _))
            {
                RaiseSafely(HistoryChanged, nameof(HistoryChanged));
                EmitRuntimeState(
                    "BackHistoryChangedDuringNavigation",
                    success: false);
                return result;
            }

            if (forwardEntry != null)
                _ctx.History.PushForward(forwardEntry);

            // SwitchInternalAsync intentionally skips its Record/HistoryChanged on
            // back-navigation (see fix there); the back-path manages history itself,
            // so fire the notification once from here.
            RaiseSafely(HistoryChanged, nameof(HistoryChanged));
            EmitRuntimeState("HistoryChanged");

            return result;
        }

        // ---------------------------------------------------------------------
        // CORE NAVIGATION (ASSUMES already UI-thread + serialized)
        // ---------------------------------------------------------------------

        private async Task<NavigationAttemptResult> SwitchInternalAsync(
            Type? pageType,
            NavigationArgs navArgs,
            NavigationTraceScope? requestTrace,
            int redirectDepth,
            HashSet<Type>? visited,
            string? parentAttemptId)
        {
            if (pageType == null)
                throw new ArgumentNullException(nameof(pageType));

            IPageView from = Current;
            IPageView to = null;
            PageDescriptor toDesc = null;
            PageDescriptor fromDesc = null;
            bool targetAttached = false;
            bool targetWasAlreadyAttached = false;
            bool targetWasVisible = false;
            bool targetShowAttempted = false;
            bool currentChangedToTarget = false;
            BackgroundLoadRegistration? backgroundLoad = null;
            bool previousTransitionStarted = false;
            bool previousDetached = false;
            bool previousCleanupPending = false;
            var previousWasAttached =
                from != null &&
                (_attachedPages.Contains(from) || ReferenceEquals(Current, from));
            var previousWasVisible =
                from != null &&
                (_visiblePages.Contains(from) || ReferenceEquals(Current, from));
            bool attemptTerminal = false;
            var hadAttachedAtStart = _attachedPages.Count != 0;
            var hadVisibleAtStart = _visiblePages.Count != 0;
            var effectiveArgs = navArgs;
            var failureStage = NavigationFailureKind.PageNotRegistered;
            var requestedTarget = pageType.FullName ?? pageType.Name;
            var attemptTrace = requestTrace?.StartAttempt(
                from?.GetType().FullName ?? from?.GetType().Name,
                requestedTarget,
                redirectDepth,
                parentAttemptId);

            if (from != null)
            {
                _ctx.Registry.TryGetDescriptor(from.GetType(), out fromDesc);
                attemptTrace?.SetFromPageName(fromDesc?.Name ?? from.Name);
            }

            try
            {
                attemptTrace?.SetStage(NavigationTraceStage.RegistryLookup);

                if (!_ctx.Registry.TryGetDescriptor(pageType, out toDesc))
                    throw new InvalidOperationException(
                        $"Type '{pageType.FullName}' is not a registered page.");

                attemptTrace?.SetDescriptor(toDesc);
                failureStage = NavigationFailureKind.PageCreationFailed;

                var canonicalPageType = toDesc.PageType;
                effectiveArgs = navArgs.WithLoadMode(toDesc.LoadMode);

                // A registered target is now being processed. This remains before
                // guard evaluation and every subscriber is isolated.
                RaiseNavigating(Current, canonicalPageType, effectiveArgs);

                attemptTrace?.SetStage(NavigationTraceStage.CycleCheck);
                visited ??= new HashSet<Type>();

                if (!visited.Add(canonicalPageType))
                {
                    _diagnostics.EmitGuardDenied(
                        from,
                        canonicalPageType,
                        null,
                        "Guard redirect cycle detected.",
                        attemptTrace);
                    attemptTrace?.Complete(
                        NavigationTraceOutcome.Denied,
                        decision: "RedirectCycle");
                    attemptTerminal = true;
                    return NavigationAttemptResult.Denied(
                        canonicalPageType,
                        toDesc.Name);
                }

                if (redirectDepth > 8)
                {
                    _diagnostics.EmitGuardDenied(
                        from,
                        canonicalPageType,
                        null,
                        "Max guard redirect depth exceeded.",
                        attemptTrace);
                    attemptTrace?.Complete(
                        NavigationTraceOutcome.Denied,
                        decision: "RedirectDepthExceeded");
                    attemptTerminal = true;
                    return NavigationAttemptResult.Denied(
                        canonicalPageType,
                        toDesc.Name);
                }

                // AllowAnonymous is descriptor policy, so it bypasses the composed
                // authentication/authorization guard rather than evaluating it.
                var guard = toDesc.Guard;
                if (guard != null && toDesc.AllowAnonymous)
                {
                    EmitPageDecision(
                        attemptTrace,
                        canonicalPageType.FullName ?? canonicalPageType.Name,
                        "GuardBypassedAllowAnonymous");
                }
                else if (guard != null)
                {
                    attemptTrace?.SetStage(NavigationTraceStage.GuardEvaluation);
                    var guardContext = new GuardContext(
                        canonicalPageType,
                        _ctx.User,
                        effectiveArgs.Timing);
                    GuardResult guardResult;

                    try
                    {
                        var evaluation = guard.EvaluateAsync(guardContext);
                        var finished = await Task.WhenAny(
                            evaluation,
                            Task.Delay(GuardEvaluationTimeoutMs));

                        if (!ReferenceEquals(finished, evaluation))
                        {
                            _diagnostics.EmitGuardDenied(
                                from,
                                canonicalPageType,
                                null,
                                "Guard evaluation timed out.",
                                attemptTrace);
                            attemptTrace?.Complete(
                                NavigationTraceOutcome.Denied,
                                decision: "GuardTimeout");
                            attemptTerminal = true;
                            return NavigationAttemptResult.Denied(
                                canonicalPageType,
                                toDesc.Name);
                        }

                        guardResult = await evaluation;
                    }
                    catch (Exception ex)
                    {
                        _diagnostics.EmitGuardDenied(
                            from,
                            canonicalPageType,
                            null,
                            $"Guard exception: {ex.Message}",
                            attemptTrace);
                        attemptTrace?.Complete(
                            NavigationTraceOutcome.Denied,
                            decision: "GuardException",
                            errorType: ex.GetType().FullName);
                        attemptTerminal = true;
                        return NavigationAttemptResult.Denied(
                            canonicalPageType,
                            toDesc.Name);
                    }

                    if (!guardResult.Allowed)
                    {
                        _diagnostics.EmitGuardDenied(
                            from,
                            canonicalPageType,
                            guardResult.RedirectPage,
                            guardResult.Reason,
                            attemptTrace);

                        if (guardResult.RedirectPage == null)
                        {
                            attemptTrace?.Complete(
                                NavigationTraceOutcome.Denied,
                                decision: "GuardDenied");
                            attemptTerminal = true;
                            return NavigationAttemptResult.Denied(
                                canonicalPageType,
                                toDesc.Name);
                        }

                        attemptTrace?.Complete(
                            NavigationTraceOutcome.Redirected,
                            decision: "GuardRedirect");
                        attemptTerminal = true;

                        var redirected = await SwitchInternalAsync(
                            guardResult.RedirectPage,
                            navArgs,
                            requestTrace,
                            redirectDepth + 1,
                            visited,
                            attemptTrace?.AttemptId);

                        return NavigationAttemptResult.Redirected(redirected);
                    }
                }

                if (!typeof(IPageView).IsAssignableFrom(canonicalPageType))
                    throw new InvalidOperationException(
                        $"Navigation target '{canonicalPageType.FullName}' is not a page.");

                object fromState = null;
                if (from != null && !effectiveArgs.IsBackNavigation)
                {
                    attemptTrace?.SetStage(NavigationTraceStage.StateCapture);
                    fromState = (from as IPageStateful)?.CaptureState();
                }

                attemptTrace?.SetStage(NavigationTraceStage.PageResolution);
                to = ResolvePage(toDesc, attemptTrace);

                if (toDesc.LoadMode == NavigationLoadMode.LoadBeforeShow)
                {
                    attemptTrace?.SetStage(NavigationTraceStage.LoadBeforeShow);
                    failureStage = NavigationFailureKind.LoadFailed;
                    await LoadAsync(to, effectiveArgs.Payload, attemptTrace);
                }

                failureStage = NavigationFailureKind.LifecycleFailed;
                attemptTrace?.SetStage(NavigationTraceStage.LeavePage);
                previousTransitionStarted = from != null;

                if (from is IPageVisibility fromVisibility)
                {
                    fromVisibility.HidePage();
                    EmitPageDecision(attemptTrace, from, "Hidden");
                }

                if (from is IPageLifecycle leave)
                    await leave.OnNavigatedFromAsync();

                if (from != null)
                {
                    bool keepAttached =
                        fromDesc != null &&
                        fromDesc.KeepAttachedWhenHidden &&
                        fromDesc.ReusePolicy != PageReusePolicy.Transient &&
                        from is IPageVisibility &&
                        !from.IsDisposed;

                    if (!keepAttached)
                    {
                        attemptTrace?.SetStage(NavigationTraceStage.DetachPage);
                        var detachError = DetachPageFromHost(
                            from,
                            out var hostDetached);

                        if (hostDetached)
                        {
                            previousDetached = true;
                            _visiblePages.Remove(from);
                            _attachedPages.Remove(from);
                            EmitPageDecision(
                                attemptTrace,
                                from,
                                "Detached");
                            previousCleanupPending = true;
                        }

                        SurfaceCleanup.Rethrow(detachError);
                    }
                    else
                    {
                        _visiblePages.Remove(from);
                        EmitPageDecision(attemptTrace, from, "KeptAttachedHidden");
                    }
                }

                attemptTrace?.SetStage(NavigationTraceStage.AttachPage);
                targetWasAlreadyAttached = _attachedPages.Contains(to);
                targetWasVisible = _visiblePages.Contains(to);
                if (!targetWasAlreadyAttached)
                {
                    AttachPageToHost(to);
                    _attachedPages.Add(to);
                }

                targetAttached = true;
                _ctx.Host.BringToFront(to);

                if (!targetWasAlreadyAttached && !_firstPageAttachedRaised)
                {
                    _firstPageAttachedRaised = true;
                    RaiseSafely(OnFirstPageAttached, to, nameof(OnFirstPageAttached));
                }

                EmitPageDecision(
                    attemptTrace,
                    to,
                    targetWasAlreadyAttached ? "BroughtAttachedPageToFront" : "Attached");

                if (to is IPageVisibility toVisibility)
                {
                    targetShowAttempted = true;
                    toVisibility.ShowPage();
                }

                _visiblePages.Add(to);
                EmitPageDecision(attemptTrace, to, "Visible");

                Current = to;
                currentChangedToTarget = true;
                RaiseSafely(CurrentChanged, Current, nameof(CurrentChanged));
                EmitPageDecision(attemptTrace, to, "CurrentChanged");

                if (toDesc.LoadMode == NavigationLoadMode.ShowImmediately)
                {
                    attemptTrace?.SetStage(NavigationTraceStage.LoadAfterShow);
                    failureStage = NavigationFailureKind.LoadFailed;
                    await LoadAsync(to, effectiveArgs.Payload, attemptTrace);
                    failureStage = NavigationFailureKind.LifecycleFailed;
                }
                else if (toDesc.LoadMode == NavigationLoadMode.LoadInBackground)
                {
                    backgroundLoad = StartBackgroundLoad(
                        to!,
                        effectiveArgs.Payload,
                        requestTrace,
                        attemptTrace);
                }

                if (effectiveArgs.IsBackNavigation && to is IPageStateful stateful)
                {
                    attemptTrace?.SetStage(NavigationTraceStage.StateRestore);
                    stateful.RestoreState(effectiveArgs.Payload);
                }

                if (to is IPageLifecycle enter)
                {
                    attemptTrace?.SetStage(NavigationTraceStage.EnterPage);
                    await enter.OnNavigatedToAsync(effectiveArgs);
                }

                // The previous transient remains alive until the replacement has
                // completed every fallible load/lifecycle step. This makes a failed
                // switch recoverable instead of leaving Current detached or blank.
                if (previousCleanupPending &&
                    from != null &&
                    !ReferenceEquals(from, to))
                {
                    var cleanupError = Cleanup(
                        from,
                        fromDesc,
                        forceDispose: false,
                        attemptTrace,
                        "NavigationCleanup");

                    if (cleanupError != null)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            "[NavigationRuntime] Previous-page cleanup failed " +
                            "after navigation commit: " + cleanupError);
                    }
                }

                if (from != null && !effectiveArgs.IsBackNavigation)
                {
                    attemptTrace?.SetStage(NavigationTraceStage.HistoryUpdate);
                    _ctx.History.Record(new PageHistoryEntry(
                        from.GetType(),
                        fromDesc?.Name ?? from.Name,
                        fromState));

                    RaiseSafely(HistoryChanged, nameof(HistoryChanged));
                    EmitRuntimeState("HistoryChanged");
                }

                RaiseSafely(Navigated, from!, to, effectiveArgs, nameof(Navigated));
                _diagnostics.EmitSuccess(
                    from,
                    to,
                    effectiveArgs,
                    toDesc,
                    fromDesc,
                    attemptTrace);
                attemptTrace?.Complete(NavigationTraceOutcome.Succeeded);
                attemptTerminal = true;
                EmitRuntimeState("NavigationCompleted");
                return NavigationAttemptResult.Succeeded(
                    canonicalPageType,
                    toDesc.Name);
            }
            catch (Exception ex)
            {
                if (previousTransitionStarted ||
                    targetAttached ||
                    targetShowAttempted ||
                    currentChangedToTarget ||
                    backgroundLoad != null)
                {
                    await RollbackFailedSwitchAsync(
                        from,
                        previousWasAttached,
                        previousWasVisible,
                        previousDetached,
                        to,
                        toDesc,
                        targetWasAlreadyAttached,
                        targetWasVisible,
                        targetAttached,
                        targetShowAttempted,
                        currentChangedToTarget,
                        backgroundLoad,
                        attemptTrace);
                }
                else if (to != null &&
                    toDesc != null &&
                    toDesc.ReusePolicy == PageReusePolicy.Transient &&
                    !ReferenceEquals(from, to))
                {
                    DisposePage(to, attemptTrace, "UnattachedTransientFailure");
                }

                if (!attemptTerminal)
                {
                    RaiseSafely(
                        NavigationFailed,
                        from!,
                        pageType,
                        ex,
                        nameof(NavigationFailed));
                    _diagnostics.EmitFailure(
                        from,
                        to,
                        effectiveArgs,
                        failureStage,
                        ex.Message,
                        toDesc,
                        pageType,
                        fromDesc,
                        attemptTrace);
                    attemptTrace?.Complete(
                        NavigationTraceOutcome.Failed,
                        failureKind: failureStage.ToString(),
                        errorType: ex.GetType().FullName);
                    RaiseBlankStateIfNeeded(
                        hadAttachedAtStart,
                        hadVisibleAtStart);
                }

                throw;
            }
        }

        private async Task RollbackFailedSwitchAsync(
            IPageView? previous,
            bool previousWasAttached,
            bool previousWasVisible,
            bool previousDetached,
            IPageView? target,
            PageDescriptor? targetDescriptor,
            bool targetWasAlreadyAttached,
            bool targetWasVisible,
            bool targetAttached,
            bool targetShowAttempted,
            bool currentChangedToTarget,
            BackgroundLoadRegistration? backgroundLoad,
            NavigationAttemptTraceScope? attemptTrace)
        {
            if (backgroundLoad != null)
            {
                try
                {
                    backgroundLoad.Cancellation.TrySetResult(
                        NavigationTraceCloseReasons.NavigationRollback);
                    await backgroundLoad.Wrapper;
                }
                catch (Exception cleanupError)
                {
                    WriteRollbackFailure(
                        "background-load cancellation",
                        cleanupError);
                }
            }

            if (target != null)
            {
                if ((targetShowAttempted || currentChangedToTarget) &&
                    target is IPageVisibility targetVisibility)
                {
                    try
                    {
                        targetVisibility.HidePage();
                        EmitPageDecision(
                            attemptTrace,
                            target,
                            "RollbackHidden");
                    }
                    catch (Exception cleanupError)
                    {
                        WriteRollbackFailure(
                            "target hide",
                            cleanupError);
                    }
                }

                if (!targetWasVisible ||
                    ReferenceEquals(target, previous))
                {
                    _visiblePages.Remove(target);
                }

                if (targetAttached && !targetWasAlreadyAttached)
                {
                    var detachError = DetachPageFromHost(
                        target,
                        out var hostDetached);

                    if (hostDetached)
                    {
                        _attachedPages.Remove(target);
                        _visiblePages.Remove(target);
                        EmitPageDecision(
                            attemptTrace,
                            target,
                            "RollbackDetached");
                    }

                    if (detachError != null)
                    {
                        WriteRollbackFailure(
                            "target detach",
                            detachError);
                    }
                }

                if (targetDescriptor != null &&
                    targetDescriptor.ReusePolicy == PageReusePolicy.Transient &&
                    !ReferenceEquals(target, previous))
                {
                    if (!_attachedPages.Contains(target))
                    {
                        var disposeError = DisposePage(
                            target,
                            attemptTrace,
                            "RollbackDisposed");
                        if (disposeError != null)
                        {
                            WriteRollbackFailure(
                                "target dispose",
                                disposeError);
                        }
                    }
                    else
                    {
                        EmitPageDecision(
                            attemptTrace,
                            target,
                            "RollbackDisposeSkippedAttached");
                    }
                }
            }

            var restoredPrevious = false;
            IPageView? previousPage = null;
            if (previous != null && !previous.IsDisposed)
            {
                restoredPrevious = true;
                previousPage = previous;
            }

            if (restoredPrevious &&
                previousPage != null &&
                previousWasAttached)
            {
                if (previousDetached ||
                    !_attachedPages.Contains(previousPage))
                {
                    try
                    {
                        AttachPageToHost(previousPage);
                        _attachedPages.Add(previousPage);
                        EmitPageDecision(
                            attemptTrace,
                            previousPage,
                            "RollbackAttached");
                    }
                    catch (Exception cleanupError)
                    {
                        restoredPrevious = false;
                        WriteRollbackFailure(
                            "previous-page attach",
                            cleanupError);
                    }
                }

                if (restoredPrevious)
                {
                    try
                    {
                        _ctx.Host.BringToFront(previousPage);
                        EmitPageDecision(
                            attemptTrace,
                            previousPage,
                            "RollbackBroughtToFront");
                    }
                    catch (Exception cleanupError)
                    {
                        restoredPrevious = false;
                        EmitPageDecision(
                            attemptTrace,
                            previousPage,
                            "RollbackBringToFrontFailed");
                        WriteRollbackFailure(
                            "previous-page bring-to-front",
                            cleanupError);
                    }
                }
            }

            if (restoredPrevious &&
                previousPage != null &&
                previousWasVisible)
            {
                if (previousPage is IPageVisibility previousVisibility)
                {
                    try
                    {
                        previousVisibility.ShowPage();
                        EmitPageDecision(
                            attemptTrace,
                            previousPage,
                            "RollbackVisible");
                    }
                    catch (Exception cleanupError)
                    {
                        restoredPrevious = false;
                        _visiblePages.Remove(previousPage);
                        EmitPageDecision(
                            attemptTrace,
                            previousPage,
                            "RollbackShowFailed");
                        WriteRollbackFailure(
                            "previous-page show",
                            cleanupError);
                    }
                }

                if (restoredPrevious)
                    _visiblePages.Add(previousPage);
            }

            var restoredCurrent = restoredPrevious
                ? previousPage
                : null;
            var currentChanged =
                !ReferenceEquals(Current, restoredCurrent);
            Current = restoredCurrent!;

            if (currentChangedToTarget || currentChanged)
            {
                RaiseSafely(CurrentChanged, Current!, nameof(CurrentChanged));
                if (Current != null)
                {
                    EmitPageDecision(
                        attemptTrace,
                        Current,
                        "RollbackCurrentChanged");
                }
                else
                {
                    EmitRuntimeState(
                        "RollbackCurrentCleared",
                        success: false);
                }
            }
        }

        private static void WriteRollbackFailure(
            string step,
            Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                "[NavigationRuntime] Navigation rollback " +
                step +
                " failed: " +
                exception);
        }

        private void RaiseNavigating(
            IPageView from,
            Type target,
            NavigationArgs args)
        {
            RaiseSafely(Navigating, from, target, args, nameof(Navigating));
        }

        private static TaskCompletionSource<string>
            CreateBackgroundLoadCancellation()
            => new TaskCompletionSource<string>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        private void TrackBackgroundLoadWrapper(Task wrapper)
        {
            lock (_backgroundLoadSync)
                _backgroundLoadWrappers.Add(wrapper);

            _ = RemoveBackgroundLoadWrapperWhenCompleteAsync(wrapper);
        }

        private async Task RemoveBackgroundLoadWrapperWhenCompleteAsync(
            Task wrapper)
        {
            try
            {
                await wrapper.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // LoadInBackgroundSafeAsync is terminal-safe and should not fault.
                // Keep this observer defensive so no wrapper exception is unobserved.
                System.Diagnostics.Debug.WriteLine(
                    "[NavigationRuntime] Background wrapper failed: " + ex);
            }
            finally
            {
                lock (_backgroundLoadSync)
                    _backgroundLoadWrappers.Remove(wrapper);
            }
        }

        private async Task CancelBackgroundLoadsAsync(
            string reason,
            bool renewGeneration)
        {
            Task[] wrappers;

            lock (_backgroundLoadSync)
            {
                _backgroundLoadCancellation.TrySetResult(reason);
                wrappers = _backgroundLoadWrappers.ToArray();
                _backgroundLoadsEnded = !renewGeneration;

                if (renewGeneration)
                {
                    _backgroundLoadCancellation =
                        CreateBackgroundLoadCancellation();
                }
            }

            if (wrappers.Length == 0)
                return;

            try
            {
                // These are runtime wrappers which stop waiting as soon as their
                // generation is canceled. The arbitrary page-owned work is not
                // included in this set and is observed separately if it finishes late.
                await Task.WhenAll(wrappers);
            }
            catch (Exception ex)
            {
                // Every wrapper owns its terminal and catches user-code failures.
                // A defensive catch keeps Reset/Dispose cleanup progressing.
                System.Diagnostics.Debug.WriteLine(
                    "[NavigationRuntime] Background drain failed: " + ex);
            }
        }

        private BackgroundLoadRegistration StartBackgroundLoad(
            IPageView page,
            object payload,
            NavigationTraceScope? requestTrace,
            NavigationAttemptTraceScope? attemptTrace)
        {
            Task<string> runtimeCancellation;
            lock (_backgroundLoadSync)
            {
                if (_backgroundLoadsEnded)
                    throw new ObjectDisposedException(nameof(NavigationRuntime));

                runtimeCancellation = _backgroundLoadCancellation.Task;
            }

            var registration = new BackgroundLoadRegistration();
            var cancellation = WaitForFirstCancellationAsync(
                runtimeCancellation,
                registration.Cancellation.Task);
            var operationId = requestTrace != null &&
                _diagnostics.TraceEventsEnabled
                    ? NavigationTraceScope.NewId()
                    : null;
            var active = Interlocked.Increment(ref _backgroundLoadCount);
            var pageName = attemptTrace?.TargetPage ??
                page.GetType().FullName ??
                page.GetType().Name;

            if (operationId != null && requestTrace != null && attemptTrace != null)
            {
                requestTrace.EmitBackground(
                    NavigationTraceKind.BackgroundLoadStarted,
                    attemptTrace,
                    operationId,
                    pageName,
                    elapsedMilliseconds: 0,
                    backgroundLoadCount: active);
            }

            EmitRuntimeState("BackgroundLoadStarted");
            var operation = LoadInBackgroundSafeAsync(
                page,
                payload,
                requestTrace,
                attemptTrace,
                operationId,
                pageName,
                cancellation);
            var wrapper = CompleteBackgroundRegistrationAsync(
                operation,
                registration.Cancellation);
            registration.Wrapper = wrapper;
            TrackBackgroundLoadWrapper(wrapper);
            return registration;
        }

        private static async Task CompleteBackgroundRegistrationAsync(
            Task operation,
            TaskCompletionSource<string> cancellation)
        {
            try
            {
                await operation;
            }
            finally
            {
                // Release the unused side of Task.WhenAny after a normal
                // terminal so one runtime generation does not retain completed
                // background registrations until the next reset.
                cancellation.TrySetResult("BackgroundLoadFinished");
            }
        }

        private static async Task<string> WaitForFirstCancellationAsync(
            Task<string> runtimeCancellation,
            Task<string> operationCancellation)
        {
            var completed = await Task.WhenAny(
                runtimeCancellation,
                operationCancellation);
            return await completed;
        }

        // Fire-and-forget wrapper for LoadInBackground. It has an independent
        // terminal trace and never creates a second PageLogEntry for the navigation.
        private async Task LoadInBackgroundSafeAsync(
            IPageView page,
            object payload,
            NavigationTraceScope? requestTrace,
            NavigationAttemptTraceScope? attemptTrace,
            string? operationId,
            string pageName,
            Task<string> cancellation)
        {
            var watch = operationId != null
                ? System.Diagnostics.Stopwatch.StartNew()
                : null;
            var kind = NavigationTraceKind.BackgroundLoadCompleted;
            string? decision = null;
            string? errorType = null;

            try
            {
                var result = await LoadAsync(
                    page,
                    payload,
                    attemptTrace,
                    guardApply: true,
                    cancellation);

                if (result.Applied && cancellation.IsCompleted)
                    result = PageLoadResult.Discarded(await cancellation);

                if (!result.Applied)
                {
                    kind = NavigationTraceKind.BackgroundLoadDiscarded;
                    decision = result.DiscardReason;
                }
            }
            catch (Exception ex)
            {
                kind = NavigationTraceKind.BackgroundLoadFailed;
                errorType = ex.GetType().FullName;
                System.Diagnostics.Debug.WriteLine(
                    $"[NavigationRuntime] Background load failed for '{page.GetType().FullName}': {ex}");
            }
            finally
            {
                var remaining = Interlocked.Decrement(ref _backgroundLoadCount);

                if (operationId != null &&
                    requestTrace != null &&
                    attemptTrace != null)
                {
                    requestTrace.EmitBackground(
                        kind,
                        attemptTrace,
                        operationId,
                        pageName,
                        watch?.ElapsedMilliseconds ?? 0,
                        decision,
                        errorType,
                        remaining);
                }

                EmitRuntimeState(
                    kind.ToString(),
                    success: kind == NavigationTraceKind.BackgroundLoadCompleted
                        ? true
                        : kind == NavigationTraceKind.BackgroundLoadFailed
                            ? false
                            : null,
                    errorType: errorType,
                    elapsedMilliseconds: watch?.ElapsedMilliseconds ?? 0);
            }
        }

        private async Task<PageLoadResult> LoadAsync(
            IPageView page,
            object payload,
            NavigationAttemptTraceScope? attemptTrace,
            bool guardApply = false,
            Task<string>? cancellation = null)
        {
            if (page is IBackgroundLoadable bg)
            {
                // The loading mask is system infrastructure, not a user toast/dialog.
                // Drive it through IViewHost directly to avoid coupling to the user-facing services.
                var maskDesc = _ctx.Registry.AllDescriptors()
                    .FirstOrDefault(d => typeof(IGlobalLoadingMask).IsAssignableFrom(d.PageType));

                var viewHost = _ctx.Host as IViewHost;
                IPageView mask = null;
                Exception? operationError = null;
                var canceled = false;
                var maskTrackedByBlocker = false;
                var pageName = attemptTrace?.TargetPage ??
                    page.GetType().FullName ??
                    page.GetType().Name;

                try
                {
                    if (maskDesc != null && viewHost != null && _pageFactory != null)
                    {
                        mask = _pageFactory.Create(maskDesc.PageType);
                        viewHost.AddView(mask.NativeView);
                        if (_pageAwareInteractionBlocker != null)
                        {
                            maskTrackedByBlocker = true;
                            _pageAwareInteractionBlocker.OnViewAdded(
                                mask.NativeView,
                                isModalSurface: false);
                        }
                        viewHost.BringToFront(mask.NativeView);
                        EmitPageDecision(
                            attemptTrace,
                            mask,
                            "LoadingMaskShown");

                        if (mask is IPageOverlay overlay)
                            await overlay.OnOverlayOpenedAsync("Loading...");
                    }

                    var loadTask = Task.Run(
                        async () => await bg
                            .LoadInBackgroundAsync(payload)
                            .ConfigureAwait(false));
                    var cancelReason = await WaitForUserWorkOrCancellationAsync(
                        loadTask,
                        cancellation,
                        pageName,
                        "load");

                    if (cancelReason != null)
                    {
                        canceled = true;
                        return PageLoadResult.Discarded(cancelReason);
                    }

                    // For background loads the user may have navigated away (or the page may
                    // have been disposed) while we were loading. Only apply the result when
                    // this page is still the live, attached one (A-5).
                    if (guardApply && (page.IsDisposed || !ReferenceEquals(Current, page)))
                    {
                        return PageLoadResult.Discarded(
                            page.IsDisposed
                                ? "PageDisposed"
                                : "PageNoLongerCurrent");
                    }

                    var applyTask = bg.ApplyBackgroundResultAsync();
                    cancelReason = await WaitForUserWorkOrCancellationAsync(
                        applyTask,
                        cancellation,
                        pageName,
                        "apply");

                    if (cancelReason != null)
                    {
                        canceled = true;
                        return PageLoadResult.Discarded(cancelReason);
                    }
                }
                catch (Exception ex)
                {
                    operationError = ex;
                    throw;
                }
                finally
                {
                    if (mask != null && viewHost != null)
                    {
                        Exception? cleanupError = null;

                        try
                        {
                            if (mask is IPageOverlay overlay)
                                await overlay.OnOverlayClosingAsync();
                        }
                        catch (Exception ex)
                        {
                            cleanupError = ex;
                        }

                        try
                        {
                            viewHost.RemoveView(mask.NativeView);
                            EmitPageDecision(
                                attemptTrace,
                                mask,
                                "LoadingMaskRemoved");
                        }
                        catch (Exception ex)
                        {
                            if (cleanupError == null)
                                cleanupError = ex;
                        }

                        if (maskTrackedByBlocker)
                        {
                            maskTrackedByBlocker = false;
                            try
                            {
                                _pageAwareInteractionBlocker?.OnViewRemoved(
                                    mask.NativeView);
                            }
                            catch (Exception ex)
                            {
                                if (cleanupError == null)
                                    cleanupError = ex;
                            }
                        }

                        DisposePage(mask, attemptTrace, "LoadingMaskDisposed");
                        canceled = canceled ||
                            (cancellation?.IsCompleted ?? false);

                        // Preserve the original load/open/apply exception. If the
                        // operation itself succeeded, a close/remove failure remains
                        // observable as the navigation failure it was before this fix.
                        // Cancellation teardown must still resolve as Discarded, so a
                        // mask hook failure is logged without replacing that terminal.
                        if (operationError == null && cleanupError != null)
                        {
                            if (!canceled)
                                throw cleanupError;

                            System.Diagnostics.Debug.WriteLine(
                                "[NavigationRuntime] Loading mask cleanup failed " +
                                "during background cancellation: " + cleanupError);
                        }
                    }
                }
            }

            return PageLoadResult.AppliedResult;
        }

        private static async Task<string?> WaitForUserWorkOrCancellationAsync(
            Task userWork,
            Task<string>? cancellation,
            string pageName,
            string phase)
        {
            if (cancellation == null)
            {
                await userWork;
                return null;
            }

            if (cancellation.IsCompleted)
            {
                _ = ObserveDetachedUserWorkAsync(userWork, pageName, phase);
                return await cancellation;
            }

            var completed = await Task.WhenAny(userWork, cancellation);
            if (cancellation.IsCompleted ||
                !ReferenceEquals(completed, userWork))
            {
                _ = ObserveDetachedUserWorkAsync(userWork, pageName, phase);
                return await cancellation;
            }

            await userWork;
            return null;
        }

        private static async Task ObserveDetachedUserWorkAsync(
            Task userWork,
            string pageName,
            string phase)
        {
            try
            {
                await userWork.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[NavigationRuntime] Detached background " + phase +
                    " failed for '" + pageName + "': " + ex);
            }
        }

        // ---------------------------------------------------------------------
        // PAGE RESOLUTION (reuse policy caches)
        // ---------------------------------------------------------------------

        private IPageView ResolvePage(
            PageDescriptor d,
            NavigationAttemptTraceScope? attemptTrace)
        {
            var factory = _pageFactory;

            switch (d.ReusePolicy)
            {
                case PageReusePolicy.Transient:
                    var transient = factory.Create(d.PageType);
                    EmitPageDecision(attemptTrace, transient, "TransientCreated");
                    return transient;

                case PageReusePolicy.StrongSingleton:
                    if (_strongCache.TryGetValue(d.PageType, out var strong) &&
                        strong != null &&
                        !strong.IsDisposed)
                    {
                        EmitPageDecision(attemptTrace, strong, "StrongCacheHit");
                        return strong;
                    }

                    strong = factory.Create(d.PageType);
                    _strongCache[d.PageType] = strong;
                    EmitPageDecision(attemptTrace, strong, "StrongCacheCreated");
                    return strong;

                case PageReusePolicy.WeakSingleton:
                    if (_weakCache.TryGetValue(d.PageType, out var weak) &&
                        weak.TryGetTarget(out var target) &&
                        target != null &&
                        !target.IsDisposed)
                    {
                        EmitPageDecision(attemptTrace, target, "WeakCacheHit");
                        return target;
                    }

                    // Drop weak entries whose page was collected/disposed so dead
                    // slots don't accumulate over the app's lifetime (L-5).
                    CompactWeakCache();

                    var newPage = factory.Create(d.PageType);
                    _weakCache[d.PageType] = new WeakReference<IPageView>(newPage);
                    EmitPageDecision(attemptTrace, newPage, "WeakCacheCreated");
                    return newPage;

                default:
                    var created = factory.Create(d.PageType);
                    EmitPageDecision(attemptTrace, created, "PageCreated");
                    return created;
            }
        }

        // ---------------------------------------------------------------------
        // LIFECYCLE + CLEANUP
        // ---------------------------------------------------------------------

        // Synchronous on purpose: there is no async teardown work here, so a Task-returning
        // signature would only mislead callers (A-8).
        private Exception? Cleanup(
            IPageView page,
            PageDescriptor? descriptor,
            bool forceDispose,
            NavigationAttemptTraceScope? attemptTrace = null,
            string reason = "Cleanup")
        {
            if (page == null || page.IsDisposed)
                return null;

            if (forceDispose)
            {
                if (descriptor != null)
                    RemoveFromCaches(descriptor.PageType, page);

                return DisposePage(page, attemptTrace, reason);
            }

            if (descriptor != null && descriptor.ReusePolicy == PageReusePolicy.Transient)
                return DisposePage(page, attemptTrace, reason);

            return null;
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

        private Exception? DisposePage(
            IPageView page,
            NavigationAttemptTraceScope? attemptTrace = null,
            string reason = "Disposed")
        {
            if (page == null || page.IsDisposed)
                return null;

            try
            {
                page.Dispose();
                EmitPageDecision(attemptTrace, page, reason, isDisposed: true);
                return null;
            }
            catch (Exception ex)
            {
                EmitPageDecision(
                    attemptTrace,
                    page,
                    reason + "Failed",
                    isDisposed: page.IsDisposed);
                System.Diagnostics.Debug.WriteLine(
                    "[NavigationRuntime] Page dispose failed: " + ex);
                return ex;
            }
        }

        private async Task<Exception?> DetachAndDisposeTrackedPagesAsync(
            string reason)
        {
            Exception? firstError = null;
            var active = Current;

            // Only the active page receives the exit lifecycle. Pages retained
            // hidden already received it when they stopped being Current.
            if (active is IPageVisibility visibility)
            {
                try
                {
                    visibility.HidePage();
                    EmitPageDecision(null, active, reason + "Hidden");
                }
                catch (Exception ex)
                {
                    firstError ??= ex;
                }
            }

            if (active is IPageLifecycle lifecycle)
            {
                try
                {
                    await lifecycle.OnNavigatedFromAsync();
                }
                catch (Exception ex)
                {
                    firstError ??= ex;
                }
            }

            var pages = _attachedPages.ToList();

            // Current should normally be tracked, but include it defensively so a
            // prior host failure cannot strand the live page during teardown.
            if (Current != null && !pages.Contains(Current))
                pages.Add(Current);

            foreach (var page in pages)
            {
                var detachError = DetachPageFromHost(
                    page,
                    out var hostDetached);

                if (hostDetached)
                {
                    _attachedPages.Remove(page);
                    _visiblePages.Remove(page);
                    EmitPageDecision(null, page, reason + "Detached");
                }

                if (detachError != null)
                {
                    firstError ??= detachError;
                    System.Diagnostics.Debug.WriteLine(
                        "[NavigationRuntime] Page detach failed during teardown: " +
                        detachError);
                }

                _attachedPages.Remove(page);
                _visiblePages.Remove(page);

                Exception? disposeError;
                if (_ctx.Registry.TryGetDescriptor(page.GetType(), out var descriptor))
                    disposeError = Cleanup(
                        page,
                        descriptor,
                        forceDispose: true,
                        attemptTrace: null,
                        reason: reason + "Disposed");
                else
                    disposeError = DisposePage(
                        page,
                        null,
                        reason + "Disposed");

                firstError ??= disposeError;
            }

            _attachedPages.Clear();
            _visiblePages.Clear();
            return firstError;
        }

        private static void RaiseSafely(Action? subscribers, string eventName)
        {
            if (subscribers == null)
                return;

            foreach (Action subscriber in subscribers.GetInvocationList())
            {
                try
                {
                    subscriber();
                }
                catch (Exception ex)
                {
                    WriteSubscriberFailure(eventName, ex);
                }
            }
        }

        private static void RaiseSafely<T>(
            Action<T>? subscribers,
            T value,
            string eventName)
        {
            if (subscribers == null)
                return;

            foreach (Action<T> subscriber in subscribers.GetInvocationList())
            {
                try
                {
                    subscriber(value);
                }
                catch (Exception ex)
                {
                    WriteSubscriberFailure(eventName, ex);
                }
            }
        }

        private static void RaiseSafely<T1, T2, T3>(
            Action<T1, T2, T3>? subscribers,
            T1 value1,
            T2 value2,
            T3 value3,
            string eventName)
        {
            if (subscribers == null)
                return;

            foreach (Action<T1, T2, T3> subscriber in subscribers.GetInvocationList())
            {
                try
                {
                    subscriber(value1, value2, value3);
                }
                catch (Exception ex)
                {
                    WriteSubscriberFailure(eventName, ex);
                }
            }
        }

        private static void WriteSubscriberFailure(string eventName, Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[NavigationRuntime] {eventName} subscriber failed: {ex}");
        }

        private void EmitPageDecision(
            NavigationAttemptTraceScope? attemptTrace,
            IPageView page,
            string decision,
            bool? isDisposed = null)
        {
            if (page == null)
                return;

            EmitPageDecision(
                attemptTrace,
                page.GetType().FullName ?? page.GetType().Name,
                decision,
                isDisposed ?? page.IsDisposed);
        }

        private void EmitPageDecision(
            NavigationAttemptTraceScope? attemptTrace,
            string page,
            string decision,
            bool? isDisposed = null)
        {
            if (!_diagnostics.TraceEventsEnabled)
                return;

            var attached = _attachedPages.Count;
            var visible = _visiblePages.Count;
            var strong = _strongCache.Count;
            var weak = _weakCache.Count;
            var background = Volatile.Read(ref _backgroundLoadCount);
            var back = _ctx.History.HistoryBack.Count();
            var forward = _ctx.History.HistoryForward.Count();

            if (attemptTrace != null)
            {
                attemptTrace.EmitPage(
                    page,
                    decision,
                    isDisposed,
                    attached,
                    visible,
                    strong,
                    weak,
                    background,
                    back,
                    forward);
                return;
            }

            _diagnostics.EmitTrace(new NavigationTraceEvent(
                NavigationTraceKind.Page,
                RuntimeId,
                trigger: NavigationTraceTrigger.Runtime,
                targetPage: page,
                decision: decision,
                isDisposed: isDisposed,
                attachedCount: attached,
                visibleCount: visible,
                strongCacheCount: strong,
                weakCacheCount: weak,
                backgroundLoadCount: background,
                backHistoryCount: back,
                forwardHistoryCount: forward));
        }

        private void EmitRuntimeState(
            string decision,
            NavigationTraceStage stage = NavigationTraceStage.None,
            bool? success = null,
            string? errorType = null,
            int? queueDepth = null,
            long elapsedMilliseconds = 0)
        {
            if (!_diagnostics.TraceEventsEnabled)
                return;

            _diagnostics.EmitRuntime(
                RuntimeId,
                stage,
                decision,
                success,
                errorType,
                Current?.GetType().FullName ?? Current?.GetType().Name,
                queueDepth ?? Volatile.Read(ref _queuedRequestCount),
                _attachedPages.Count,
                _visiblePages.Count,
                _strongCache.Count,
                _weakCache.Count,
                Volatile.Read(ref _backgroundLoadCount),
                _ctx.History.HistoryBack.Count(),
                _ctx.History.HistoryForward.Count(),
                elapsedMilliseconds);
        }

        private void RaiseBlankStateIfNeeded(
            bool hadAttachedAtStart,
            bool hadVisibleAtStart)
        {
            if (hadAttachedAtStart && _attachedPages.Count == 0)
                RaiseSafely(OnNoPageAttached, nameof(OnNoPageAttached));

            if (hadVisibleAtStart && _visiblePages.Count == 0)
                RaiseSafely(OnNoPageVisible, nameof(OnNoPageVisible));

            if ((hadAttachedAtStart && _attachedPages.Count == 0) ||
                (hadVisibleAtStart && _visiblePages.Count == 0))
            {
                EmitRuntimeState("BlankShell");
            }
        }

        private sealed class NavigationAttemptResult
        {
            public bool TargetNavigated { get; }
            public NavigationTraceOutcome RequestOutcome { get; }
            public string Decision { get; }
            public string TerminalTarget { get; }

            private NavigationAttemptResult(
                bool targetNavigated,
                NavigationTraceOutcome requestOutcome,
                string decision,
                string terminalTarget)
            {
                TargetNavigated = targetNavigated;
                RequestOutcome = requestOutcome;
                Decision = decision;
                TerminalTarget = terminalTarget;
            }

            public static NavigationAttemptResult Succeeded(
                Type target,
                string? logicalName)
                => new NavigationAttemptResult(
                    true,
                    NavigationTraceOutcome.Succeeded,
                    "Navigated",
                    logicalName ?? target.FullName ?? target.Name);

            public static NavigationAttemptResult Denied(
                Type target,
                string? logicalName)
                => new NavigationAttemptResult(
                    false,
                    NavigationTraceOutcome.Denied,
                    "GuardDenied",
                    logicalName ?? target.FullName ?? target.Name);

            public static NavigationAttemptResult Redirected(
                NavigationAttemptResult child)
                => new NavigationAttemptResult(
                    false,
                    child.RequestOutcome,
                    "Redirected",
                    child.TerminalTarget);

            public static NavigationAttemptResult NoHistory()
                => new NavigationAttemptResult(
                    false,
                    NavigationTraceOutcome.NoHistory,
                    "NoHistory",
                    "<history>");
        }

        private readonly struct PageLoadResult
        {
            public static readonly PageLoadResult AppliedResult =
                new PageLoadResult(true, null);

            public bool Applied { get; }
            public string? DiscardReason { get; }

            private PageLoadResult(bool applied, string? discardReason)
            {
                Applied = applied;
                DiscardReason = discardReason;
            }

            public static PageLoadResult Discarded(string reason)
                => new PageLoadResult(
                    false,
                    reason ?? throw new ArgumentNullException(nameof(reason)));
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
        private Exception? DisposeCachedPages(string reason)
        {
            Exception? firstError = null;

            foreach (var page in _strongCache.Values)
            {
                var disposeError = DisposePage(
                    page,
                    null,
                    reason + "StrongCacheDisposed");
                firstError ??= disposeError;
            }
            _strongCache.Clear();

            foreach (var weak in _weakCache.Values)
            {
                if (weak.TryGetTarget(out var page))
                {
                    var disposeError = DisposePage(
                        page,
                        null,
                        reason + "WeakCacheDisposed");
                    firstError ??= disposeError;
                }
            }
            _weakCache.Clear();
            EmitRuntimeState(reason + "CachesCleared");
            return firstError;
        }
    }
}
