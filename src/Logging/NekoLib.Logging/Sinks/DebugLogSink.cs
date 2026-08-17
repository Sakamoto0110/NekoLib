using NekoLib.Core.Logging;
using System;
using System.Diagnostics;

namespace NekoLib.Logging.Sinks
{
    /// <summary>
    /// Writes formatted entries to the process trace channel, which the default
    /// listener forwards to the attached debugger and to the Windows debug
    /// output stream.
    /// <para/>
    /// This deliberately uses <see cref="Trace"/> and not <see cref="Debug"/>.
    /// <c>Debug.WriteLine</c> is <c>[Conditional("DEBUG")]</c>, so the call is
    /// removed from the Release assembly that ships in the package and the sink
    /// discards every entry. <c>TRACE</c> is defined for both configurations of
    /// this project.
    /// </summary>
    public sealed class DebugLogSink : ILogSink
    {
        public void Write(LogEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            try { Trace.WriteLine(entry.ToString()); }
            catch { }
        }
    }
}
