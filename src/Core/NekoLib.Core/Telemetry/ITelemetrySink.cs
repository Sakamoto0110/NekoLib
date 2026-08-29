namespace NekoLib.Core.Telemetry
{
    /// <summary>Receives structurally read-only completed telemetry operations.</summary>
    /// <remarks>
    /// This is the usual consumer implementation seam for export or aggregation.
    /// The composing telemetry implementation owns dispatch ordering, threading,
    /// exception isolation, and lifetime. A sink owns redaction and access control
    /// before it persists or transmits dimension values.
    /// </remarks>
    public interface ITelemetrySink
    {
        /// <summary>Consumes one completed operation.</summary>
        /// <param name="operation">Non-null completed operation model.</param>
        /// <remarks>
        /// Implementations should return promptly when used by an inline pipeline.
        /// They must avoid recursively producing telemetry through the same pipeline.
        /// </remarks>
        void Write(TelemetryOperation operation);
    }
}
