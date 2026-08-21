#nullable enable
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Runtime;
using System;

namespace NekoLib.Navigation.Diagnostics
{
    /// <summary>
    /// Immutable navigation outcome used for diagnostics and tracing.
    /// This is not a general-purpose logger entry.
    /// </summary>
    public sealed class PageLogEntry
    {
        public Type? FromPageType { get; }
        public string? FromPageName { get; }
        public Type ToPageType { get; }
        public string ToPageName { get; }

        /// <summary>UTC timestamp at which the correlated request began.</summary>
        public DateTime TimestampUtc { get; }
        public NavigationLoadMode LoadMode { get; }
        public PageReusePolicy ReusePolicy { get; }
        public bool IsTimeout { get; }
        public bool IsBackNavigation { get; }
        public bool Success { get; }
        public string? Error { get; }
        public NavigationFailureKind FailureKind { get; }

        /// <summary>Correlation is null for entries built through the legacy constructor.</summary>
        public string? RuntimeId { get; }
        public string? RequestId { get; }
        public string? AttemptId { get; }
        public string? ParentAttemptId { get; }
        public int RedirectDepth { get; }
        public string? Trigger { get; }
        public long DurationMilliseconds { get; }

        /// <summary>
        /// Internal constructor used by tests and runtime helpers that do not carry a
        /// correlated trace scope.
        /// </summary>
        internal PageLogEntry(
            Type? fromType,
            string? fromName,
            Type toType,
            string? toName,
            NavigationArgs args,
            bool success,
            NavigationLoadMode navigationLoadMode,
            PageReusePolicy reusePolicy,
            NavigationFailureKind failureKind = NavigationFailureKind.None,
            bool isTimeout = false,
            bool isBackNavigation = false,
            string? error = null)
            : this(
                fromType,
                fromName,
                toType,
                toName,
                success,
                navigationLoadMode,
                reusePolicy,
                failureKind,
                isTimeout,
                isBackNavigation,
                error,
                DateTime.UtcNow,
                null,
                null,
                null,
                null,
                0,
                null,
                0)
        {
        }

        internal PageLogEntry(
            Type? fromType,
            string? fromName,
            Type toType,
            string? toName,
            bool success,
            NavigationLoadMode navigationLoadMode,
            PageReusePolicy reusePolicy,
            NavigationFailureKind failureKind,
            bool isTimeout,
            bool isBackNavigation,
            string? error,
            NavigationAttemptTraceScope? trace)
            : this(
                fromType,
                fromName,
                toType,
                toName,
                success,
                navigationLoadMode,
                reusePolicy,
                failureKind,
                isTimeout,
                isBackNavigation,
                error,
                trace?.RequestStartedUtc ?? DateTime.UtcNow,
                trace?.RuntimeId,
                trace?.RequestId,
                trace?.AttemptId,
                trace?.ParentAttemptId,
                trace?.RedirectDepth ?? 0,
                trace?.Trigger.ToString(),
                trace?.ElapsedMilliseconds ?? 0)
        {
        }

        private PageLogEntry(
            Type? fromType,
            string? fromName,
            Type toType,
            string? toName,
            bool success,
            NavigationLoadMode navigationLoadMode,
            PageReusePolicy reusePolicy,
            NavigationFailureKind failureKind,
            bool isTimeout,
            bool isBackNavigation,
            string? error,
            DateTime timestampUtc,
            string? runtimeId,
            string? requestId,
            string? attemptId,
            string? parentAttemptId,
            int redirectDepth,
            string? trigger,
            long durationMilliseconds)
        {
            FromPageType = fromType;
            FromPageName = fromName;
            ToPageType = toType ?? throw new ArgumentNullException(nameof(toType));
            ToPageName = toName ?? toType.Name;
            TimestampUtc = timestampUtc;
            LoadMode = navigationLoadMode;
            ReusePolicy = reusePolicy;
            FailureKind = failureKind;
            IsTimeout = isTimeout;
            IsBackNavigation = isBackNavigation;
            Success = success;
            Error = error;
            RuntimeId = runtimeId;
            RequestId = requestId;
            AttemptId = attemptId;
            ParentAttemptId = parentAttemptId;
            RedirectDepth = redirectDepth;
            Trigger = trigger;
            DurationMilliseconds = durationMilliseconds;
        }

        public override string ToString()
        {
            var direction = IsBackNavigation ? "BACK" : "NAV";
            var status = Success ? "OK" : "FAIL";

            return $"[{TimestampUtc:HH:mm:ss}] {direction} " +
                   $"{FromPageName ?? "<null>"} -> {ToPageName} " +
                   $"({status}, {LoadMode} | {ReusePolicy})";
        }
    }
}
