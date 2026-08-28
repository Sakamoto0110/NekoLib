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
        /// <summary>Gets the previous page type, or <see langword="null"/> when navigation started without one.</summary>
        public Type? FromPageType { get; }
        /// <summary>Gets the previous page's descriptor name, if one was available.</summary>
        public string? FromPageName { get; }
        /// <summary>Gets the resolved target page type.</summary>
        public Type ToPageType { get; }
        /// <summary>Gets the target page's descriptor name.</summary>
        public string ToPageName { get; }

        /// <summary>UTC timestamp at which the correlated request began.</summary>
        public DateTime TimestampUtc { get; }
        /// <summary>Gets the descriptor-effective page load mode.</summary>
        public NavigationLoadMode LoadMode { get; }
        /// <summary>Gets the descriptor-effective instance reuse policy.</summary>
        public PageReusePolicy ReusePolicy { get; }
        /// <summary>Gets whether the terminal outcome represents an idle-timeout navigation failure.</summary>
        public bool IsTimeout { get; }
        /// <summary>Gets whether this outcome belongs to a back-navigation request.</summary>
        public bool IsBackNavigation { get; }
        /// <summary>Gets whether navigation completed successfully.</summary>
        public bool Success { get; }
        /// <summary>Gets captured failure text, or <see langword="null"/> when none was recorded.</summary>
        public string? Error { get; }
        /// <summary>Gets the normalized failure category.</summary>
        public NavigationFailureKind FailureKind { get; }

        /// <summary>Correlation is null for entries built through the legacy constructor.</summary>
        public string? RuntimeId { get; }
        /// <summary>Gets the request correlation identifier, or <see langword="null"/> for a legacy entry.</summary>
        public string? RequestId { get; }
        /// <summary>Gets the attempt correlation identifier, or <see langword="null"/> for a legacy entry.</summary>
        public string? AttemptId { get; }
        /// <summary>Gets the parent attempt identifier for a redirect child, if any.</summary>
        public string? ParentAttemptId { get; }
        /// <summary>Gets the zero-based redirect depth.</summary>
        public int RedirectDepth { get; }
        /// <summary>Gets the request trigger name, or <see langword="null"/> for a legacy entry.</summary>
        public string? Trigger { get; }
        /// <summary>Gets elapsed monotonic milliseconds from request start to this outcome.</summary>
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

        /// <summary>Formats a compact timestamped navigation outcome for diagnostics.</summary>
        /// <returns>A single-line outcome containing direction, page names, status, load mode, and reuse policy.</returns>
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
