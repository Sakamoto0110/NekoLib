// FILE: NekoLib.Navigation/Diagnostics/DiagnosticsNavigationSink.cs
#nullable enable
using NekoLib.Core.Diagnostics;
using System;
using System.Collections.Generic;

namespace NekoLib.Navigation.Diagnostics
{
    /// <summary>
    /// Bridges navigation events into NekoLib.Diagnostics (Logger + Telemetry).
    /// Kept in Navigation to remain "drop-in". You can move it to Diagnostics later if desired.
    /// </summary>
    public sealed class DiagnosticsNavigationSink : INavigationDiagnosticsSink
    {
        private readonly IDiagnosticsContext _ctx;

        public DiagnosticsNavigationSink(IDiagnosticsContext ctx)
            => _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));

        public void OnNavigation(PageLogEntry entry)
        {
            if (entry is null) return;

            // Keep log payload small and structured.
            try
            {
                _ctx.Logger?.Info(entry.ToString());
            }
            catch { /* never break navigation */ }

            try
            {
                var data = new Dictionary<string, object>
                {
                    ["From"] = entry.FromPageName ?? "<null>",
                    ["To"] = entry.ToPageName ?? "<null>",
                    ["Success"] = entry.Success,
                    ["Behavior"] = entry.Presentation.ToString(),
                    ["LoadMode"] = entry.LoadMode.ToString(),
                    ["Timeout"] = entry.IsTimeout,
                    ["Back"] = entry.IsBackNavigation,
                    ["FailureKind"] = entry.FailureKind.ToString(),
                    ["Error"] = entry.Error ?? string.Empty
                };

                _ctx.Telemetry?.Track(new TelemetryEvent(
                    timestampUtc: entry.TimestampUtc,
                    name: $"Navigation.Page[{data["From"]} -> {data["To"]}]",
                    duration: null,
                    data: data
                ));
            }
            catch { /* never break navigation */ }
        }

        public void OnGuardDenied(GuardDeniedEvent e)
        {
            if (e is null) return;

            try
            {
                _ctx.Logger?.Warn($"GuardDenied: from={e.FromPage?.Name ?? "<null>"} target={e.TargetPage?.Name ?? "<null>"} redirect={e.RedirectPage?.Name ?? "<null>"} reason={e.Reason ?? "<null>"}");
            }
            catch { }
        }
    }
}
