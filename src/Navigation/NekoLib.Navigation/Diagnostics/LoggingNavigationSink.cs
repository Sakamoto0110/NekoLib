using NekoLib.Core.Logging;
using System;

namespace NekoLib.Navigation.Diagnostics
{
    /// <summary>Projects Navigation outcomes into the independent logging channel.</summary>
    public sealed class LoggingNavigationSink : INavigationDiagnosticsSink
    {
        private readonly ILogger _logger;

        public LoggingNavigationSink(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void OnNavigation(PageLogEntry entry)
        {
            if (entry == null)
                return;

            try
            {
                _logger.Log(
                    entry.Success ? LogLevel.Info : LogLevel.Error,
                    entry.ToString(),
                    category: "Navigation");
            }
            catch
            {
                // Logging must never alter navigation control flow.
            }
        }

        public void OnGuardDenied(GuardDeniedEvent denied)
        {
            if (denied == null)
                return;

            try
            {
                _logger.Warn(
                    $"GuardDenied: from={denied.FromPage?.Name ?? "<null>"} " +
                    $"target={denied.TargetPage?.Name ?? "<null>"} " +
                    $"redirect={denied.RedirectPage?.Name ?? "<null>"} " +
                    $"reason={denied.Reason ?? "<null>"}",
                    "Navigation");
            }
            catch
            {
            }
        }
    }
}
