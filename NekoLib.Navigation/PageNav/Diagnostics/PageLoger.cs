using NekoLib.Diagnostics;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;
using System;

namespace NekoLib.Navigation.Diagnostics
{
    /// <summary>
    /// Hook surface for navigation diagnostics.
    /// Consumers (or the NekoLib host) can subscribe to receive navigation events.
    /// </summary>
    public static class NavigationDiagnostics
    {
        /// <summary>
        /// Optional diagnostic context injected by bootstrap/host.
        /// If not set, events are ignored.
        /// </summary>
        public static IDiagnosticContext Context { get; set; }

        public static event Action<PageLogEntry> NavigationLogged;

        internal static void EmitNavigation(PageLogEntry entry)
        {
            try
            {
                // 1) Push to IDiagnosticContext as log + telemetry
                var ctx = Context;
                if(ctx != null)
                {
                    ctx.Logger.Info(entry.ToString());

                    // Telemetry payload (small & safe)
                    var data = new System.Collections.Generic.Dictionary<string, object>
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

                    ctx.Track(new TelemetryEvent(
                        timestampUtc: entry.TimestampUtc,
                        name: "Navigation.Page",
                        duration: null,
                        data: data
                    ));
                }

                // 2) Notify subscribers (for in-memory dashboards, overlays, tests)
                NavigationLogged?.Invoke(entry);
            }
            catch
            {
                // Never throw into navigation.
            }
        }

        internal static void EmitSuccess(IPageView from, IPageView to, NavigationArgs args)
            => EmitNavigation(new PageLogEntry(from?.GetType(), from?.Name, to?.GetType(), to?.Name, args, success: true));

        internal static void EmitFailure(IPageView from, IPageView to, NavigationArgs args, NavigationFailureKind kind = NavigationFailureKind.None, string error = null)
            => EmitNavigation(new PageLogEntry(from?.GetType(), from?.Name, to?.GetType(), to?.Name, args, success: false, failureKind: kind, error: error));

        internal static void EmitTimeout(IPageView from, IPageView to, NavigationArgs args)
            => EmitNavigation(new PageLogEntry(from?.GetType(), from?.Name, to?.GetType(), to?.Name, args, success: true, isTimeout: true));

        internal static void EmitBack(IPageView from, IPageView to, NavigationArgs args)
            => EmitNavigation(new PageLogEntry(from?.GetType(), from?.Name, to?.GetType(), to?.Name, args, success: true, isBackNavigation: true));

        internal static void EmitError(string message, Exception ex)
        {
            try
            {
                var ctx = Context;
                if(ctx != null)
                    ctx.Logger.Error(message, ex);
            }
            catch { }
        }

        internal static void EmitInfo(string message)
        {
            try
            {
                var ctx = Context;
                if(ctx != null)
                    ctx.Logger.Info(message);
            }
            catch { }
        }

        internal static void EmitWarn(string message)
        {
            try
            {
                var ctx = Context;
                if(ctx != null)
                    ctx.Logger.Warn(message);
            }
            catch { }
        }
    }
}
