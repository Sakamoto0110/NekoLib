namespace NekoLib.Diagnostics.Sinks
{
    public sealed class NullTelemetrySink : ITelemetrySink
    {
        public static readonly NullTelemetrySink Instance = new NullTelemetrySink();
        private NullTelemetrySink() { }
        public void Track(TelemetryEvent evt) { }
    }
}
