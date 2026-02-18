namespace NekoLib.Diagnostics.Contracts
{
    public interface IDiagnostics
    {
        ILogger Logger { get; }
        ITelemetrySink Telemetry { get; }
         
    }
}
