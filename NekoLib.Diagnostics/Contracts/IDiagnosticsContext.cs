namespace NekoLib.Diagnostics.Contracts
{
    public interface IDiagnosticsContext
    {
        ILogger Logger { get; }
        ITelemetrySink Telemetry { get; }
         
    }
}
