namespace NekoLib.Core.Telemetry
{
    /// <summary>Classifies the terminal result of a telemetry operation.</summary>
    public enum TelemetryOutcome
    {
        /// <summary>No more specific terminal result was supplied.</summary>
        Unknown = 0,
        /// <summary>The operation completed successfully.</summary>
        Succeeded = 1,
        /// <summary>The operation completed with a failure.</summary>
        Failed = 2,
        /// <summary>The operation ended because it was cancelled.</summary>
        Cancelled = 3
    }
}
