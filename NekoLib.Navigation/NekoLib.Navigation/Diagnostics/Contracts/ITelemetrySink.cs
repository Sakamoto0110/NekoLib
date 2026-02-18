namespace NekoLib.Diagnostics.Contracts

{
    public interface ITelemetrySink
    {
        void Track(TelemetryEventData evt);
    }
}
