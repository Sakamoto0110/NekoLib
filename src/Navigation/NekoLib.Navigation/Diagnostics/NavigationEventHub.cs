using System;

// FILE: NekoLib.Navigation/Diagnostics/NavigationEventHub.cs
#nullable enable

namespace NekoLib.Navigation.Diagnostics
{
    /// <summary>
    /// Internal navigation event pipeline.
    /// Consumers (UI, tests, etc.) can subscribe without depending on NekoLib.Diagnostics.
    /// </summary>
    public sealed class NavigationEventHub
    {
        public event Action<PageLogEntry>? NavigationLogged;
        public event Action<GuardDeniedEvent>? GuardDenied;

        public void Publish(PageLogEntry entry)
        {
            if (entry is null) return;
            try { NavigationLogged?.Invoke(entry); }
            catch { /* never break navigation */ }
        }

        public void Publish(GuardDeniedEvent e)
        {
            if (e is null) return;
            try { GuardDenied?.Invoke(e); }
            catch { /* never break navigation */ }
        }
    }
}

