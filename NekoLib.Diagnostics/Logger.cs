 using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NekoLib.Diagnostics
{
    public sealed class Logger : ILogger
    {
        private readonly ILogSink[] _sinks;
        private readonly LogLevel _minLevel;

        public Logger(LogLevel minLevel, params ILogSink[] sinks)
        {
            _minLevel = minLevel;
            _sinks = sinks ?? Array.Empty<ILogSink>();
        }

        public void Log(LogLevel level, string message, Exception ex = null)
        {
            if (level < _minLevel)
                return;

            var entry = new LogEntry(
                DateTime.UtcNow,
                level,
                message,
                ex
            );

            for (int i = 0; i < _sinks.Length; i++)
            {
                try
                {
                    _sinks[i].Write(entry);
                }
                catch
                {
                    // swallow – logging must never crash the app
                }
            }
        }

        public void Trace(string message) => Log(LogLevel.Trace, message);
        public void Debug(string message) => Log(LogLevel.Debug, message);
        public void Info(string message) => Log(LogLevel.Info, message);
        public void Warn(string message) => Log(LogLevel.Warn, message);
        public void Error(string message, Exception ex = null)
            => Log(LogLevel.Error, message, ex);
        public void Fatal(string message, Exception ex = null)
            => Log(LogLevel.Fatal, message, ex);
    }
}

