namespace NekoLib.Diagnostics
{
    public interface ITelemetrySink
    {
        void Track(TelemetryEvent evt);
    }
}
