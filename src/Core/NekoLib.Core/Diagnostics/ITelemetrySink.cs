namespace NekoLib.Core.Diagnostics
{
    public interface ITelemetrySink
    {
        void Track(TelemetryEvent evt);
    }
}
