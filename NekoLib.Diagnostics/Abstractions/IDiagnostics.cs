namespace NekoLib.Diagnostics.Abstractions
{
    public interface IDiagnostics
    {
        ILogger Logger { get; }
        ITelemetrySink Telemetry { get; }
         
    }
}
