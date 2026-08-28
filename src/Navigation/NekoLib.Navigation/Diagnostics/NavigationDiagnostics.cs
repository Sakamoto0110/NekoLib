// FILE: NekoLib.Navigation/Diagnostics/NavigationDiagnostics.cs
#nullable enable
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Runtime;
using System;

namespace NekoLib.Navigation.Diagnostics
{
    /// <summary>
    /// Emits public navigation outcomes and the internal scalar runtime trace.
    /// Diagnostics are subscriber-safe and never alter navigation control flow.
    /// </summary>
    public sealed class NavigationDiagnostics
    {
        private readonly NavigationEventHub _hub;
        private readonly INavigationDiagnosticsSink? _sink;
        internal string RuntimeId { get; } = Guid.NewGuid().ToString("N");

        internal NavigationDiagnostics(
            NavigationEventHub hub,
            INavigationDiagnosticsSink? sink = null)
        {
            _hub = hub ?? throw new ArgumentNullException(nameof(hub));
            _sink = sink;
        }

        /// <summary>Gets the subscriber-safe public outcome event hub for this context.</summary>
        public NavigationEventHub Hub => _hub;
        internal bool IsTracingEnabled => _sink != null || _hub.HasAnySubscribers;
        internal bool TraceEventsEnabled => _hub.HasTraceSubscribers;

        internal NavigationTraceScope? StartRequest(
            string runtimeId,
            IPageView? from,
            Type? target,
            string requestedTargetName,
            NavigationArgs args,
            NavigationTraceTrigger trigger)
        {
            if (!IsTracingEnabled)
                return null;

            var startedUtc = DateTime.UtcNow;
            var scope = new NavigationTraceScope(
                this,
                runtimeId,
                trigger,
                from?.GetType().FullName ?? from?.GetType().Name,
                requestedTargetName,
                (args ?? NavigationArgs.Empty).LoadMode.ToString(),
                args?.IsBackNavigation ?? false,
                startedUtc);

            scope.EmitStarted();
            _hub.PublishStarted(new NavigationStartedEvent(
                from,
                target,
                requestedTargetName,
                args,
                startedUtc,
                runtimeId,
                scope.RequestId,
                trigger));

            return scope;
        }

        /// <summary>
        /// Legacy start helper retained for the hub tests and internal callers that
        /// do not participate in correlated runtime tracing.
        /// </summary>
        internal void EmitStarted(IPageView? from, Type target, NavigationArgs? args)
            => _hub.PublishStarted(from, target, args);

        internal void EmitTrace(NavigationTraceEvent e)
        {
            if (_hub.HasTraceSubscribers)
                _hub.PublishTrace(e);
        }

        internal void EmitRuntime(
            string runtimeId,
            NavigationTraceStage stage,
            string decision,
            bool? success = null,
            string? errorType = null,
            string? currentPage = null,
            int? queueDepth = null,
            int? attachedCount = null,
            int? visibleCount = null,
            int? strongCacheCount = null,
            int? weakCacheCount = null,
            int? backgroundLoadCount = null,
            int? backHistoryCount = null,
            int? forwardHistoryCount = null,
            long elapsedMilliseconds = 0)
        {
            if (!_hub.HasTraceSubscribers)
                return;

            EmitTrace(new NavigationTraceEvent(
                NavigationTraceKind.Runtime,
                runtimeId,
                stage: stage,
                outcome: success == true
                    ? NavigationTraceOutcome.Succeeded
                    : success == false
                        ? NavigationTraceOutcome.Failed
                        : NavigationTraceOutcome.None,
                trigger: NavigationTraceTrigger.Runtime,
                targetPage: currentPage,
                decision: decision,
                errorType: errorType,
                success: success,
                queueDepth: queueDepth,
                attachedCount: attachedCount,
                visibleCount: visibleCount,
                strongCacheCount: strongCacheCount,
                weakCacheCount: weakCacheCount,
                backgroundLoadCount: backgroundLoadCount,
                backHistoryCount: backHistoryCount,
                forwardHistoryCount: forwardHistoryCount,
                elapsedMilliseconds: elapsedMilliseconds));
        }

        internal void EmitNavigation(PageLogEntry entry)
        {
            if (entry is null)
                return;

            try { _sink?.OnNavigation(entry); }
            catch { /* diagnostics never break navigation */ }

            _hub.Publish(entry);
        }

        internal void EmitSuccess(
            IPageView? from,
            IPageView? to,
            NavigationArgs args,
            PageDescriptor? desc = null)
            => EmitSuccess(from, to, args, desc, null, null);

        internal void EmitSuccess(
            IPageView? from,
            IPageView? to,
            NavigationArgs args,
            PageDescriptor? desc,
            PageDescriptor? fromDesc,
            NavigationAttemptTraceScope? trace)
        {
            var targetType = to?.GetType() ?? desc?.PageType;
            if (targetType is null)
                return;

            EmitNavigation(new PageLogEntry(
                from?.GetType(),
                fromDesc?.Name ?? PageName(from),
                targetType,
                desc?.Name ?? PageName(to) ?? targetType.Name,
                success: true,
                navigationLoadMode: desc?.LoadMode ?? args?.LoadMode ?? default,
                reusePolicy: desc?.ReusePolicy ?? default,
                failureKind: NavigationFailureKind.None,
                isTimeout: false,
                isBackNavigation: args?.IsBackNavigation ?? false,
                error: null,
                trace: trace));
        }

        internal void EmitFailure(
            IPageView? from,
            IPageView? to,
            NavigationArgs args,
            NavigationFailureKind kind = NavigationFailureKind.None,
            string? error = null,
            PageDescriptor? desc = null)
            => EmitFailure(
                from,
                to,
                args,
                kind,
                error,
                desc,
                null,
                null,
                null);

        internal void EmitFailure(
            IPageView? from,
            IPageView? to,
            NavigationArgs args,
            NavigationFailureKind kind,
            string? error,
            PageDescriptor? desc,
            Type? requestedType,
            PageDescriptor? fromDesc,
            NavigationAttemptTraceScope? trace)
        {
            var targetType = to?.GetType() ?? desc?.PageType ?? requestedType;
            if (targetType is null)
                return;

            EmitNavigation(new PageLogEntry(
                from?.GetType(),
                fromDesc?.Name ?? PageName(from),
                targetType,
                desc?.Name ?? PageName(to) ?? targetType.Name,
                success: false,
                navigationLoadMode: desc?.LoadMode ?? args?.LoadMode ?? default,
                reusePolicy: desc?.ReusePolicy ?? default,
                failureKind: kind,
                isTimeout: false,
                isBackNavigation: args?.IsBackNavigation ?? false,
                error: error,
                trace: trace));
        }

        internal void EmitGuardDenied(
            IPageView? from,
            Type? target,
            Type? redirect,
            string? reason)
            => EmitGuardDenied(from, target, redirect, reason, null);

        internal void EmitGuardDenied(
            IPageView? from,
            Type? target,
            Type? redirect,
            string? reason,
            NavigationAttemptTraceScope? trace)
        {
            var e = new GuardDeniedEvent(
                from,
                target,
                redirect,
                reason,
                DateTime.UtcNow,
                trace);

            try { _sink?.OnGuardDenied(e); }
            catch { /* diagnostics never break navigation */ }

            _hub.Publish(e);
        }

        private static string? PageName(IPageView? page)
            => page is null
                ? null
                : string.IsNullOrEmpty(page.Name)
                    ? page.GetType().Name
                    : page.Name;
    }
}
