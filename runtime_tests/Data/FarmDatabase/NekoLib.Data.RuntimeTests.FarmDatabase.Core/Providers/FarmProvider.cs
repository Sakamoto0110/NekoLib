#nullable enable
using System;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.Core.Providers
{
    /// <summary>
    /// The two database engines this scenario drives side by side. They were chosen
    /// because they disagree on almost everything that matters to
    /// <c>NekoLib.Data</c>: row limiting, DDL vocabulary, parameter binding, and
    /// catalog discovery.
    /// </summary>
    public enum FarmProvider
    {
        /// <summary>File-backed SQLite through <c>Microsoft.Data.Sqlite</c>.</summary>
        Sqlite = 0,

        /// <summary>Access <c>.accdb</c> through the ACE OLEDB driver.</summary>
        Access = 1
    }

    /// <summary>
    /// Result of probing whether a provider can actually run on this machine.
    /// Availability is a runtime fact, not a compile-time one: the scenario builds
    /// on any Windows box, but Access additionally needs the ACE driver installed
    /// at a matching bitness.
    /// </summary>
    public sealed class ProviderAvailability
    {
        public bool IsAvailable { get; }

        /// <summary>Human-readable reason when <see cref="IsAvailable"/> is false.</summary>
        public string? Reason { get; }

        /// <summary>Remediation hint shown in the UI when unavailable.</summary>
        public string? Remedy { get; }

        private ProviderAvailability(bool isAvailable, string? reason, string? remedy)
        {
            IsAvailable = isAvailable;
            Reason = reason;
            Remedy = remedy;
        }

        public static ProviderAvailability Available() =>
            new ProviderAvailability(true, null, null);

        public static ProviderAvailability Unavailable(string reason, string? remedy = null) =>
            new ProviderAvailability(false, reason, remedy);
    }
}
