using System;

// FILE: NekoLib.Navigation/Diagnostics/NavigationEventHub.cs
#nullable enable
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;

namespace NekoLib.Navigation.Diagnostics
{
    /// <summary>
    /// Internal navigation event pipeline.
    /// Consumers (UI, tests, etc.) can subscribe without depending on NekoLib.Diagnostics.
    /// </summary>
    public sealed class NavigationEventHub
    {
        internal NavigationEventHub()
        {
        }

        public event Action<PageLogEntry>? NavigationLogged;
        public event Action<GuardDeniedEvent>? GuardDenied;
        internal event Action<NavigationStartedEvent>? NavigationStarted;
        internal event Action<NavigationTraceEvent>? NavigationTrace;

        internal bool HasAnySubscribers =>
            NavigationLogged != null ||
            GuardDenied != null ||
            NavigationStarted != null ||
            NavigationTrace != null;

        internal bool HasTraceSubscribers => NavigationTrace != null;

        internal void PublishStarted(
            IPageView? from,
            Type target,
            NavigationArgs? args)
            => PublishStarted(new NavigationStartedEvent(from, target, args));

        internal void PublishStarted(NavigationStartedEvent e)
        {
            var subscribers = NavigationStarted;
            if (subscribers is null) return;

            PublishToEach(subscribers, e);
        }

        internal void PublishTrace(NavigationTraceEvent e)
        {
            if (e is null) return;
            PublishToEach(NavigationTrace, e);
        }

        internal void Publish(PageLogEntry entry)
        {
            if (entry is null) return;
            PublishToEach(NavigationLogged, entry);
        }

        internal void Publish(GuardDeniedEvent e)
        {
            if (e is null) return;
            PublishToEach(GuardDenied, e);
        }

        private static void PublishToEach<T>(Action<T>? subscribers, T value)
        {
            if (subscribers is null)
                return;

            foreach (Action<T> subscriber in subscribers.GetInvocationList())
            {
                try { subscriber(value); }
                catch { /* never break navigation or later subscribers */ }
            }
        }
    }
}

