namespace NekoLib.Diagnostics
{
    public interface IDiagnosticContext
    {
        ILogger Logger { get; }

        void Track(TelemetryEvent evt);
    }
}
