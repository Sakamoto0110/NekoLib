namespace NekoLib.Runtime.Diagnostics
{
    /// <summary>
    /// Dump "levels" (what gets captured). Bigger levels = bigger files + more sensitive data risk.
    /// Keep MiniDumpNormal as default for field machines.
    /// </summary>
    public enum CrashDumpLevel
    {
        None = 0,
        MiniDumpNormal = 1,
        WithDataSegs = 2,
        WithHandleData = 3,
        WithThreadInfo = 4,
        WithFullMemory = 5
    }
}
