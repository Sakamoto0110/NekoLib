namespace NekoLib.Diagnostics
{
    public interface IDiagnostics
    {
        ILogger Logger { get; }
        ITelemetrySink Telemetry { get; }
         
    }
}
