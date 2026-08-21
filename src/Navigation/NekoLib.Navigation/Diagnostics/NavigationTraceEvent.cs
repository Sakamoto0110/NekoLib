#nullable enable
using System;

namespace NekoLib.Navigation.Diagnostics
{
    /// <summary>
    /// Kind of internal runtime trace. This channel intentionally carries only
    /// scalar values so diagnostics never retain a page, payload or captured state.
    /// </summary>
    internal enum NavigationTraceKind
    {
        Runtime,
        RequestStarted,
        RequestStage,
        RequestCompleted,
        AttemptStarted,
        AttemptStage,
        AttemptCompleted,
        Page,
        BackgroundLoadStarted,
        BackgroundLoadCompleted,
        BackgroundLoadDiscarded,
        BackgroundLoadFailed,
        SurfaceOpening,
        SurfaceOpened,
        SurfaceClosed,
        SurfaceFailed,
        IdleConfigured,
        IdleInteraction,
        IdleElapsed,
        IdleNavigationFailed,
        IdleDisposed
    }

    internal enum NavigationTraceStage
    {
        None,
        Requested,
        Dispatch,
        GateWait,
        Processing,
        HistoryLookup,
        RegistryLookup,
        CycleCheck,
        GuardEvaluation,
        StateCapture,
        PageResolution,
        LoadBeforeShow,
        LeavePage,
        DetachPage,
        AttachPage,
        LoadAfterShow,
        StateRestore,
        EnterPage,
        HistoryUpdate,
        Completed,
        Reset,
        Dispose
    }

    internal enum NavigationTraceOutcome
    {
        None,
        Succeeded,
        Failed,
        Denied,
        Redirected,
        NoHistory,
        Discarded
    }

    internal enum NavigationTraceTrigger
    {
        Navigate,
        Idle,
        Back,
        Redirect,
        Runtime
    }

    /// <summary>
    /// Immutable, scalar-only trace emitted by the navigation runtime.
    /// Optional values are null when they do not apply to a given kind.
    /// </summary>
    internal sealed class NavigationTraceEvent
    {
        public NavigationTraceKind Kind { get; }
        public NavigationTraceStage Stage { get; }
        public NavigationTraceStage PreviousStage { get; }
        public NavigationTraceOutcome Outcome { get; }
        public NavigationTraceTrigger Trigger { get; }

        public string RuntimeId { get; }
        public string? RequestId { get; }
        public string? AttemptId { get; }
        public string? ParentAttemptId { get; }
        public string? BackgroundOperationId { get; }

        public int RedirectDepth { get; }
        public string? FromPage { get; }
        public string? TargetPage { get; }
        public string? RequestedLoadMode { get; }
        public string? EffectiveLoadMode { get; }
        public string? ReusePolicy { get; }
        public string? Decision { get; }
        public string? FailureKind { get; }
        public string? ErrorType { get; }
        public bool? Success { get; }
        public bool IsBackNavigation { get; }
        public bool? IsDisposed { get; }

        public int? QueueDepth { get; }
        public int? AttachedCount { get; }
        public int? VisibleCount { get; }
        public int? StrongCacheCount { get; }
        public int? WeakCacheCount { get; }
        public int? BackgroundLoadCount { get; }
        public int? BackHistoryCount { get; }
        public int? ForwardHistoryCount { get; }
        public string? SurfaceId { get; }
        public string? SurfaceKind { get; }
        public int? SurfaceDepth { get; }
        public string? CloseReason { get; }
        public int? IdleIntervalMilliseconds { get; }

        public DateTime TimestampUtc { get; }
        public long ElapsedMilliseconds { get; }
        public long StageElapsedMilliseconds { get; }

        internal NavigationTraceEvent(
            NavigationTraceKind kind,
            string runtimeId,
            NavigationTraceStage stage = NavigationTraceStage.None,
            NavigationTraceStage previousStage = NavigationTraceStage.None,
            NavigationTraceOutcome outcome = NavigationTraceOutcome.None,
            NavigationTraceTrigger trigger = NavigationTraceTrigger.Runtime,
            string? requestId = null,
            string? attemptId = null,
            string? parentAttemptId = null,
            string? backgroundOperationId = null,
            int redirectDepth = 0,
            string? fromPage = null,
            string? targetPage = null,
            string? requestedLoadMode = null,
            string? effectiveLoadMode = null,
            string? reusePolicy = null,
            string? decision = null,
            string? failureKind = null,
            string? errorType = null,
            bool? success = null,
            bool isBackNavigation = false,
            bool? isDisposed = null,
            int? queueDepth = null,
            int? attachedCount = null,
            int? visibleCount = null,
            int? strongCacheCount = null,
            int? weakCacheCount = null,
            int? backgroundLoadCount = null,
            int? backHistoryCount = null,
            int? forwardHistoryCount = null,
            string? surfaceId = null,
            string? surfaceKind = null,
            int? surfaceDepth = null,
            string? closeReason = null,
            int? idleIntervalMilliseconds = null,
            DateTime? timestampUtc = null,
            long elapsedMilliseconds = 0,
            long stageElapsedMilliseconds = 0)
        {
            Kind = kind;
            RuntimeId = runtimeId ?? throw new ArgumentNullException(nameof(runtimeId));
            Stage = stage;
            PreviousStage = previousStage;
            Outcome = outcome;
            Trigger = trigger;
            RequestId = requestId;
            AttemptId = attemptId;
            ParentAttemptId = parentAttemptId;
            BackgroundOperationId = backgroundOperationId;
            RedirectDepth = redirectDepth;
            FromPage = fromPage;
            TargetPage = targetPage;
            RequestedLoadMode = requestedLoadMode;
            EffectiveLoadMode = effectiveLoadMode;
            ReusePolicy = reusePolicy;
            Decision = decision;
            FailureKind = failureKind;
            ErrorType = errorType;
            Success = success;
            IsBackNavigation = isBackNavigation;
            IsDisposed = isDisposed;
            QueueDepth = queueDepth;
            AttachedCount = attachedCount;
            VisibleCount = visibleCount;
            StrongCacheCount = strongCacheCount;
            WeakCacheCount = weakCacheCount;
            BackgroundLoadCount = backgroundLoadCount;
            BackHistoryCount = backHistoryCount;
            ForwardHistoryCount = forwardHistoryCount;
            SurfaceId = surfaceId;
            SurfaceKind = surfaceKind;
            SurfaceDepth = surfaceDepth;
            CloseReason = closeReason;
            IdleIntervalMilliseconds = idleIntervalMilliseconds;
            TimestampUtc = timestampUtc ?? DateTime.UtcNow;
            ElapsedMilliseconds = elapsedMilliseconds;
            StageElapsedMilliseconds = stageElapsedMilliseconds;
        }
    }
}
