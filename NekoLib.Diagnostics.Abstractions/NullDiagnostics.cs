using System;

namespace NekoLib.Diagnostics
{
    public sealed class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new NullLogger();

        private NullLogger() { }

        public void Log(LogLevel level, string message, Exception exception = null) { }
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message, Exception exception = null) { }
        public void Critical(string message, Exception exception = null) { }
    }

    public sealed class NullDiagnosticContext : IDiagnosticContext
    {
        public static readonly NullDiagnosticContext Instance = new NullDiagnosticContext();

        private NullDiagnosticContext() { }

        public ILogger Logger => NullLogger.Instance;

        public void Track(TelemetryEvent evt) { }
    }
}
