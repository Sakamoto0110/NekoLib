#nullable enable
using System;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;

namespace NekoLib.Navigation.Diagnostics
{
    /// <summary>
    /// Compatibility signal for the beginning of a navigation request. It is
    /// emitted at API entry, before UI dispatch and navigation-gate waiting.
    /// New diagnostics should use the scalar <see cref="NavigationTraceEvent"/>
    /// stream; this event retains the page/args shape used by the existing bridge.
    /// </summary>
    internal sealed class NavigationStartedEvent
    {
        public IPageView? FromPage { get; }
        public Type? TargetPage { get; }
        public string RequestedTargetName { get; }
        public NavigationArgs? Args { get; }
        public DateTime TimestampUtc { get; }
        public string? RuntimeId { get; }
        public string? RequestId { get; }
        public NavigationTraceTrigger Trigger { get; }

        public NavigationStartedEvent(
            IPageView? fromPage,
            Type? targetPage,
            NavigationArgs? args)
            : this(
                fromPage,
                targetPage,
                targetPage?.FullName ?? "<unknown>",
                args,
                DateTime.UtcNow,
                null,
                null,
                NavigationTraceTrigger.Navigate)
        {
        }

        internal NavigationStartedEvent(
            IPageView? fromPage,
            Type? targetPage,
            string requestedTargetName,
            NavigationArgs? args,
            DateTime timestampUtc,
            string? runtimeId,
            string? requestId,
            NavigationTraceTrigger trigger)
        {
            FromPage = fromPage;
            TargetPage = targetPage;
            RequestedTargetName = requestedTargetName ??
                throw new ArgumentNullException(nameof(requestedTargetName));
            Args = args;
            TimestampUtc = timestampUtc;
            RuntimeId = runtimeId;
            RequestId = requestId;
            Trigger = trigger;
        }
    }
}
