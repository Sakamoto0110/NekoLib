namespace NekoLib.Core.Diagnostics
{
    public interface IDiagnosticsContext
    {
        ILogger Logger { get; }
        ITelemetrySink Telemetry { get; }
    }
}
