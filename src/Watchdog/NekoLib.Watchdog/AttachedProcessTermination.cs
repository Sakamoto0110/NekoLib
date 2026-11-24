using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NekoLib.Watchdog
{
    // EnsureStarted launches the Host from the initial application. Killing that
    // application's tree with taskkill /T would kill its own supervisor as well.
    internal static class AttachedProcessTermination
    {
        internal static void Kill(Process target, int timeoutMs)
        {
            using var supervisor = Process.GetCurrentProcess();
            if (target.Id == supervisor.Id) return;
            var order = new List<int> { target.Id };
            try { order = SelectKillOrder(target.Id, supervisor.Id, CaptureParents()); }
            catch { /* Snapshot failure must never fall back to killing the Host tree. */ }

            var watch = Stopwatch.StartNew();
            foreach (var pid in order)
            {
                try
                {
                    if (pid == target.Id)
                    {
                        if (!target.HasExited) target.Kill();
                        target.WaitForExit(Math.Max(0, timeoutMs - (int)watch.ElapsedMilliseconds));
                    }
                    else
                    {
                        using var descendant = Process.GetProcessById(pid);
                        // Refuse an older process if a snapshot PID was reused.
                        if (descendant.StartTime < target.StartTime) continue;
                        _ = descendant.Handle;
                        if (!descendant.HasExited) descendant.Kill();
                    }
                }
                catch { }
            }
        }

        internal static List<int> SelectKillOrder(int targetPid, int supervisorPid, IReadOnlyDictionary<int, int> parents)
        {
            var selected = new List<KeyValuePair<int, int>>();
            foreach (var pid in parents.Keys)
            {
                var visited = new HashSet<int>();
                var cursor = pid;
                var depth = 0;
                while (visited.Add(cursor))
                {
                    // Protect the Host and every process in its own branch.
                    if (cursor == supervisorPid) break;
                    if (cursor == targetPid)
                    {
                        selected.Add(new KeyValuePair<int, int>(pid, depth));
                        break;
                    }
                    if (!parents.TryGetValue(cursor, out cursor)) break;
                    depth++;
                }
            }
            selected.Sort((left, right) => right.Value.CompareTo(left.Value));
            var result = new List<int>();
            foreach (var pair in selected) result.Add(pair.Key);
            if (targetPid != supervisorPid && !result.Contains(targetPid)) result.Add(targetPid);
            return result;
        }

        private static Dictionary<int, int> CaptureParents()
        {
            var snapshot = CreateToolhelp32Snapshot(2, 0);
            if (snapshot == new IntPtr(-1)) throw new InvalidOperationException("Cannot snapshot process parents.");
            try
            {
                var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf(typeof(ProcessEntry)) };
                var result = new Dictionary<int, int>();
                if (!Process32First(snapshot, ref entry)) throw new InvalidOperationException("Cannot read process parents.");
                do { result[(int)entry.ProcessId] = (int)entry.ParentProcessId; }
                while (Process32Next(snapshot, ref entry));
                return result;
            }
            finally { CloseHandle(snapshot); }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ProcessEntry
        {
            internal uint Size, Usage, ProcessId;
            internal UIntPtr DefaultHeapId;
            internal uint ModuleId, Threads, ParentProcessId;
            internal int Priority;
            internal uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] internal string ExeFile;
        }

        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry entry);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry entry);
        [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
    }
}
