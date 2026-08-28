#nullable enable
using NekoLib.Navigation.Contracts.Pages;
using System;

namespace NekoLib.Navigation.Diagnostics
{
    /// <summary>
    /// Immutable diagnostic record emitted when a guard denies or redirects a
    /// navigation attempt. Correlation fields may be absent for legacy events.
    /// </summary>
    public sealed class GuardDeniedEvent
    {
        /// <summary>Gets the page active before the denied attempt, if any.</summary>
        public IPageView? FromPage { get; }
        /// <summary>Gets the requested page type, if it was resolved.</summary>
        public Type? TargetPage { get; }
        /// <summary>Gets the redirect target selected by the guard, if any.</summary>
        public Type? RedirectPage { get; }
        /// <summary>Gets the guard-supplied denial reason, if any.</summary>
        public string? Reason { get; }
        /// <summary>Gets the UTC time at which the denial was recorded.</summary>
        public DateTime TimestampUtc { get; }

        /// <summary>Gets the runtime correlation identifier, or <see langword="null"/> for an uncorrelated event.</summary>
        public string? RuntimeId { get; }
        /// <summary>Gets the request correlation identifier, or <see langword="null"/> for an uncorrelated event.</summary>
        public string? RequestId { get; }
        /// <summary>Gets the attempt correlation identifier, or <see langword="null"/> for an uncorrelated event.</summary>
        public string? AttemptId { get; }
        /// <summary>Gets the parent attempt identifier for a redirect child, if any.</summary>
        public string? ParentAttemptId { get; }
        /// <summary>Gets the zero-based redirect depth of the denied attempt.</summary>
        public int RedirectDepth { get; }
        /// <summary>Gets the trigger name recorded by the correlated trace, if any.</summary>
        public string? Trigger { get; }
        /// <summary>Gets elapsed monotonic milliseconds from attempt start to denial.</summary>
        public long DurationMilliseconds { get; }

        /// <summary>Internal constructor for uncorrelated runtime and test events.</summary>
        internal GuardDeniedEvent(
            IPageView? fromPage,
            Type? targetPage,
            Type? redirectPage,
            string? reason)
            : this(
                fromPage,
                targetPage,
                redirectPage,
                reason,
                DateTime.UtcNow,
                null)
        {
        }

        internal GuardDeniedEvent(
            IPageView? fromPage,
            Type? targetPage,
            Type? redirectPage,
            string? reason,
            DateTime timestampUtc,
            NavigationAttemptTraceScope? trace)
        {
            FromPage = fromPage;
            TargetPage = targetPage;
            RedirectPage = redirectPage;
            Reason = reason;
            TimestampUtc = timestampUtc;
            RuntimeId = trace?.RuntimeId;
            RequestId = trace?.RequestId;
            AttemptId = trace?.AttemptId;
            ParentAttemptId = trace?.ParentAttemptId;
            RedirectDepth = trace?.RedirectDepth ?? 0;
            Trigger = trace?.Trigger.ToString();
            DurationMilliseconds = trace?.ElapsedMilliseconds ?? 0;
        }
    }
}
