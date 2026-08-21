#nullable enable
using System;
using System.Diagnostics;
using System.Threading;
using NekoLib.Navigation.Metadata;

namespace NekoLib.Navigation.Diagnostics
{
    /// <summary>
    /// Correlates one API request across UI dispatch, gate wait and one or more
    /// target attempts. Stopwatch supplies monotonic durations; DateTime is used
    /// only as the wall-clock timestamp exposed to consumers.
    /// </summary>
    internal sealed class NavigationTraceScope
    {
        private readonly NavigationDiagnostics _diagnostics;
        private readonly Stopwatch _watch = Stopwatch.StartNew();
        private long _stageStartedTicks;
        private NavigationTraceStage _stage = NavigationTraceStage.Requested;
        private int _terminal;
        private string? _failureKind;
        private string? _errorType;
        private NavigationAttemptTraceScope? _lastTerminalAttempt;

        public string RuntimeId { get; }
        public string RequestId { get; }
        public NavigationTraceTrigger Trigger { get; }
        public string? FromPage { get; private set; }
        public string TargetPage { get; }
        public string RequestedLoadMode { get; }
        public bool IsBackNavigation { get; }
        public DateTime StartedUtc { get; }
        public bool IsCompleted => Volatile.Read(ref _terminal) != 0;
        public long ElapsedMilliseconds => _watch.ElapsedMilliseconds;

        internal NavigationTraceScope(
            NavigationDiagnostics diagnostics,
            string runtimeId,
            NavigationTraceTrigger trigger,
            string? fromPage,
            string targetPage,
            string requestedLoadMode,
            bool isBackNavigation,
            DateTime startedUtc)
        {
            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            RuntimeId = runtimeId ?? throw new ArgumentNullException(nameof(runtimeId));
            RequestId = NewId();
            Trigger = trigger;
            FromPage = fromPage;
            TargetPage = targetPage ?? throw new ArgumentNullException(nameof(targetPage));
            RequestedLoadMode = requestedLoadMode ??
                throw new ArgumentNullException(nameof(requestedLoadMode));
            IsBackNavigation = isBackNavigation;
            StartedUtc = startedUtc;
            _stageStartedTicks = Stopwatch.GetTimestamp();
        }

        internal void EmitStarted()
        {
            if (!_diagnostics.TraceEventsEnabled)
                return;

            _diagnostics.EmitTrace(new NavigationTraceEvent(
                NavigationTraceKind.RequestStarted,
                RuntimeId,
                stage: NavigationTraceStage.Requested,
                trigger: Trigger,
                requestId: RequestId,
                fromPage: FromPage,
                targetPage: TargetPage,
                requestedLoadMode: RequestedLoadMode,
                isBackNavigation: IsBackNavigation,
                timestampUtc: StartedUtc));
        }

        internal void SetStage(NavigationTraceStage stage, int? queueDepth = null)
        {
            var now = Stopwatch.GetTimestamp();
            var previous = _stage;
            var previousElapsed = ElapsedMillisecondsSince(_stageStartedTicks, now);
            _stage = stage;
            _stageStartedTicks = now;

            if (!_diagnostics.TraceEventsEnabled)
                return;

            _diagnostics.EmitTrace(new NavigationTraceEvent(
                NavigationTraceKind.RequestStage,
                RuntimeId,
                stage: stage,
                previousStage: previous,
                trigger: Trigger,
                requestId: RequestId,
                fromPage: FromPage,
                targetPage: TargetPage,
                requestedLoadMode: RequestedLoadMode,
                isBackNavigation: IsBackNavigation,
                queueDepth: queueDepth,
                elapsedMilliseconds: _watch.ElapsedMilliseconds,
                stageElapsedMilliseconds: previousElapsed));
        }

        internal NavigationAttemptTraceScope StartAttempt(
            string? fromPage,
            string targetPage,
            int redirectDepth,
            string? parentAttemptId)
        {
            var trigger = redirectDepth == 0
                ? Trigger
                : NavigationTraceTrigger.Redirect;

            return new NavigationAttemptTraceScope(
                _diagnostics,
                this,
                trigger,
                fromPage,
                targetPage,
                redirectDepth,
                parentAttemptId);
        }

        internal void Complete(
            NavigationTraceOutcome outcome,
            string? decision = null,
            string? failureKind = null,
            string? errorType = null,
            string? targetPage = null)
        {
            if (Interlocked.Exchange(ref _terminal, 1) != 0)
                return;

            var now = Stopwatch.GetTimestamp();
            var previous = _stage;
            var stageElapsed = ElapsedMillisecondsSince(_stageStartedTicks, now);
            _stage = NavigationTraceStage.Completed;
            _watch.Stop();

            if (!_diagnostics.TraceEventsEnabled)
                return;

            var terminalAttempt = _lastTerminalAttempt;
            _diagnostics.EmitTrace(new NavigationTraceEvent(
                NavigationTraceKind.RequestCompleted,
                RuntimeId,
                stage: NavigationTraceStage.Completed,
                previousStage: previous,
                outcome: outcome,
                trigger: Trigger,
                requestId: RequestId,
                attemptId: terminalAttempt?.AttemptId,
                parentAttemptId: terminalAttempt?.ParentAttemptId,
                redirectDepth: terminalAttempt?.RedirectDepth ?? 0,
                fromPage: terminalAttempt?.FromPage ?? FromPage,
                targetPage: terminalAttempt?.TargetPage ??
                    targetPage ??
                    TargetPage,
                requestedLoadMode: RequestedLoadMode,
                effectiveLoadMode: terminalAttempt?.EffectiveLoadMode,
                reusePolicy: terminalAttempt?.ReusePolicy,
                decision: decision,
                failureKind: failureKind ?? _failureKind,
                errorType: errorType ?? _errorType,
                success: outcome == NavigationTraceOutcome.Succeeded ||
                    outcome == NavigationTraceOutcome.Redirected,
                isBackNavigation: IsBackNavigation,
                elapsedMilliseconds: _watch.ElapsedMilliseconds,
                stageElapsedMilliseconds: stageElapsed));
        }

        internal void SetTerminalAttempt(NavigationAttemptTraceScope attempt)
        {
            if (attempt == null)
                return;

            // Redirect attempts may close on either side of a nested call as the
            // implementation evolves. Depth, rather than completion order, makes
            // the deepest completed attempt the real terminal target.
            var current = _lastTerminalAttempt;
            if (current == null ||
                attempt.RedirectDepth >= current.RedirectDepth)
            {
                _lastTerminalAttempt = attempt;
            }
        }

        internal void SetFailure(string? failureKind, string? errorType)
        {
            _failureKind = failureKind;
            _errorType = errorType;
        }

        internal void EmitBackground(
            NavigationTraceKind kind,
            NavigationAttemptTraceScope attempt,
            string operationId,
            string targetPage,
            long elapsedMilliseconds,
            string? decision = null,
            string? errorType = null,
            int? backgroundLoadCount = null)
        {
            if (!_diagnostics.TraceEventsEnabled)
                return;

            _diagnostics.EmitTrace(new NavigationTraceEvent(
                kind,
                RuntimeId,
                stage: NavigationTraceStage.LoadAfterShow,
                outcome: kind == NavigationTraceKind.BackgroundLoadCompleted
                    ? NavigationTraceOutcome.Succeeded
                    : kind == NavigationTraceKind.BackgroundLoadDiscarded
                        ? NavigationTraceOutcome.Discarded
                        : kind == NavigationTraceKind.BackgroundLoadFailed
                            ? NavigationTraceOutcome.Failed
                            : NavigationTraceOutcome.None,
                trigger: attempt.Trigger,
                requestId: RequestId,
                attemptId: attempt.AttemptId,
                parentAttemptId: attempt.ParentAttemptId,
                backgroundOperationId: operationId,
                redirectDepth: attempt.RedirectDepth,
                fromPage: attempt.FromPage,
                targetPage: targetPage,
                requestedLoadMode: RequestedLoadMode,
                effectiveLoadMode: attempt.EffectiveLoadMode,
                reusePolicy: attempt.ReusePolicy,
                decision: decision,
                errorType: errorType,
                success: kind == NavigationTraceKind.BackgroundLoadCompleted
                    ? true
                    : kind == NavigationTraceKind.BackgroundLoadDiscarded
                        ? (bool?)null
                        : kind == NavigationTraceKind.BackgroundLoadFailed
                            ? false
                            : null,
                isBackNavigation: IsBackNavigation,
                backgroundLoadCount: backgroundLoadCount,
                elapsedMilliseconds: elapsedMilliseconds));
        }

        internal static string NewId() => Guid.NewGuid().ToString("N");

        internal static long ElapsedMillisecondsSince(long startedTicks, long nowTicks)
        {
            var ticks = nowTicks - startedTicks;
            if (ticks <= 0)
                return 0;

            return (long)(ticks * 1000d / Stopwatch.Frequency);
        }
    }

    internal sealed class NavigationAttemptTraceScope
    {
        private readonly NavigationDiagnostics _diagnostics;
        private readonly NavigationTraceScope _request;
        private readonly Stopwatch _watch = Stopwatch.StartNew();
        private long _stageStartedTicks;
        private NavigationTraceStage _stage = NavigationTraceStage.RegistryLookup;
        private int _terminal;

        public string RuntimeId => _request.RuntimeId;
        public string RequestId => _request.RequestId;
        public string AttemptId { get; }
        public string? ParentAttemptId { get; }
        public int RedirectDepth { get; }
        public NavigationTraceTrigger Trigger { get; }
        public string? FromPage { get; private set; }
        public string TargetPage { get; private set; }
        public bool IsBackNavigation => _request.IsBackNavigation;
        public DateTime RequestStartedUtc => _request.StartedUtc;
        public bool IsCompleted => Volatile.Read(ref _terminal) != 0;
        public long ElapsedMilliseconds => _watch.ElapsedMilliseconds;

        public string? EffectiveLoadMode { get; private set; }
        public string? ReusePolicy { get; private set; }

        internal NavigationAttemptTraceScope(
            NavigationDiagnostics diagnostics,
            NavigationTraceScope request,
            NavigationTraceTrigger trigger,
            string? fromPage,
            string targetPage,
            int redirectDepth,
            string? parentAttemptId)
        {
            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            _request = request ?? throw new ArgumentNullException(nameof(request));
            Trigger = trigger;
            FromPage = fromPage;
            TargetPage = targetPage ?? throw new ArgumentNullException(nameof(targetPage));
            RedirectDepth = redirectDepth;
            ParentAttemptId = parentAttemptId;
            AttemptId = NavigationTraceScope.NewId();
            _stageStartedTicks = Stopwatch.GetTimestamp();

            if (!_diagnostics.TraceEventsEnabled)
                return;

            _diagnostics.EmitTrace(new NavigationTraceEvent(
                NavigationTraceKind.AttemptStarted,
                RuntimeId,
                stage: NavigationTraceStage.RegistryLookup,
                trigger: Trigger,
                requestId: RequestId,
                attemptId: AttemptId,
                parentAttemptId: ParentAttemptId,
                redirectDepth: RedirectDepth,
                fromPage: FromPage,
                targetPage: TargetPage,
                requestedLoadMode: _request.RequestedLoadMode,
                isBackNavigation: IsBackNavigation));
        }

        internal void SetDescriptor(PageDescriptor descriptor)
        {
            if (descriptor is null)
                return;

            TargetPage = descriptor.Name;
            EffectiveLoadMode = descriptor.LoadMode.ToString();
            ReusePolicy = descriptor.ReusePolicy.ToString();
        }

        internal void SetFromPageName(string? fromPage)
        {
            if (!string.IsNullOrEmpty(fromPage))
                FromPage = fromPage;
        }

        internal void SetStage(NavigationTraceStage stage)
        {
            var now = Stopwatch.GetTimestamp();
            var previous = _stage;
            var previousElapsed = NavigationTraceScope.ElapsedMillisecondsSince(
                _stageStartedTicks,
                now);
            _stage = stage;
            _stageStartedTicks = now;

            if (!_diagnostics.TraceEventsEnabled)
                return;

            _diagnostics.EmitTrace(new NavigationTraceEvent(
                NavigationTraceKind.AttemptStage,
                RuntimeId,
                stage: stage,
                previousStage: previous,
                trigger: Trigger,
                requestId: RequestId,
                attemptId: AttemptId,
                parentAttemptId: ParentAttemptId,
                redirectDepth: RedirectDepth,
                fromPage: FromPage,
                targetPage: TargetPage,
                requestedLoadMode: _request.RequestedLoadMode,
                effectiveLoadMode: EffectiveLoadMode,
                reusePolicy: ReusePolicy,
                isBackNavigation: IsBackNavigation,
                elapsedMilliseconds: _watch.ElapsedMilliseconds,
                stageElapsedMilliseconds: previousElapsed));
        }

        internal void EmitPage(
            string page,
            string decision,
            bool? isDisposed,
            int attachedCount,
            int visibleCount,
            int strongCacheCount,
            int weakCacheCount,
            int backgroundLoadCount,
            int backHistoryCount,
            int forwardHistoryCount)
        {
            if (!_diagnostics.TraceEventsEnabled)
                return;

            _diagnostics.EmitTrace(new NavigationTraceEvent(
                NavigationTraceKind.Page,
                RuntimeId,
                stage: _stage,
                trigger: Trigger,
                requestId: RequestId,
                attemptId: AttemptId,
                parentAttemptId: ParentAttemptId,
                redirectDepth: RedirectDepth,
                fromPage: FromPage,
                targetPage: page,
                requestedLoadMode: _request.RequestedLoadMode,
                effectiveLoadMode: EffectiveLoadMode,
                reusePolicy: ReusePolicy,
                decision: decision,
                isBackNavigation: IsBackNavigation,
                isDisposed: isDisposed,
                attachedCount: attachedCount,
                visibleCount: visibleCount,
                strongCacheCount: strongCacheCount,
                weakCacheCount: weakCacheCount,
                backgroundLoadCount: backgroundLoadCount,
                backHistoryCount: backHistoryCount,
                forwardHistoryCount: forwardHistoryCount,
                elapsedMilliseconds: _watch.ElapsedMilliseconds));
        }

        internal void Complete(
            NavigationTraceOutcome outcome,
            string? decision = null,
            string? failureKind = null,
            string? errorType = null)
        {
            if (Interlocked.Exchange(ref _terminal, 1) != 0)
                return;

            var now = Stopwatch.GetTimestamp();
            var previous = _stage;
            var stageElapsed = NavigationTraceScope.ElapsedMillisecondsSince(
                _stageStartedTicks,
                now);
            _stage = NavigationTraceStage.Completed;
            _watch.Stop();

            _request.SetTerminalAttempt(this);

            if (outcome == NavigationTraceOutcome.Failed)
                _request.SetFailure(failureKind, errorType);

            if (!_diagnostics.TraceEventsEnabled)
                return;

            _diagnostics.EmitTrace(new NavigationTraceEvent(
                NavigationTraceKind.AttemptCompleted,
                RuntimeId,
                stage: NavigationTraceStage.Completed,
                previousStage: previous,
                outcome: outcome,
                trigger: Trigger,
                requestId: RequestId,
                attemptId: AttemptId,
                parentAttemptId: ParentAttemptId,
                redirectDepth: RedirectDepth,
                fromPage: FromPage,
                targetPage: TargetPage,
                requestedLoadMode: _request.RequestedLoadMode,
                effectiveLoadMode: EffectiveLoadMode,
                reusePolicy: ReusePolicy,
                decision: decision,
                failureKind: failureKind,
                errorType: errorType,
                success: outcome == NavigationTraceOutcome.Succeeded ||
                    outcome == NavigationTraceOutcome.Redirected,
                isBackNavigation: IsBackNavigation,
                elapsedMilliseconds: _watch.ElapsedMilliseconds,
                stageElapsedMilliseconds: stageElapsed));
        }
    }
}
