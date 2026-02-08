namespace NekoLib.Diagnostics.Abstractions
{
    public interface ITelemetrySink
    {
        void Track(TelemetryEvent evt);
    }
}
