// FILE: NekoLib.Navigation/Diagnostics/InspectionNavigationObserver.cs
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NekoLib.Core;
using NekoLib.Core.Inspection;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Runtime.Core;

namespace NekoLib.Navigation.Diagnostics
{
    /// <summary>
    /// Projects the scalar navigation trace into Inspection operations and
    /// pull-based state. Providers only read immutable copies maintained by this
    /// observer; <see cref="IInspectionRecorder"/> consumers never touch UI objects,
    /// navigation history, caches or the runtime itself.
    /// </summary>
    public sealed class InspectionNavigationObserver : IDisposable
    {
        public const string Module = "Navigation";

        private const long SlowStageMilliseconds = 1000;

        private readonly object _sync = new object();
        private readonly object _recordSync = new object();
        private readonly NavigationEventHub _hub;
        private readonly NavigationContext? _context;
        private readonly IInspectionRecorder _debug;
        private readonly bool _staticWired;
        private readonly List<IDisposable> _registrations =
            new List<IDisposable>(16);
        private readonly Dictionary<string, RequestMirror> _requests =
            new Dictionary<string, RequestMirror>(StringComparer.Ordinal);
        private readonly Dictionary<string, AttemptMirror> _attempts =
            new Dictionary<string, AttemptMirror>(StringComparer.Ordinal);
        private readonly Dictionary<string, PageMirror> _pages =
            new Dictionary<string, PageMirror>(StringComparer.Ordinal);
        private readonly Dictionary<string, BackgroundMirror> _background =
            new Dictionary<string, BackgroundMirror>(StringComparer.Ordinal);
        private readonly Dictionary<string, SurfaceMirror> _surfaces =
            new Dictionary<string, SurfaceMirror>(StringComparer.Ordinal);
        private readonly HashSet<string> _timedOutRequests =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly IReadOnlyList<object> _registryPages;
        private readonly IReadOnlyDictionary<string, string> _registryNamesByType;

        private string? _runtimeId;
        private string _runtimeStatus = "NotStarted";
        private string? _runtimeDecision;
        private DateTime? _runtimeUpdatedUtc;
        private bool _runtimeDisposed;

        private string? _currentName;
        private string? _currentType;
        private DateTime? _currentChangedUtc;
        private NavigationOutcomeMirror? _lastNavigation;

        private int _queueDepth;
        private int _attachedCount;
        private int _visibleCount;
        private int _strongCacheCount;
        private int _weakCacheCount;
        private int _backgroundLoadCount;
        private int _backHistoryCount;
        private int _forwardHistoryCount;
        private bool _shellBlankActive;
        private string? _lastPage;
        private string? _lastPageDecision;
        private DateTime? _lastPageUpdatedUtc;

        private SurfaceTerminalMirror? _lastSurfaceTerminal;

        private string _idleStatus = "NotObserved";
        private string? _idleDecision;
        private DateTime? _idleUpdatedUtc;
        private DateTime? _idleConfiguredUtc;
        private DateTime? _idleLastInteractionUtc;
        private DateTime? _idleLastElapsedUtc;
        private DateTime? _idleDisposedUtc;
        private int? _idleIntervalMilliseconds;
        private string? _idleErrorType;

        private string[] _historyBack = new string[0];
        private string[] _historyForward = new string[0];
        private bool _sessionAuthenticated;
        private int _sessionRoleCount;
        private int _sessionPermissionCount;

        private int _requestsStarted;
        private int _requestsCompleted;
        private int _attemptsStarted;
        private int _attemptsCompleted;
        private int _navigated;
        private int _failed;
        private int _guardDenied;
        private int _guardTimeouts;
        private int _redirects;
        private int _noHistory;
        private int _discarded;
        private int _backNavigations;
        private int _idleNavigations;
        private int _backgroundCompleted;
        private int _backgroundDiscarded;
        private int _backgroundFailed;
        private int _blankShellDetections;
        private string _lastStarted = "<none>";
        private DateTime? _lastStartedUtc;
        private int _disposed;

        private InspectionNavigationObserver(
            NavigationEventHub hub,
            NavigationContext? context,
            IInspectionRecorder debug)
        {
            _hub = hub;
            _context = context;
            _debug = debug;
            _staticWired = context != null;
            _registryPages = context == null
                ? new List<object>().AsReadOnly()
                : CaptureRegistry(context);
            _registryNamesByType = context == null
                ? new Dictionary<string, string>()
                : CaptureRegistryNames(context);

            if (context != null)
            {
                RefreshHistoryMirror();
                RefreshSessionMirror();
            }

            try
            {
                Wire();
                RegisterProviders();
            }
            catch
            {
                // Provider keys are unique process-wide. If any registration is
                // rejected, remove everything installed by this instance and
                // detach every event before propagating the deterministic error.
                Unwire();
                DisposeRegistrations();
                throw;
            }
        }

        public static IDisposable Attach(
            NavigationContext context,
            IInspectionRecorder debug)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));
            if (debug is null || !debug.IsEnabled)
                return Disposable.Empty;

            return new InspectionNavigationObserver(
                context.Events,
                context,
                debug);
        }

        public static IDisposable Attach(
            NavigationEventHub hub,
            IInspectionRecorder debug)
        {
            if (hub is null)
                throw new ArgumentNullException(nameof(hub));
            if (debug is null || !debug.IsEnabled)
                return Disposable.Empty;

            return new InspectionNavigationObserver(hub, null, debug);
        }

        private void Wire()
        {
            _hub.NavigationTrace += OnNavigationTrace;

            // Compatibility stream for callers that manually publish the legacy
            // hub events. Runtime-correlated events are ignored by these handlers
            // because their scalar trace is the canonical source.
            _hub.NavigationStarted += OnLegacyNavigationStarted;
            _hub.NavigationLogged += OnLegacyNavigationLogged;
            _hub.GuardDenied += OnLegacyGuardDenied;

            if (!_staticWired)
                return;

            _context!.Session.Changed += RefreshSessionMirror;
            NavigationService.Navigating += OnFacadeNavigating;
            NavigationService.CurrentChanged += OnCurrentChanged;
            NavigationService.HistoryChanged += OnHistoryChanged;
            NavigationService.OnFirstPageAttached += OnFirstPageAttached;
            NavigationService.OnNoPageAttached += OnNoPageAttached;
            NavigationService.OnNoPageVisible += OnNoPageVisible;
        }

        private void Unwire()
        {
            _hub.NavigationTrace -= OnNavigationTrace;
            _hub.NavigationStarted -= OnLegacyNavigationStarted;
            _hub.NavigationLogged -= OnLegacyNavigationLogged;
            _hub.GuardDenied -= OnLegacyGuardDenied;

            if (!_staticWired)
                return;

            _context!.Session.Changed -= RefreshSessionMirror;
            NavigationService.Navigating -= OnFacadeNavigating;
            NavigationService.CurrentChanged -= OnCurrentChanged;
            NavigationService.HistoryChanged -= OnHistoryChanged;
            NavigationService.OnFirstPageAttached -= OnFirstPageAttached;
            NavigationService.OnNoPageAttached -= OnNoPageAttached;
            NavigationService.OnNoPageVisible -= OnNoPageVisible;
        }

        private void RegisterProviders()
        {
            Register("runtime", SnapshotRuntime);
            Register("inFlight", SnapshotInFlight);
            Register("activeAttempts", SnapshotActiveAttempts);
            Register("queue", SnapshotQueue);
            Register("current", SnapshotCurrent);
            Register("currentPage", SnapshotCurrent);
            Register("lastNavigation", SnapshotLastNavigation);
            Register("pages", SnapshotPages);
            Register("cache", SnapshotCache);
            Register("backgroundLoads", SnapshotBackgroundLoads);
            Register("overlays", SnapshotOverlays);
            Register("idle", SnapshotIdle);
            Register("stats", SnapshotStats);

            if (_context != null)
            {
                Register("registry", SnapshotRegistry);
                Register("history", SnapshotHistory);
                Register("session", SnapshotSession);
            }
        }

        private void Register(string key, Func<object> snapshot)
            => _registrations.Add(
                _debug.RegisterStateProvider(Module, key, snapshot));

        // -----------------------------------------------------------------
        // Scalar trace
        // -----------------------------------------------------------------

        private void OnNavigationTrace(NavigationTraceEvent e)
        {
            if (e is null || Volatile.Read(ref _disposed) != 0)
                return;

            var operations = new List<PendingOperation>(2);

            lock (_sync)
            {
                ApplyCommonCounts(e);
                _runtimeId = e.RuntimeId;
                _runtimeUpdatedUtc = e.TimestampUtc;

                switch (e.Kind)
                {
                    case NavigationTraceKind.RequestStarted:
                        OnRequestStarted(e, operations);
                        break;

                    case NavigationTraceKind.RequestStage:
                        OnRequestStage(e, operations);
                        break;

                    case NavigationTraceKind.RequestCompleted:
                        OnRequestCompleted(e, operations);
                        break;

                    case NavigationTraceKind.AttemptStarted:
                        OnAttemptStarted(e);
                        break;

                    case NavigationTraceKind.AttemptStage:
                        OnAttemptStage(e, operations);
                        break;

                    case NavigationTraceKind.AttemptCompleted:
                        OnAttemptCompleted(e, operations);
                        break;

                    case NavigationTraceKind.Page:
                        OnPageTrace(e);
                        break;

                    case NavigationTraceKind.BackgroundLoadStarted:
                    case NavigationTraceKind.BackgroundLoadCompleted:
                    case NavigationTraceKind.BackgroundLoadDiscarded:
                    case NavigationTraceKind.BackgroundLoadFailed:
                        OnBackgroundTrace(e, operations);
                        break;

                    case NavigationTraceKind.Runtime:
                        OnRuntimeTrace(e, operations);
                        break;

                    case NavigationTraceKind.SurfaceOpening:
                    case NavigationTraceKind.SurfaceOpened:
                    case NavigationTraceKind.SurfaceClosed:
                    case NavigationTraceKind.SurfaceFailed:
                        OnSurfaceTrace(e, operations);
                        break;

                    case NavigationTraceKind.IdleConfigured:
                    case NavigationTraceKind.IdleInteraction:
                    case NavigationTraceKind.IdleElapsed:
                    case NavigationTraceKind.IdleNavigationFailed:
                    case NavigationTraceKind.IdleDisposed:
                        OnIdleTrace(e, operations);
                        break;
                }
            }

            Record(operations);
        }

        private void OnRequestStarted(
            NavigationTraceEvent e,
            List<PendingOperation> operations)
        {
            if (e.RequestId is null)
                return;

            _requests[e.RequestId] = new RequestMirror(e);
            _requestsStarted++;
            _lastStarted = e.TargetPage ?? "<unknown>";
            _lastStartedUtc = e.TimestampUtc;
            _runtimeStatus = "Running";

            if (e.Trigger == NavigationTraceTrigger.Idle)
            {
                _idleNavigations++;
                _idleStatus = "Navigating";
                _idleDecision = "RequestStarted";
                _idleUpdatedUtc = e.TimestampUtc;
            }

            operations.Add(new PendingOperation(
                "NavigationStarted",
                new
                {
                    runtimeId = e.RuntimeId,
                    requestId = e.RequestId,
                    trigger = e.Trigger.ToString(),
                    from = e.FromPage,
                    to = e.TargetPage,
                    requestedLoadMode = e.RequestedLoadMode,
                    back = e.IsBackNavigation,
                    timestampUtc = e.TimestampUtc
                }));
        }

        private void OnRequestStage(
            NavigationTraceEvent e,
            List<PendingOperation> operations)
        {
            RequestMirror? request;
            if (e.RequestId != null &&
                _requests.TryGetValue(e.RequestId, out request))
            {
                request.Update(e);
            }

            AddSlowStage(e, "Request", operations);
        }

        private void OnRequestCompleted(
            NavigationTraceEvent e,
            List<PendingOperation> operations)
        {
            AddSlowStage(e, "Request", operations);

            if (e.RequestId != null)
                _requests.Remove(e.RequestId);

            if (e.RequestId != null)
            {
                var attemptIds = _attempts
                    .Where(pair => pair.Value.RequestId == e.RequestId)
                    .Select(pair => pair.Key)
                    .ToArray();
                foreach (var attemptId in attemptIds)
                    _attempts.Remove(attemptId);
            }

            _requestsCompleted++;
            _lastNavigation = NavigationOutcomeMirror.FromTrace(e);
            if (e.RequestId != null &&
                _timedOutRequests.Remove(e.RequestId))
            {
                _lastNavigation.MarkTimeout();
            }

            string operation;
            switch (e.Outcome)
            {
                case NavigationTraceOutcome.Succeeded:
                case NavigationTraceOutcome.Redirected:
                    _navigated++;
                    operation = "Navigated";
                    if (e.IsBackNavigation)
                        _backNavigations++;
                    break;

                case NavigationTraceOutcome.Denied:
                    _guardDenied++;
                    operation = "GuardDenied";
                    break;

                case NavigationTraceOutcome.NoHistory:
                    _noHistory++;
                    operation = "NavigationNoHistory";
                    break;

                case NavigationTraceOutcome.Discarded:
                    _discarded++;
                    operation = "NavigationDiscarded";
                    break;

                default:
                    _failed++;
                    operation = "NavigationFailed";
                    break;
            }

            if (e.Trigger == NavigationTraceTrigger.Idle)
            {
                _idleStatus = e.Success == true ? "Completed" : "Failed";
                _idleDecision = e.Decision ?? e.Outcome.ToString();
                _idleUpdatedUtc = e.TimestampUtc;
            }

            operations.Add(new PendingOperation(
                operation,
                SnapshotOutcome(_lastNavigation)));

            if (IsBlankLocked() && !_shellBlankActive)
            {
                _shellBlankActive = true;
                _blankShellDetections++;
                operations.Add(new PendingOperation(
                    "ShellBlankDetected",
                    new
                    {
                        runtimeId = e.RuntimeId,
                        requestId = e.RequestId,
                        outcome = e.Outcome.ToString(),
                        target = e.TargetPage,
                        attached = _attachedCount,
                        visible = _visibleCount,
                        timestampUtc = e.TimestampUtc
                    }));
            }
        }

        private void OnAttemptStarted(NavigationTraceEvent e)
        {
            if (e.AttemptId is null)
                return;

            _attempts[e.AttemptId] = new AttemptMirror(e);
            _attemptsStarted++;
        }

        private void OnAttemptStage(
            NavigationTraceEvent e,
            List<PendingOperation> operations)
        {
            AttemptMirror? attempt;
            if (e.AttemptId != null &&
                _attempts.TryGetValue(e.AttemptId, out attempt))
            {
                attempt.Update(e);

                // CycleCheck is the first trace immediately after descriptor
                // resolution and the public Navigating signal. It therefore means
                // "a registered target is now being processed", before its guard.
                if (e.Stage == NavigationTraceStage.CycleCheck &&
                    !attempt.NavigatingRecorded)
                {
                    attempt.NavigatingRecorded = true;
                    operations.Add(new PendingOperation(
                        "Navigating",
                        new
                        {
                            runtimeId = e.RuntimeId,
                            requestId = e.RequestId,
                            attemptId = e.AttemptId,
                            parentAttemptId = e.ParentAttemptId,
                            redirectDepth = e.RedirectDepth,
                            trigger = e.Trigger.ToString(),
                            from = e.FromPage,
                            to = e.TargetPage,
                            effectiveLoadMode = e.EffectiveLoadMode,
                            back = e.IsBackNavigation,
                            timestampUtc = e.TimestampUtc
                        }));
                }
            }

            AddSlowStage(e, "Attempt", operations);
        }

        private void OnAttemptCompleted(
            NavigationTraceEvent e,
            List<PendingOperation> operations)
        {
            AddSlowStage(e, "Attempt", operations);

            if (e.AttemptId != null)
                _attempts.Remove(e.AttemptId);

            _attemptsCompleted++;
            if (e.Outcome == NavigationTraceOutcome.Redirected)
            {
                _redirects++;
                operations.Add(new PendingOperation(
                    "GuardRedirected",
                    new
                    {
                        runtimeId = e.RuntimeId,
                        requestId = e.RequestId,
                        attemptId = e.AttemptId,
                        parentAttemptId = e.ParentAttemptId,
                        redirectDepth = e.RedirectDepth,
                        from = e.FromPage,
                        target = e.TargetPage,
                        decision = e.Decision,
                        durationMs = e.ElapsedMilliseconds,
                        timestampUtc = e.TimestampUtc
                    }));
            }
            if (string.Equals(
                e.Decision,
                "GuardTimeout",
                StringComparison.Ordinal))
            {
                _guardTimeouts++;
                if (e.RequestId != null)
                    _timedOutRequests.Add(e.RequestId);
            }

            if (e.Outcome == NavigationTraceOutcome.Failed)
            {
                operations.Add(new PendingOperation(
                    "StageFailed",
                    StagePayload(e, "Attempt")));
            }
        }

        private void OnPageTrace(NavigationTraceEvent e)
        {
            var page = e.TargetPage ?? "<unknown>";
            PageMirror? mirror;
            if (!_pages.TryGetValue(page, out mirror))
            {
                mirror = new PageMirror(page);
                _pages.Add(page, mirror);
            }

            mirror.Update(e);
            _lastPage = page;
            _lastPageDecision = e.Decision;
            _lastPageUpdatedUtc = e.TimestampUtc;

            var decision = e.Decision ?? string.Empty;
            if (decision.IndexOf(
                "CurrentChanged",
                StringComparison.OrdinalIgnoreCase) >= 0)
            {
                SetCurrent(page, LogicalName(page), e.TimestampUtc);
            }

            if (_attachedCount > 0 || _visibleCount > 0)
                _shellBlankActive = false;
        }

        private void OnBackgroundTrace(
            NavigationTraceEvent e,
            List<PendingOperation> operations)
        {
            var id = e.BackgroundOperationId;
            if (e.Kind == NavigationTraceKind.BackgroundLoadStarted)
            {
                if (id != null)
                    _background[id] = new BackgroundMirror(e);
                return;
            }

            BackgroundMirror? mirror = null;
            if (id != null)
            {
                _background.TryGetValue(id, out mirror);
                _background.Remove(id);
            }

            var payload = new
            {
                runtimeId = e.RuntimeId,
                requestId = e.RequestId,
                attemptId = e.AttemptId,
                operationId = id,
                page = e.TargetPage ?? mirror?.TargetPage,
                outcome = e.Outcome.ToString(),
                decision = e.Decision,
                errorType = e.ErrorType,
                durationMs = e.ElapsedMilliseconds,
                timestampUtc = e.TimestampUtc
            };

            switch (e.Kind)
            {
                case NavigationTraceKind.BackgroundLoadCompleted:
                    _backgroundCompleted++;
                    operations.Add(new PendingOperation(
                        "BackgroundLoadCompleted",
                        payload));
                    break;

                case NavigationTraceKind.BackgroundLoadDiscarded:
                    _backgroundDiscarded++;
                    operations.Add(new PendingOperation(
                        "BackgroundLoadDiscarded",
                        payload));
                    break;

                case NavigationTraceKind.BackgroundLoadFailed:
                    _backgroundFailed++;
                    operations.Add(new PendingOperation(
                        "BackgroundLoadFailed",
                        payload));
                    break;
            }
        }

        private void OnRuntimeTrace(
            NavigationTraceEvent e,
            List<PendingOperation> operations)
        {
            _runtimeDecision = e.Decision;

            var decision = e.Decision ?? string.Empty;
            switch (decision)
            {
                case "Created":
                    _runtimeStatus = "Created";
                    break;
                case "ResetStarted":
                    _runtimeStatus = "Resetting";
                    break;
                case "ResetCompleted":
                    _runtimeStatus = "Running";
                    break;
                case "ResetFailed":
                    _runtimeStatus = "Faulted";
                    break;
                case "DisposeStarted":
                    _runtimeStatus = "ShuttingDown";
                    break;
                case "DisposeCompleted":
                    _runtimeStatus = "Disposed";
                    break;
                case "DisposeFailed":
                    _runtimeStatus = "Faulted";
                    break;
            }

            if (decision == "ResetCompleted" || decision == "DisposeCompleted")
            {
                SetCurrent(null, null, e.TimestampUtc);
            }

            string? operation = null;
            if (decision == "ResetStarted" ||
                decision == "ResetCompleted" ||
                decision == "ResetFailed")
            {
                operation = decision;
            }
            else if (decision == "DisposeStarted")
            {
                operation = "ShutdownStarted";
            }
            else if (decision == "DisposeCompleted")
            {
                operation = "ShutdownCompleted";
                _runtimeDisposed = true;
            }
            else if (decision == "DisposeFailed")
            {
                operation = "ShutdownFailed";
            }

            if (operation != null)
            {
                operations.Add(new PendingOperation(
                    operation,
                    new
                    {
                        runtimeId = e.RuntimeId,
                        decision = e.Decision,
                        success = e.Success,
                        errorType = e.ErrorType,
                        durationMs = e.ElapsedMilliseconds,
                        timestampUtc = e.TimestampUtc
                    }));
            }
        }

        private void OnSurfaceTrace(
            NavigationTraceEvent e,
            List<PendingOperation> operations)
        {
            var id = e.SurfaceId;
            if (id is null)
                return;

            SurfaceMirror? surface;
            switch (e.Kind)
            {
                case NavigationTraceKind.SurfaceOpening:
                    _surfaces[id] = new SurfaceMirror(e);
                    break;

                case NavigationTraceKind.SurfaceOpened:
                    if (!_surfaces.TryGetValue(id, out surface))
                    {
                        surface = new SurfaceMirror(e);
                        _surfaces.Add(id, surface);
                    }
                    surface.Opened(e);
                    operations.Add(new PendingOperation(
                        "SurfaceOpened",
                        SurfacePayload(e)));
                    break;

                case NavigationTraceKind.SurfaceClosed:
                case NavigationTraceKind.SurfaceFailed:
                    _surfaces.TryGetValue(id, out surface);
                    _surfaces.Remove(id);
                    _lastSurfaceTerminal =
                        SurfaceTerminalMirror.FromTrace(e, surface);
                    operations.Add(new PendingOperation(
                        e.Kind == NavigationTraceKind.SurfaceFailed
                            ? "SurfaceFailed"
                            : "SurfaceClosed",
                        SurfacePayload(e)));
                    break;
            }
        }

        private void OnIdleTrace(
            NavigationTraceEvent e,
            List<PendingOperation> operations)
        {
            _idleDecision = e.Decision ?? e.Kind.ToString();
            _idleUpdatedUtc = e.TimestampUtc;

            string? operation = null;
            switch (e.Kind)
            {
                case NavigationTraceKind.IdleConfigured:
                    _idleIntervalMilliseconds = e.IdleIntervalMilliseconds;
                    if (e.Success == false)
                    {
                        _idleStatus = "Unavailable";
                        _idleConfiguredUtc = null;
                        _idleErrorType = e.ErrorType;
                        operation = "IdleConfigurationFailed";
                    }
                    else
                    {
                        _idleStatus = "Armed";
                        _idleConfiguredUtc = e.TimestampUtc;
                        _idleErrorType = null;
                        operation = "IdleConfigured";
                    }
                    break;

                case NavigationTraceKind.IdleInteraction:
                    _idleStatus = "Armed";
                    _idleLastInteractionUtc = e.TimestampUtc;
                    // Interactions can be very frequent and deliberately do not
                    // enter the Inspection ring.
                    break;

                case NavigationTraceKind.IdleElapsed:
                    _idleStatus = "Elapsed";
                    _idleLastElapsedUtc = e.TimestampUtc;
                    operation = "IdleElapsed";
                    break;

                case NavigationTraceKind.IdleNavigationFailed:
                    _idleStatus = "Failed";
                    _idleErrorType = e.ErrorType;
                    operation = "IdleNavigationFailed";
                    break;

                case NavigationTraceKind.IdleDisposed:
                    _idleStatus = "Disposed";
                    _idleDisposedUtc = e.TimestampUtc;
                    operation = "IdleDisposed";
                    break;
            }

            if (operation != null)
            {
                operations.Add(new PendingOperation(
                    operation,
                    new
                    {
                        runtimeId = e.RuntimeId,
                        intervalMs = e.IdleIntervalMilliseconds ??
                            _idleIntervalMilliseconds,
                        decision = e.Decision,
                        success = e.Success,
                        errorType = e.ErrorType,
                        durationMs = e.ElapsedMilliseconds,
                        timestampUtc = e.TimestampUtc
                    }));
            }
        }

        private static object SurfacePayload(NavigationTraceEvent e)
            => new
            {
                runtimeId = e.RuntimeId,
                surfaceId = e.SurfaceId,
                kind = e.SurfaceKind,
                type = e.TargetPage,
                depth = e.SurfaceDepth,
                outcome = e.Outcome.ToString(),
                closeReason = e.CloseReason,
                success = e.Success,
                errorType = e.ErrorType,
                durationMs = e.ElapsedMilliseconds,
                timestampUtc = e.TimestampUtc
            };

        private void AddSlowStage(
            NavigationTraceEvent e,
            string scope,
            List<PendingOperation> operations)
        {
            if (e.StageElapsedMilliseconds < SlowStageMilliseconds)
                return;

            operations.Add(new PendingOperation(
                "SlowStage",
                StagePayload(e, scope)));
        }

        private static object StagePayload(
            NavigationTraceEvent e,
            string scope)
            => new
            {
                runtimeId = e.RuntimeId,
                requestId = e.RequestId,
                attemptId = e.AttemptId,
                scope,
                stage = e.PreviousStage.ToString(),
                nextStage = e.Stage.ToString(),
                target = e.TargetPage,
                elapsedMs = e.StageElapsedMilliseconds,
                totalDurationMs = e.ElapsedMilliseconds,
                errorType = e.ErrorType,
                timestampUtc = e.TimestampUtc
            };

        private void ApplyCommonCounts(NavigationTraceEvent e)
        {
            if (e.QueueDepth.HasValue)
                _queueDepth = Math.Max(0, e.QueueDepth.Value);
            if (e.AttachedCount.HasValue)
                _attachedCount = Math.Max(0, e.AttachedCount.Value);
            if (e.VisibleCount.HasValue)
                _visibleCount = Math.Max(0, e.VisibleCount.Value);
            if (e.StrongCacheCount.HasValue)
                _strongCacheCount = Math.Max(0, e.StrongCacheCount.Value);
            if (e.WeakCacheCount.HasValue)
                _weakCacheCount = Math.Max(0, e.WeakCacheCount.Value);
            if (e.BackgroundLoadCount.HasValue)
                _backgroundLoadCount = Math.Max(0, e.BackgroundLoadCount.Value);
            if (e.BackHistoryCount.HasValue)
                _backHistoryCount = Math.Max(0, e.BackHistoryCount.Value);
            if (e.ForwardHistoryCount.HasValue)
                _forwardHistoryCount = Math.Max(0, e.ForwardHistoryCount.Value);
        }

        // -----------------------------------------------------------------
        // Legacy hub compatibility
        // -----------------------------------------------------------------

        private void OnLegacyNavigationStarted(NavigationStartedEvent e)
        {
            if (e is null || e.RequestId != null ||
                Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            object payload;
            lock (_sync)
            {
                _requestsStarted++;
                _lastStarted = e.RequestedTargetName;
                _lastStartedUtc = e.TimestampUtc;

                payload = new
                {
                    runtimeId = e.RuntimeId,
                    requestId = e.RequestId,
                    trigger = e.Trigger.ToString(),
                    from = e.FromPage?.GetType().FullName,
                    to = e.RequestedTargetName,
                    requestedLoadMode = e.Args?.LoadMode.ToString(),
                    back = e.Args?.IsBackNavigation ?? false,
                    timestampUtc = e.TimestampUtc
                };
            }

            Record("NavigationStarted", payload);
        }

        private void OnLegacyNavigationLogged(PageLogEntry entry)
        {
            if (entry is null || entry.RequestId != null ||
                Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            object payload;
            lock (_sync)
            {
                _requestsCompleted++;
                _lastNavigation = NavigationOutcomeMirror.FromEntry(entry);

                if (entry.Success)
                {
                    _navigated++;
                    if (entry.IsBackNavigation)
                        _backNavigations++;
                    SetCurrent(
                        entry.ToPageType.FullName,
                        entry.ToPageName,
                        entry.TimestampUtc);
                }
                else
                {
                    _failed++;
                }

                if (entry.IsTimeout)
                    _guardTimeouts++;

                payload = SnapshotOutcome(_lastNavigation);
            }

            Record(
                entry.Success ? "Navigated" : "NavigationFailed",
                payload);
        }

        private void OnLegacyGuardDenied(GuardDeniedEvent e)
        {
            if (e is null || e.RequestId != null ||
                Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            object payload;
            lock (_sync)
            {
                _requestsCompleted++;
                _guardDenied++;
                _lastNavigation = NavigationOutcomeMirror.FromGuard(e);
                payload = SnapshotOutcome(_lastNavigation);
            }

            Record("GuardDenied", payload);
        }

        // -----------------------------------------------------------------
        // Facade mirrors. These handlers run where the runtime emits the public
        // events; providers never call the facade or inspect UI/history objects.
        // -----------------------------------------------------------------

        private void OnFacadeNavigating(
            IPageView? from,
            Type toType,
            NavigationArgs args)
            => RefreshSessionMirror();

        private void OnCurrentChanged(IPageView? page)
        {
            string? name = null;
            string? type = null;

            if (page != null)
            {
                type = page.GetType().FullName ?? page.GetType().Name;
                PageDescriptor? descriptor;
                if (_context != null &&
                    _context.Registry.TryGetDescriptor(
                        page.GetType(),
                        out descriptor))
                {
                    name = descriptor.Name;
                }
                else
                {
                    try { name = page.Name; }
                    catch { name = page.GetType().Name; }
                }
            }

            lock (_sync)
                SetCurrent(type, name, DateTime.UtcNow);

            RefreshSessionMirror();
        }

        private void OnHistoryChanged()
        {
            RefreshHistoryMirror();
            RefreshSessionMirror();
        }

        private void OnFirstPageAttached(IPageView page)
        {
            lock (_sync)
            {
                if (_attachedCount == 0)
                    _attachedCount = 1;
                _shellBlankActive = false;
            }
        }

        private void OnNoPageAttached()
        {
            lock (_sync)
                _attachedCount = 0;
        }

        private void OnNoPageVisible()
        {
            lock (_sync)
                _visibleCount = 0;
        }

        private void RefreshHistoryMirror()
        {
            var context = _context;
            if (context is null)
                return;

            string[] back;
            string[] forward;
            try
            {
                back = context.History.HistoryBack
                    .Select(entry => entry?.PageName ?? "<null>")
                    .ToArray();
                forward = context.History.HistoryForward
                    .Select(entry => entry?.PageName ?? "<null>")
                    .ToArray();
            }
            catch
            {
                // Event-thread refresh is best-effort. Keep the previous immutable
                // mirror rather than exposing a provider that can race the UI.
                return;
            }

            lock (_sync)
            {
                _historyBack = back;
                _historyForward = forward;
                _backHistoryCount = back.Length;
                _forwardHistoryCount = forward.Length;
            }
        }

        private void RefreshSessionMirror()
        {
            var context = _context;
            if (context is null)
                return;

            var session = context.Session;
            var authenticated = session.IsAuthenticated;
            var roleCount = session.Roles?.Count ?? 0;
            var permissionCount = session.Permissions?.Count ?? 0;

            lock (_sync)
            {
                _sessionAuthenticated = authenticated;
                _sessionRoleCount = roleCount;
                _sessionPermissionCount = permissionCount;
            }
        }

        // -----------------------------------------------------------------
        // State providers: lock + copy only. No runtime/UI access.
        // -----------------------------------------------------------------

        private object SnapshotRuntime()
        {
            lock (_sync)
            {
                return new
                {
                    runtimeId = _runtimeId,
                    status = _runtimeStatus,
                    lastDecision = _runtimeDecision,
                    disposed = _runtimeDisposed,
                    currentPage = _currentType,
                    attached = _attachedCount,
                    visible = _visibleCount,
                    updatedUtc = _runtimeUpdatedUtc
                };
            }
        }

        private object SnapshotInFlight()
        {
            lock (_sync)
            {
                return new
                {
                    count = _requests.Count,
                    requests = _requests.Values
                        .OrderBy(item => item.TimestampUtc)
                        .Select(item => item.Snapshot())
                        .ToList()
                        .AsReadOnly()
                };
            }
        }

        private object SnapshotActiveAttempts()
        {
            lock (_sync)
            {
                return new
                {
                    count = _attempts.Count,
                    attempts = _attempts.Values
                        .OrderBy(item => item.TimestampUtc)
                        .Select(item => item.Snapshot())
                        .ToList()
                        .AsReadOnly()
                };
            }
        }

        private object SnapshotQueue()
        {
            lock (_sync)
            {
                return new
                {
                    depth = _queueDepth,
                    processing = _requests.Values.Count(
                        request => request.Stage ==
                            NavigationTraceStage.Processing.ToString()),
                    inFlight = _requests.Count
                };
            }
        }

        private object SnapshotCurrent()
        {
            lock (_sync)
            {
                return new
                {
                    hasCurrent = _currentType != null || _currentName != null,
                    name = _currentName,
                    type = _currentType,
                    changedUtc = _currentChangedUtc
                };
            }
        }

        private object SnapshotLastNavigation()
        {
            lock (_sync)
                return SnapshotOutcome(_lastNavigation);
        }

        private object SnapshotPages()
        {
            lock (_sync)
            {
                return new
                {
                    current = _currentType,
                    attached = _attachedCount,
                    visible = _visibleCount,
                    lastPage = _lastPage,
                    lastDecision = _lastPageDecision,
                    lastUpdatedUtc = _lastPageUpdatedUtc,
                    tracked = _pages.Values
                        .OrderBy(page => page.Page, StringComparer.Ordinal)
                        .Select(page => page.Snapshot())
                        .ToList()
                        .AsReadOnly()
                };
            }
        }

        private object SnapshotCache()
        {
            lock (_sync)
            {
                return new
                {
                    strong = _strongCacheCount,
                    weak = _weakCacheCount,
                    total = _strongCacheCount + _weakCacheCount
                };
            }
        }

        private object SnapshotBackgroundLoads()
        {
            lock (_sync)
            {
                return new
                {
                    active = _backgroundLoadCount,
                    operations = _background.Values
                        .OrderBy(item => item.TimestampUtc)
                        .Select(item => item.Snapshot())
                        .ToList()
                        .AsReadOnly(),
                    completed = _backgroundCompleted,
                    discarded = _backgroundDiscarded,
                    failed = _backgroundFailed
                };
            }
        }

        private object SnapshotOverlays()
        {
            lock (_sync)
            {
                return new
                {
                    active = _surfaces.Count,
                    surfaces = _surfaces.Values
                        .OrderBy(surface => surface.Depth)
                        .ThenBy(surface => surface.OpeningUtc)
                        .Select(surface => surface.Snapshot())
                        .ToList()
                        .AsReadOnly(),
                    lastTerminal = _lastSurfaceTerminal?.Snapshot()
                };
            }
        }

        private object SnapshotIdle()
        {
            lock (_sync)
            {
                return new
                {
                    status = _idleStatus,
                    decision = _idleDecision,
                    intervalMs = _idleIntervalMilliseconds,
                    configuredUtc = _idleConfiguredUtc,
                    lastInteractionUtc = _idleLastInteractionUtc,
                    lastElapsedUtc = _idleLastElapsedUtc,
                    disposedUtc = _idleDisposedUtc,
                    errorType = _idleErrorType,
                    navigations = _idleNavigations,
                    updatedUtc = _idleUpdatedUtc
                };
            }
        }

        private object SnapshotRegistry()
            => new
            {
                count = _registryPages.Count,
                pages = _registryPages
            };

        private object SnapshotHistory()
        {
            lock (_sync)
            {
                var back = new List<string>(_historyBack).AsReadOnly();
                var forward = new List<string>(_historyForward).AsReadOnly();
                return new
                {
                    canGoBack = _backHistoryCount > 0,
                    canGoForward = _forwardHistoryCount > 0,
                    backCount = _backHistoryCount,
                    forwardCount = _forwardHistoryCount,
                    back,
                    forward
                };
            }
        }

        private object SnapshotSession()
        {
            lock (_sync)
            {
                return new
                {
                    authenticated = _sessionAuthenticated,
                    roleCount = _sessionRoleCount,
                    permissionCount = _sessionPermissionCount
                };
            }
        }

        private object SnapshotStats()
        {
            lock (_sync)
            {
                return new
                {
                    // Compatibility aliases retained for existing consumers.
                    started = _requestsStarted,
                    navigated = _navigated,
                    failed = _failed,
                    guardDenied = _guardDenied,
                    timeouts = _guardTimeouts,
                    backNavigations = _backNavigations,
                    lastStarted = _lastStarted,
                    lastStartedUtc = _lastStartedUtc,

                    requestsStarted = _requestsStarted,
                    requestsCompleted = _requestsCompleted,
                    requestsInFlight = _requests.Count,
                    attemptsStarted = _attemptsStarted,
                    attemptsCompleted = _attemptsCompleted,
                    activeAttempts = _attempts.Count,
                    queueDepth = _queueDepth,
                    guardTimeouts = _guardTimeouts,
                    redirects = _redirects,
                    noHistory = _noHistory,
                    discarded = _discarded,
                    idleNavigations = _idleNavigations,
                    backgroundCompleted = _backgroundCompleted,
                    backgroundDiscarded = _backgroundDiscarded,
                    backgroundFailed = _backgroundFailed,
                    blankShellDetections = _blankShellDetections
                };
            }
        }

        private static IReadOnlyList<object> CaptureRegistry(
            NavigationContext context)
        {
            var pages = context.Registry.AllDescriptors()
                .OrderBy(
                    descriptor => descriptor.Name,
                    StringComparer.OrdinalIgnoreCase)
                .Select(descriptor => (object)new
                {
                    name = descriptor.Name,
                    type = descriptor.PageType.FullName ??
                        descriptor.PageType.Name,
                    role = descriptor.Role.ToString(),
                    reuse = descriptor.ReusePolicy.ToString(),
                    loadMode = descriptor.LoadMode.ToString(),
                    allowAnonymous = descriptor.AllowAnonymous,
                    keepAttached = descriptor.KeepAttachedWhenHidden,
                    tagCount = descriptor.Tags?.Count ?? 0,
                    hasGuard = descriptor.Guard != null
                })
                .ToList();

            return pages.AsReadOnly();
        }

        private static IReadOnlyDictionary<string, string> CaptureRegistryNames(
            NavigationContext context)
        {
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var descriptor in context.Registry.AllDescriptors())
            {
                var type = descriptor.PageType.FullName ??
                    descriptor.PageType.Name;
                names[type] = descriptor.Name;
            }

            return names;
        }

        private static object SnapshotOutcome(
            NavigationOutcomeMirror? outcome)
        {
            if (outcome is null)
            {
                return new
                {
                    hasValue = false,
                    runtimeId = (string?)null,
                    requestId = (string?)null,
                    attemptId = (string?)null,
                    parentAttemptId = (string?)null,
                    redirectDepth = 0,
                    trigger = (string?)null,
                    from = (string?)null,
                    to = (string?)null,
                    success = (bool?)null,
                    outcome = (string?)null,
                    decision = (string?)null,
                    failureKind = (string?)null,
                    errorType = (string?)null,
                    stage = (string?)null,
                    loadMode = (string?)null,
                    reuse = (string?)null,
                    timeout = false,
                    back = false,
                    durationMs = 0L,
                    timestampUtc = (DateTime?)null
                };
            }

            return new
            {
                hasValue = true,
                runtimeId = outcome.RuntimeId,
                requestId = outcome.RequestId,
                attemptId = outcome.AttemptId,
                parentAttemptId = outcome.ParentAttemptId,
                redirectDepth = outcome.RedirectDepth,
                trigger = outcome.Trigger,
                from = outcome.From,
                to = outcome.To,
                success = outcome.Success,
                outcome = outcome.Outcome,
                decision = outcome.Decision,
                failureKind = outcome.FailureKind,
                errorType = outcome.ErrorType,
                stage = outcome.Stage,
                loadMode = outcome.LoadMode,
                reuse = outcome.Reuse,
                timeout = outcome.Timeout,
                back = outcome.Back,
                durationMs = outcome.DurationMilliseconds,
                timestampUtc = (DateTime?)outcome.TimestampUtc
            };
        }

        private void SetCurrent(
            string? type,
            string? name,
            DateTime timestampUtc)
        {
            _currentType = type;
            _currentName = name ?? (type == null ? null : ShortName(type));
            _currentChangedUtc = timestampUtc;
            if (type != null || name != null)
                _shellBlankActive = false;
        }

        private bool IsBlankLocked()
            => _attachedCount == 0 &&
               _visibleCount == 0;

        private string LogicalName(string type)
        {
            string? name;
            return _registryNamesByType.TryGetValue(type, out name)
                ? name
                : ShortName(type);
        }

        private static string ShortName(string value)
        {
            var dot = value.LastIndexOf('.');
            var plus = value.LastIndexOf('+');
            var separator = Math.Max(dot, plus);
            return separator >= 0 && separator + 1 < value.Length
                ? value.Substring(separator + 1)
                : value;
        }

        private void Record(
            IEnumerable<PendingOperation> operations)
        {
            foreach (var operation in operations)
                Record(operation.Name, operation.Payload);
        }

        private void Record(string operation, object payload)
        {
            lock (_recordSync)
            {
                if (Volatile.Read(ref _disposed) != 0)
                    return;

                _debug.Record(Module, operation, () => payload);
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            // Drain a handler that already built its operation list before the
            // disposed flag changed. Record() takes the same lock and rechecks the
            // flag, so no operation can be appended after Dispose returns.
            lock (_recordSync)
            {
            }

            Unwire();
            DisposeRegistrations();

            lock (_sync)
            {
                _requests.Clear();
                _attempts.Clear();
                _pages.Clear();
                _background.Clear();
                _surfaces.Clear();
                _timedOutRequests.Clear();
            }
        }

        private void DisposeRegistrations()
        {
            for (var i = _registrations.Count - 1; i >= 0; i--)
            {
                try { _registrations[i].Dispose(); }
                catch
                {
                    // Continue releasing remaining registrations. Diagnostics
                    // teardown must never prevent application shutdown.
                }
            }

            _registrations.Clear();
        }

        private sealed class PendingOperation
        {
            public PendingOperation(string name, object payload)
            {
                Name = name;
                Payload = payload;
            }

            public string Name { get; }
            public object Payload { get; }
        }

        private sealed class RequestMirror
        {
            public RequestMirror(NavigationTraceEvent e)
            {
                RuntimeId = e.RuntimeId;
                RequestId = e.RequestId ?? string.Empty;
                Trigger = e.Trigger.ToString();
                From = e.FromPage;
                Target = e.TargetPage;
                RequestedLoadMode = e.RequestedLoadMode;
                Back = e.IsBackNavigation;
                Stage = e.Stage.ToString();
                TimestampUtc = e.TimestampUtc;
                ElapsedMilliseconds = e.ElapsedMilliseconds;
            }

            public string RuntimeId { get; }
            public string RequestId { get; }
            public string Trigger { get; }
            public string? From { get; }
            public string? Target { get; }
            public string? RequestedLoadMode { get; }
            public bool Back { get; }
            public string Stage { get; private set; }
            public DateTime TimestampUtc { get; }
            public long ElapsedMilliseconds { get; private set; }

            public void Update(NavigationTraceEvent e)
            {
                Stage = e.Stage.ToString();
                ElapsedMilliseconds = e.ElapsedMilliseconds;
            }

            public object Snapshot()
                => new
                {
                    runtimeId = RuntimeId,
                    requestId = RequestId,
                    trigger = Trigger,
                    from = From,
                    target = Target,
                    requestedLoadMode = RequestedLoadMode,
                    back = Back,
                    stage = Stage,
                    durationMs = ElapsedMilliseconds,
                    timestampUtc = TimestampUtc
                };
        }

        private sealed class AttemptMirror
        {
            public AttemptMirror(NavigationTraceEvent e)
            {
                RuntimeId = e.RuntimeId;
                RequestId = e.RequestId ?? string.Empty;
                AttemptId = e.AttemptId ?? string.Empty;
                ParentAttemptId = e.ParentAttemptId;
                RedirectDepth = e.RedirectDepth;
                Trigger = e.Trigger.ToString();
                From = e.FromPage;
                Target = e.TargetPage;
                Stage = e.Stage.ToString();
                TimestampUtc = e.TimestampUtc;
                ElapsedMilliseconds = e.ElapsedMilliseconds;
            }

            public string RuntimeId { get; }
            public string RequestId { get; }
            public string AttemptId { get; }
            public string? ParentAttemptId { get; }
            public int RedirectDepth { get; }
            public string Trigger { get; }
            public string? From { get; }
            public string? Target { get; private set; }
            public string Stage { get; private set; }
            public DateTime TimestampUtc { get; }
            public long ElapsedMilliseconds { get; private set; }
            public bool NavigatingRecorded { get; set; }

            public void Update(NavigationTraceEvent e)
            {
                Target = e.TargetPage ?? Target;
                Stage = e.Stage.ToString();
                ElapsedMilliseconds = e.ElapsedMilliseconds;
            }

            public object Snapshot()
                => new
                {
                    runtimeId = RuntimeId,
                    requestId = RequestId,
                    attemptId = AttemptId,
                    parentAttemptId = ParentAttemptId,
                    redirectDepth = RedirectDepth,
                    trigger = Trigger,
                    from = From,
                    target = Target,
                    stage = Stage,
                    durationMs = ElapsedMilliseconds,
                    timestampUtc = TimestampUtc
                };
        }

        private sealed class PageMirror
        {
            public PageMirror(string page)
            {
                Page = page;
            }

            public string Page { get; }
            public string? Decision { get; private set; }
            public bool Attached { get; private set; }
            public bool Visible { get; private set; }
            public bool Disposed { get; private set; }
            public DateTime? UpdatedUtc { get; private set; }

            public void Update(NavigationTraceEvent e)
            {
                Decision = e.Decision;
                UpdatedUtc = e.TimestampUtc;

                var decision = e.Decision ?? string.Empty;
                var denotesLiveInstance =
                    decision.IndexOf(
                        "Created",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    decision.IndexOf(
                        "CacheHit",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    decision.IndexOf(
                        "Attached",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    decision.IndexOf(
                        "Visible",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    decision.IndexOf(
                        "CurrentChanged",
                        StringComparison.OrdinalIgnoreCase) >= 0;

                if (denotesLiveInstance || e.IsDisposed == false)
                    Disposed = false;

                if (decision.IndexOf(
                    "Attached",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Attached = true;
                }
                if (decision.IndexOf(
                    "Visible",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                    decision.IndexOf(
                        "CurrentChanged",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Visible = true;
                }
                if (decision.IndexOf(
                    "Hidden",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Visible = false;
                }
                if (decision.IndexOf(
                    "Detached",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Attached = false;
                    Visible = false;
                }
                if (e.IsDisposed == true ||
                    (!e.IsDisposed.HasValue &&
                     decision.IndexOf(
                        "Disposed",
                        StringComparison.OrdinalIgnoreCase) >= 0 &&
                     decision.IndexOf(
                        "Failed",
                        StringComparison.OrdinalIgnoreCase) < 0))
                {
                    Disposed = true;
                    Attached = false;
                    Visible = false;
                }
            }

            public object Snapshot()
                => new
                {
                    page = Page,
                    decision = Decision,
                    attached = Attached,
                    visible = Visible,
                    disposed = Disposed,
                    updatedUtc = UpdatedUtc
                };
        }

        private sealed class BackgroundMirror
        {
            public BackgroundMirror(NavigationTraceEvent e)
            {
                OperationId = e.BackgroundOperationId ?? string.Empty;
                RequestId = e.RequestId;
                AttemptId = e.AttemptId;
                TargetPage = e.TargetPage;
                TimestampUtc = e.TimestampUtc;
            }

            public string OperationId { get; }
            public string? RequestId { get; }
            public string? AttemptId { get; }
            public string? TargetPage { get; }
            public DateTime TimestampUtc { get; }

            public object Snapshot()
                => new
                {
                    operationId = OperationId,
                    requestId = RequestId,
                    attemptId = AttemptId,
                    page = TargetPage,
                    timestampUtc = TimestampUtc
                };
        }

        private sealed class SurfaceMirror
        {
            public SurfaceMirror(NavigationTraceEvent e)
            {
                SurfaceId = e.SurfaceId ?? string.Empty;
                Kind = e.SurfaceKind;
                Type = e.TargetPage;
                Depth = e.SurfaceDepth ?? 0;
                Status = e.Kind == NavigationTraceKind.SurfaceOpened
                    ? "Opened"
                    : "Opening";
                OpeningUtc = e.TimestampUtc;
                if (e.Kind == NavigationTraceKind.SurfaceOpened)
                    OpenedUtc = e.TimestampUtc;
            }

            public string SurfaceId { get; }
            public string? Kind { get; }
            public string? Type { get; }
            public int Depth { get; }
            public string Status { get; private set; }
            public DateTime OpeningUtc { get; }
            public DateTime? OpenedUtc { get; private set; }

            public void Opened(NavigationTraceEvent e)
            {
                Status = "Opened";
                OpenedUtc = e.TimestampUtc;
            }

            public object Snapshot()
                => new
                {
                    surfaceId = SurfaceId,
                    kind = Kind,
                    type = Type,
                    depth = Depth,
                    status = Status,
                    openingUtc = OpeningUtc,
                    openedUtc = OpenedUtc
                };
        }

        private sealed class SurfaceTerminalMirror
        {
            public string SurfaceId { get; private set; } = string.Empty;
            public string? Kind { get; private set; }
            public string? Type { get; private set; }
            public int Depth { get; private set; }
            public string Terminal { get; private set; } = string.Empty;
            public string? CloseReason { get; private set; }
            public string? ErrorType { get; private set; }
            public bool? Success { get; private set; }
            public DateTime? OpenedUtc { get; private set; }
            public DateTime TimestampUtc { get; private set; }
            public long DurationMilliseconds { get; private set; }

            public static SurfaceTerminalMirror FromTrace(
                NavigationTraceEvent e,
                SurfaceMirror? surface)
                => new SurfaceTerminalMirror
                {
                    SurfaceId = e.SurfaceId ?? surface?.SurfaceId ?? string.Empty,
                    Kind = e.SurfaceKind ?? surface?.Kind,
                    Type = e.TargetPage ?? surface?.Type,
                    Depth = e.SurfaceDepth ?? surface?.Depth ?? 0,
                    Terminal = e.Kind.ToString(),
                    CloseReason = e.CloseReason,
                    ErrorType = e.ErrorType,
                    Success = e.Success,
                    OpenedUtc = surface?.OpenedUtc,
                    TimestampUtc = e.TimestampUtc,
                    DurationMilliseconds = e.ElapsedMilliseconds
                };

            public object Snapshot()
                => new
                {
                    surfaceId = SurfaceId,
                    kind = Kind,
                    type = Type,
                    depth = Depth,
                    terminal = Terminal,
                    closeReason = CloseReason,
                    errorType = ErrorType,
                    success = Success,
                    openedUtc = OpenedUtc,
                    timestampUtc = TimestampUtc,
                    durationMs = DurationMilliseconds
                };
        }

        private sealed class NavigationOutcomeMirror
        {
            public string? RuntimeId { get; private set; }
            public string? RequestId { get; private set; }
            public string? AttemptId { get; private set; }
            public string? ParentAttemptId { get; private set; }
            public int RedirectDepth { get; private set; }
            public string? Trigger { get; private set; }
            public string? From { get; private set; }
            public string? To { get; private set; }
            public bool? Success { get; private set; }
            public string? Outcome { get; private set; }
            public string? Decision { get; private set; }
            public string? FailureKind { get; private set; }
            public string? ErrorType { get; private set; }
            public string? Stage { get; private set; }
            public string? LoadMode { get; private set; }
            public string? Reuse { get; private set; }
            public bool Timeout { get; private set; }
            public bool Back { get; private set; }
            public long DurationMilliseconds { get; private set; }
            public DateTime TimestampUtc { get; private set; }

            public static NavigationOutcomeMirror FromTrace(
                NavigationTraceEvent e)
                => new NavigationOutcomeMirror
                {
                    RuntimeId = e.RuntimeId,
                    RequestId = e.RequestId,
                    AttemptId = e.AttemptId,
                    ParentAttemptId = e.ParentAttemptId,
                    RedirectDepth = e.RedirectDepth,
                    Trigger = e.Trigger.ToString(),
                    From = e.FromPage,
                    To = e.TargetPage,
                    Success = e.Success,
                    Outcome = e.Outcome.ToString(),
                    Decision = e.Decision,
                    FailureKind = e.FailureKind,
                    ErrorType = e.ErrorType,
                    Stage = e.PreviousStage.ToString(),
                    LoadMode = e.EffectiveLoadMode ?? e.RequestedLoadMode,
                    Reuse = e.ReusePolicy,
                    Timeout = string.Equals(
                        e.Decision,
                        "GuardTimeout",
                        StringComparison.Ordinal),
                    Back = e.IsBackNavigation,
                    DurationMilliseconds = e.ElapsedMilliseconds,
                    TimestampUtc = e.TimestampUtc
                };

            public static NavigationOutcomeMirror FromEntry(
                PageLogEntry entry)
                => new NavigationOutcomeMirror
                {
                    RuntimeId = entry.RuntimeId,
                    RequestId = entry.RequestId,
                    AttemptId = entry.AttemptId,
                    ParentAttemptId = entry.ParentAttemptId,
                    RedirectDepth = entry.RedirectDepth,
                    Trigger = entry.Trigger,
                    From = entry.FromPageName,
                    To = entry.ToPageName,
                    Success = entry.Success,
                    Outcome = entry.Success ? "Succeeded" : "Failed",
                    Decision = entry.Success ? "Navigated" : "NavigationFailed",
                    FailureKind = entry.FailureKind.ToString(),
                    ErrorType = null,
                    Stage = entry.FailureKind.ToString(),
                    LoadMode = entry.LoadMode.ToString(),
                    Reuse = entry.ReusePolicy.ToString(),
                    Timeout = entry.IsTimeout,
                    Back = entry.IsBackNavigation,
                    DurationMilliseconds = entry.DurationMilliseconds,
                    TimestampUtc = entry.TimestampUtc
                };

            public static NavigationOutcomeMirror FromGuard(
                GuardDeniedEvent e)
                => new NavigationOutcomeMirror
                {
                    RuntimeId = e.RuntimeId,
                    RequestId = e.RequestId,
                    AttemptId = e.AttemptId,
                    ParentAttemptId = e.ParentAttemptId,
                    RedirectDepth = e.RedirectDepth,
                    Trigger = e.Trigger,
                    From = e.FromPage?.GetType().FullName,
                    To = e.TargetPage?.FullName,
                    Success = false,
                    Outcome = "Denied",
                    Decision = e.RedirectPage == null
                        ? "GuardDenied"
                        : "GuardRedirect",
                    FailureKind = null,
                    ErrorType = null,
                    Stage = null,
                    LoadMode = null,
                    Reuse = null,
                    Timeout = false,
                    Back = false,
                    DurationMilliseconds = e.DurationMilliseconds,
                    TimestampUtc = e.TimestampUtc
                };

            public void MarkTimeout() => Timeout = true;
        }
    }
}
