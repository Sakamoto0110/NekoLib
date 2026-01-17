using NekoLib.Diagnostics;

namespace NekoLib
{
    /// <summary>
    /// Entry-point-only builder used to configure diagnostics sinks.
    /// </summary>
    public interface IDiagnosticsBuilder
    {
        IDiagnosticsBuilder AddLogSink(ILogSink sink);
        IDiagnosticsBuilder AddTelemetrySink(ITelemetrySink sink);
    }
}
