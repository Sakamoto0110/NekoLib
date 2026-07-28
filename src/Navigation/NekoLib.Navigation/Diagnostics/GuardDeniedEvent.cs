#nullable enable
using NekoLib.Navigation.Contracts.Pages;
using System;

namespace NekoLib.Navigation.Diagnostics
{
    public sealed class GuardDeniedEvent
    {
        public IPageView? FromPage { get; }
        public Type? TargetPage { get; }
        public Type? RedirectPage { get; }
        public string? Reason { get; }
        public DateTime TimestampUtc { get; }

        public string? RuntimeId { get; }
        public string? RequestId { get; }
        public string? AttemptId { get; }
        public string? ParentAttemptId { get; }
        public int RedirectDepth { get; }
        public string? Trigger { get; }
        public long DurationMilliseconds { get; }

        /// <summary>Existing constructor retained for compatibility.</summary>
        public GuardDeniedEvent(
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
