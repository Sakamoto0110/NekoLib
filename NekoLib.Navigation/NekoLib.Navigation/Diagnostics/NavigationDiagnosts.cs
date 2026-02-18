using NekoLib.Diagnostics.Contracts;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;
using System;
using System.Collections.Generic;

namespace NekoLib.Navigation.Diagnostics
{
    /// <summary>
    /// Hook surface for navigation diagnostics.
    /// Consumers (or host) can attach a diagnostics context.
    /// </summary>
    public static class NavigationDiagnostics
    {
        /// <summary>
        /// Optional diagnostics context injected by bootstrap/host.
        /// If null, diagnostics are ignored.
        /// </summary>
        public static IDiagnosticsContext Context { get; set; }

        public static event Action<PageLogEntry> NavigationLogged;
        public static event Action<GuardDeniedEvent> GuardDenied;

        internal static void EmitNavigation(PageLogEntry entry)
        {
            try
            {
                var ctx = Context;

                if (ctx != null)
                {
                    ctx.Logger?.Info(entry.ToString());

                    var data = new Dictionary<string, object>
                    {
                        ["From"] = entry.FromPageName ?? "<null>",
                        ["To"] = entry.ToPageName ?? "<null>",
                        ["Success"] = entry.Success,
                        ["Behavior"] = entry.Behavior.ToString(),
                        ["LoadMode"] = entry.LoadMode.ToString(),
                        ["Timeout"] = entry.IsTimeout,
                        ["Back"] = entry.IsBackNavigation,
                        ["FailureKind"] = entry.FailureKind.ToString(),
                    };

                    ctx.Telemetry?.Track(new TelemetryEventData(
                        timestampUtc: entry.TimestampUtc,
                        name: "Navigation.Page",
                        duration: null,
                        data: data
                    ));
                }

                NavigationLogged?.Invoke(entry);
            }
            catch
            {
                // Diagnostics must never break navigation.
            }
        }

        internal static void EmitSuccess(IPageView from, IPageView to, NavigationArgs args)
            => EmitNavigation(new PageLogEntry(
                from?.GetType(), from?.Name,
                to?.GetType(), to?.Name,
                args,
                success: true));

        internal static void EmitFailure(
            IPageView from,
            IPageView to,
            NavigationArgs args,
            NavigationFailureKind kind = NavigationFailureKind.None,
            string error = null)
            => EmitNavigation(new PageLogEntry(
                from?.GetType(), from?.Name,
                to?.GetType(), to?.Name,
                args,
                success: false,
                failureKind: kind,
                error: error));

        internal static void EmitTimeout(IPageView from, IPageView to, NavigationArgs args)
            => EmitNavigation(new PageLogEntry(
                from?.GetType(), from?.Name,
                to?.GetType(), to?.Name,
                args,
                success: true,
                isTimeout: true));

        internal static void EmitBack(IPageView from, IPageView to, NavigationArgs args)
            => EmitNavigation(new PageLogEntry(
                from?.GetType(), from?.Name,
                to?.GetType(), to?.Name,
                args,
                success: true,
                isBackNavigation: true));

        internal static void EmitGuardDenied(
            IPageView from,
            Type target,
            Type redirect,
            string reason)
        {
            try
            {
                GuardDenied?.Invoke(
                    new GuardDeniedEvent(
                        from,
                        target,
                        redirect,
                        reason));
            }
            catch
            {
                // Diagnostics must never break navigation.
            }
        }

        internal static void EmitError(string message, Exception ex)
        {
            try
            {
                Context?.Logger?.Error(message, ex);
            }
            catch { }
        }

        internal static void EmitInfo(string message)
        {
            try
            {
                Context?.Logger?.Info(message);
            }
            catch { }
        }

        internal static void EmitWarn(string message)
        {
            try
            {
                Context?.Logger?.Warn(message);
            }
            catch { }
        }

        public static void EmitExternal(string message)
        {
            try
            {
                Context?.Logger?.Info("[External] " + message);
            }
            catch { }
        }
    }
}
