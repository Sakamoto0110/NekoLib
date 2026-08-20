namespace NekoLib.Watchdog
{
    /// <summary>
    /// Canonical RPC command names exchanged over the watchdog control pipe.
    /// Shared by the server (<c>WatchdogRuntime</c> handler map) and the client
    /// (<c>WatchdogController</c>) so the two can never drift on a literal string.
    /// </summary>
    public static class WatchdogCommands
    {
        public const string Ping = "ping";
        public const string Status = "status";
        public const string Pause = "pause";
        public const string Resume = "resume";
        public const string Restart = "restart";
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
