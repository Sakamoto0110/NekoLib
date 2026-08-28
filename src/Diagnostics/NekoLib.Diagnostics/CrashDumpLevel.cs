namespace NekoLib.Diagnostics
{
    /// <summary>
    /// Dump "levels" (what gets captured). Bigger levels = bigger files + more sensitive data risk.
    /// Keep MiniDumpNormal as default for field machines.
    /// </summary>
    public enum CrashDumpLevel
    {
        /// <summary>Requests no dump artifact.</summary>
        None = 0,
        /// <summary>Requests the platform writer's normal minimal dump.</summary>
        MiniDumpNormal = 1,
        /// <summary>Requests a normal dump plus writable data segments.</summary>
        WithDataSegs = 2,
        /// <summary>Requests a normal dump plus operating-system handle data.</summary>
        WithHandleData = 3,
        /// <summary>Requests a normal dump plus thread information.</summary>
        WithThreadInfo = 4,
        /// <summary>Requests a full-memory dump, which can be large and contain sensitive process memory.</summary>
        WithFullMemory = 5
    }
}
