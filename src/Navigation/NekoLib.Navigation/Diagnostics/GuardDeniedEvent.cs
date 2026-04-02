using NekoLib.Navigation.Contracts.Pages;
using System;

namespace NekoLib.Navigation.Diagnostics
{
    public sealed class GuardDeniedEvent
    {
        public IPageView FromPage { get; }
        public Type TargetPage { get; }
        public Type RedirectPage { get; }
        public string Reason { get; }
        public DateTime TimestampUtc { get; }

        public GuardDeniedEvent(
            IPageView fromPage,
            Type targetPage,
            Type redirectPage,
            string reason)
        {
            FromPage = fromPage;
            TargetPage = targetPage;
            RedirectPage = redirectPage;
            Reason = reason;
            TimestampUtc = DateTime.UtcNow;
        }
    }
}   