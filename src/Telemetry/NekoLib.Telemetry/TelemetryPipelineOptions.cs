namespace NekoLib.Telemetry
{
    /// <summary>
    /// Configures the bounded in-memory history owned by a <see cref="TelemetryPipeline"/>.
    /// Values are captured when the pipeline is constructed.
    /// </summary>
    public sealed class TelemetryPipelineOptions
    {
        /// <summary>
        /// Gets or sets the maximum number of completed operations retained for
        /// snapshots. The default is 1024 and the value must be at least 1.
        /// </summary>
        public int RecentOperationCapacity { get; set; } = 1024;
    }
}
