namespace NekoLib.Core.Logging
{
    /// <summary>Identifies the severity of a log entry.</summary>
    public enum LogLevel
    {
        /// <summary>Fine-grained diagnostic detail.</summary>
        Trace = 0,
        /// <summary>Diagnostic information useful while debugging.</summary>
        Debug = 1,
        /// <summary>Normal informational progress.</summary>
        Info = 2,
        /// <summary>A recoverable or potentially harmful condition.</summary>
        Warn = 3,
        /// <summary>A failed operation or significant fault.</summary>
        Error = 4,
        /// <summary>A terminal or process-threatening fault.</summary>
        Fatal = 5
    }
}
