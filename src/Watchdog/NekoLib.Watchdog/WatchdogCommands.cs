namespace NekoLib.Watchdog
{
    /// <summary>
    /// Canonical RPC command names exchanged over the watchdog control pipe.
    /// Shared by the server (<c>WatchdogRuntime</c> handler map) and the client
    /// (<c>WatchdogController</c>) so the two can never drift on a literal string.
    /// </summary>
    public static class WatchdogCommands
    {
        /// <summary>Health-check command; the accepted response is <c>pong</c>.</summary>
        public const string Ping = "ping";
        /// <summary>Status-snapshot command.</summary>
        public const string Status = "status";
        /// <summary>Command that pauses restart supervision without terminating the current target.</summary>
        public const string Pause = "pause";
        /// <summary>Command that resumes restart supervision.</summary>
        public const string Resume = "resume";
        /// <summary>Command that terminates the current target and launches its replacement.</summary>
        public const string Restart = "restart";
        /// <summary>Command that stops the runtime and its supervised target.</summary>
        public const string Stop = "stop";

        internal const string LogHistory = "log_history";
        internal const string ExceptionNotify = "exception_notify";
        internal const string ProtocolVersion = "protocol_version";
        internal const string AttachStatus = "attach_status";
        internal const string LogWrite = "log_write";
        internal const string LogWriteBatch = "log_write_batch";
        internal const string Update = "update";
    }
}
