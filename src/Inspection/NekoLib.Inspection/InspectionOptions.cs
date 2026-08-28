namespace NekoLib.Inspection
{
    /// <summary>
    /// Configures the bounded operation history owned by an
    /// <see cref="InspectionRuntime"/>. Values are captured at construction.
    /// </summary>
    public sealed class InspectionOptions
    {
        /// <summary>Gets or sets the retained operation capacity. The default is 1024 and the value must be at least 1.</summary>
        public int Capacity { get; set; } = 1024;
    }
}
