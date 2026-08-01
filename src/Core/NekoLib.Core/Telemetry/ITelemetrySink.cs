namespace NekoLib.Core.Telemetry
{
    public interface ITelemetrySink
    {
        void Write(TelemetryOperation operation);
    }
}
