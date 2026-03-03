// FILE: NekoLib.Navigation/Diagnostics/NavigationDiagnostics.cs
#nullable enable
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Runtime;
using System;
using System.Security.Cryptography;

namespace NekoLib.Navigation.Diagnostics
{
    /// <summary>
    /// Emits navigation diagnostics into:
    ///  - internal hub (NavigationEventHub)
    ///  - optional external sink (INavigationDiagnosticsSink)
    ///
    /// No global state, no static Context.
    /// </summary>
    public sealed class NavigationDiagnostics
    {
        private readonly NavigationEventHub _hub;
        private readonly INavigationDiagnosticsSink? _sink;

        public NavigationDiagnostics(NavigationEventHub hub, INavigationDiagnosticsSink? sink = null)
        {
            _hub = hub ?? throw new ArgumentNullException(nameof(hub));
            _sink = sink;
        }

        public NavigationEventHub Hub => _hub;

        public void EmitNavigation(PageLogEntry entry)
        {
            if (entry is null) return;

            try { _sink?.OnNavigation(entry); }
            catch { /* never break navigation */ }

            _hub.Publish(entry);
        }

        public void EmitSuccess(IPageView? from, IPageView? to, NavigationArgs args, PageDescriptor? desc =null)
            => EmitNavigation(new PageLogEntry(
                from?.GetType(), string.IsNullOrEmpty(from?.Name)?from?.GetType().Name:from?.Name,
                to?.GetType(), string.IsNullOrEmpty(to?.Name) ? to?.GetType().Name : to?.Name,
                args,  true,
              navigationBehavior:  desc.Presentation  ,
              navigationLoadMode:  desc.LoadMode,
                reusePolicy: desc.ReusePolicy 
                ));

        public void EmitFailure(
            IPageView? from,
            IPageView? to,
            NavigationArgs args,
            NavigationFailureKind kind = NavigationFailureKind.None,
            string? error = null, PageDescriptor? desc = null)
            => EmitNavigation(new PageLogEntry(
               from?.GetType(), string.IsNullOrEmpty(from?.Name) ? from?.GetType().Name : from?.Name,
                to?.GetType(), string.IsNullOrEmpty(to?.Name) ? to?.GetType().Name : to?.Name,
                args,
                success: false,
                failureKind: kind,
                error: error,
              navigationBehavior: desc.Presentation,
              navigationLoadMode: desc.LoadMode,
                reusePolicy: desc.ReusePolicy));

        public void EmitTimeout(IPageView? from, IPageView? to, NavigationArgs args, PageDescriptor? desc = null)
            => EmitNavigation(new PageLogEntry(
              from?.GetType(), string.IsNullOrEmpty(from?.Name) ? from?.GetType().Name : from?.Name,
                to?.GetType(), string.IsNullOrEmpty(to?.Name) ? to?.GetType().Name : to?.Name,
                args,
                success: true,
                isTimeout: true,
              navigationBehavior: desc.Presentation,
              navigationLoadMode: desc.LoadMode,
                reusePolicy: desc.ReusePolicy));

        public void EmitBack(IPageView? from, IPageView? to, NavigationArgs args, PageDescriptor? desc = null)
            => EmitNavigation(new PageLogEntry(
               from?.GetType(), string.IsNullOrEmpty(from?.Name) ? from?.GetType().Name : from?.Name,
                to?.GetType(), string.IsNullOrEmpty(to?.Name) ? to?.GetType().Name : to?.Name,
                args,
                success: true,
                isBackNavigation: true,
              navigationBehavior: desc.Presentation,
              navigationLoadMode: desc.LoadMode,
                reusePolicy: desc.ReusePolicy));

        public void EmitGuardDenied(IPageView? from, Type? target, Type? redirect, string? reason)
        {
            var e = new GuardDeniedEvent(from, target, redirect, reason);

            try { _sink?.OnGuardDenied(e); }
            catch { /* never break navigation */ }

            _hub.Publish(e);
        }

        public void EmitInfo(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            try { _sink?.OnInfo(message); } catch { }
        }

        public void EmitWarn(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            try { _sink?.OnWarn(message); } catch { }
        }

        public void EmitError(string message, Exception? ex = null)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            try { _sink?.OnError(message, ex); } catch { }
        }
    }
}
