using NekoLib.Diagnostics;

namespace NekoLib.Diagnostics.Contracts
{
    /// <summary>
    /// Provides access to logging and telemetry services.
    /// This is a pure contract and contains no behavior.
    /// </summary>
    public interface IDiagnosticsContext
    {
        ILogger Logger { get; }
        ITelemetrySink Telemetry { get; }
    }
}
