using System;

namespace NekoLib.Diagnostics
{
    /// <summary>
    /// Runtime diagnostics service used by the NekoLib entrypoint.
    /// Feature modules depend only on abstractions (ILogger/IDiagnosticContext).
    /// </summary>
    public interface IDiagnosticsRuntime : IDisposable
    {
        ILogger CreateLogger(string category);
        IDiagnosticContext CreateContext(string scope);
    }
}
