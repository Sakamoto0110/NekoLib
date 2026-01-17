using System;
using System.Collections.Generic;

namespace NekoLib.Diagnostics
{
    /// <summary>
    /// Default diagnostics runtime: creates loggers/contexts and dispatches to sinks.
    /// Threading/buffering policies can be added later without changing module-facing APIs.
    /// </summary>
    public sealed class DiagnosticsRuntime : IDiagnosticsRuntime
    {
        private readonly object _lock = new object();
        private readonly List<ILogSink> _logSinks = new List<ILogSink>();
        private readonly List<ITelemetrySink> _telemetrySinks = new List<ITelemetrySink>();

        public DiagnosticsRuntime() { }

        public void AddLogSink(ILogSink sink)
        {
            if(sink == null) throw new ArgumentNullException(nameof(sink));
            lock(_lock) _logSinks.Add(sink);
        }

        public void AddTelemetrySink(ITelemetrySink sink)
        {
            if(sink == null) throw new ArgumentNullException(nameof(sink));
            lock(_lock) _telemetrySinks.Add(sink);
        }

        public ILogger CreateLogger(string category)
            => new RuntimeLogger(this, category ?? "");

        public IDiagnosticContext CreateContext(string scope)
            => new RuntimeDiagnosticContext(this, scope ?? "");

        internal void Dispatch(LogEntry entry)
        {
            if(entry == null) return;

            ILogSink[] sinks;
            lock(_lock) sinks = _logSinks.ToArray();

            for(int i = 0; i < sinks.Length; i++)
            {
                try { sinks[i].Write(entry); }
                catch { /* never throw into app */ }
            }
        }

        internal void Dispatch(TelemetryEvent evt)
        {
            if(evt == null) return;

            ITelemetrySink[] sinks;
            lock(_lock) sinks = _telemetrySinks.ToArray();

            for(int i = 0; i < sinks.Length; i++)
            {
                try { sinks[i].Track(evt); }
                catch { /* never throw into app */ }
            }
        }

        public void Dispose()
        {
            // Future: flush buffers, dispose sinks if disposable.
        }

        private sealed class RuntimeLogger : ILogger
        {
            private readonly DiagnosticsRuntime _rt;
            private readonly string _category;

            public RuntimeLogger(DiagnosticsRuntime rt, string category)
            {
                _rt = rt;
                _category = category;
            }

            public void Log(LogLevel level, string message, Exception exception = null)
            {
                _rt.Dispatch(new LogEntry(DateTime.UtcNow, level, _category, message ?? "", exception));
            }

            public void Debug(string message) => Log(LogLevel.Debug, message);
            public void Info(string message) => Log(LogLevel.Info, message);
            public void Warn(string message) => Log(LogLevel.Warn, message);
            public void Error(string message, Exception exception = null) => Log(LogLevel.Error, message, exception);
            public void Critical(string message, Exception exception = null) => Log(LogLevel.Critical, message, exception);
        }

        private sealed class RuntimeDiagnosticContext : IDiagnosticContext
        {
            private readonly DiagnosticsRuntime _rt;
            private readonly string _scope;

            public RuntimeDiagnosticContext(DiagnosticsRuntime rt, string scope)
            {
                _rt = rt;
                _scope = scope;
                Logger = rt.CreateLogger(scope);
            }

            public ILogger Logger { get; }

            public void Track(TelemetryEvent evt)
            {
                // Ensure scope is represented even if caller didn't include it.
                if(evt == null)
                    return;

                _rt.Dispatch(evt);
            }
        }
    }
}
