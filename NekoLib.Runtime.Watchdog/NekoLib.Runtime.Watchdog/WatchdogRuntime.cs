using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace NekoLib.Runtime.Watchdog
{
    /// <summary>
    /// Runtime watchdog core (DLL) that owns a target process:
    /// - starts it
    /// - monitors it
    /// - restarts on crash/exit
    ///
    /// This is an aggressive cleanup of the legacy watchdog Program.cs,
    /// kept behaviorally similar and .NET Framework 4.8.1 friendly.
    /// </summary>
    public sealed class WatchdogRuntime : IDisposable
    {
        // Only shortcut enabled for now: Ctrl+Shift+F12 => Kill child + exit watchdog.
        // (Other shortcuts can be added later.)
        private const int ID_F12_KILL_EXIT = 1;
        private const uint VK_F12 = 0x7B;

        private readonly object _childLock = new object();
        private readonly object _logLock = new object();

        private WatchdogOptions _o;
        private Process _child;

        private volatile bool _exiting;
        private volatile bool _shutdownRequested;
        private volatile bool _enabled = true;
        private volatile bool _allowCloseOnce;

        private int _backoffRuntime;

        private string _flagOnline;
        private string _flagOffline;
        private string _flagPaused;
        private string _flagKilled;
        private string _flagRestart;
        private string _logPath;

        private Thread _monitorThread;
        private ManualResetEvent _exitEvent = new ManualResetEvent(false);

        private Mutex _mutex;

        public WatchdogRuntime(WatchdogOptions options)
        {
            if(options == null) throw new ArgumentNullException("options");
            _o = options;
        }

        /// <summary>
        /// Starts the watchdog monitor thread. If hotkeys are enabled and you want them working,
        /// call <see cref="RunMessageLoop"/> (blocking) from your main thread after calling Start.
        /// </summary>
        public void Start()
        {
            _o.NormalizeAndFillDefaults();
            BuildPaths();

            _mutex = new Mutex(true, @"Global\Watchdog::" + _o.InstanceTag, out bool created);
            if(!created)
            {
                // Another watchdog instance already running.
                _mutex.Dispose();
                _mutex = null;
                throw new InvalidOperationException("Watchdog instance already running for InstanceTag=" + _o.InstanceTag);
            }

            if(_o.AttachPid > 0)
                KillIfMatches(_o.AttachPid, _o.TargetPath);

            _monitorThread = new Thread(MonitorLoop)
            {
                IsBackground = true,
                Name = "WDG-Monitor"
            };
            _monitorThread.Start();
        }

        /// <summary>
        /// Runs a WinAPI message loop on the current thread to receive hotkeys.
        /// This is the closest match to the legacy behavior.
        /// </summary>
        public void RunMessageLoop()
        {
            if(!_o.EnableHotkeys)
            {
                // No hotkeys => just block until exit is requested.
                _exitEvent.WaitOne();
                return;
            }

            TryRegisterHotkeys();

            NativeMethods.MSG msg;
            while(!_exiting && NativeMethods.GetMessage(out msg, IntPtr.Zero, 0, 0))
            {
                if(msg.message == NativeMethods.WM_HOTKEY)
                {
                    if(msg.wParam.ToInt32() == ID_F12_KILL_EXIT)
                        KillAndExit();
                }

                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }
        }

        public void RequestExit()
        {
            _exiting = true;
            _shutdownRequested = true;
            _exitEvent.Set();

            lock(_childLock) TryKill(_child);

            try { NativeMethods.PostQuitMessage(0); } catch { }
        }

        /// <summary>
        /// Hotkey action: finalizes the target (and any matching instances) and shuts down.
        /// Mirrors legacy Ctrl+Shift+F12 behavior.
        /// </summary>
        public void KillAndExit()
        {
            Log("[hotkey] kill_exit");

            _exiting = true;
            _shutdownRequested = true;

            lock(_childLock) TryKill(_child);
            KillAllMatchingTarget();

            DeleteQuiet(_flagOnline);
            Touch(_flagOffline);

            _exitEvent.Set();

            try { NativeMethods.PostQuitMessage(0); } catch { }
        }

        private void MonitorLoop()
        {
            _backoffRuntime = 0;

            while(true)
            {
                if(_exiting) break;

                if(!File.Exists(_flagOnline)) { SwitchToOffline(); break; }
                if(File.Exists(_flagOffline)) { SwitchToOffline(); break; }
                if(File.Exists(_flagKilled)) { SwitchToKilled(); break; }

                if(!_enabled)
                {
                    lock(_childLock) TryKill(_child);
                    Log("[state] disabled_idle");
                    IdleUntilEnabledOrExit();
                    continue;
                }

                if(Consume(_flagRestart))
                {
                    lock(_childLock) TryKill(_child);
                    _allowCloseOnce = false;
                }

                var child = StartChild();
                if(child == null)
                {
                    _backoffRuntime = Math.Min(_backoffRuntime + _o.BackoffStepMs, _o.MaxBackoffMs);
                    Log("[backoff] ms=" + _backoffRuntime);
                    WaitWithChecks(_o.RestartDelayMs + _backoffRuntime);
                    continue;
                }

                while(!_shutdownRequested && !_exiting && !child.HasExited)
                {
                    if(!_enabled) { TryKill(child); break; }
                    if(Consume(_flagRestart)) { TryKill(child); _allowCloseOnce = false; break; }
                    Thread.Sleep(250);
                }

                var code = child.HasExited ? child.ExitCode : -9999;
                Log("[child_exit] code=" + code);

                if(_allowCloseOnce)
                {
                    _allowCloseOnce = false;
                    Log("[state] allow_close_once_consumed");
                    WaitForManualStartAndAttach();
                    continue;
                }

                if(_enabled && !_exiting)
                {
                    var wait = _o.RestartDelayMs + _backoffRuntime;
                    Log("[restart_wait] ms=" + wait);
                    WaitWithChecks(wait);
                }
            }

            Log("[state] mode_exit");
            _exitEvent.Set();
        }

        private Process StartChild()
        {
            try
            {
                var childArgs = string.IsNullOrWhiteSpace(_o.TargetArgs)
                    ? "--under-watchdog"
                    : _o.TargetArgs + " --under-watchdog";

                Log("[child_args] " + childArgs);

                var psi = new ProcessStartInfo
                {
                    FileName = _o.TargetPath,
                    Arguments = childArgs,
                    WorkingDirectory = _o.WorkDir,
                    UseShellExecute = true
                };

                var p = Process.Start(psi);
                lock(_childLock) _child = p;
                Log("[child_start] pid=" + p.Id);
                return p;
            }
            catch(Exception ex)
            {
                Log("[error] StartChild: " + ex.Message);
                return null;
            }
        }

        private void TryRegisterHotkeys()
        {
            // Only one shortcut for now: Ctrl+Shift+F12.
            try
            {
                NativeMethods.RegisterHotKey(IntPtr.Zero, ID_F12_KILL_EXIT,
                    WatchdogHotkeys.MOD_CONTROL | WatchdogHotkeys.MOD_SHIFT, VK_F12);
            }
            catch { }
        }

        private void TryUnregisterHotkeys()
        {
            try { NativeMethods.UnregisterHotKey(IntPtr.Zero, ID_F12_KILL_EXIT); } catch { }
        }

        private void IdleUntilEnabledOrExit()
        {
            while(true)
            {
                if(_exiting || _shutdownRequested) return;
                if(_enabled) return;
                Thread.Sleep(250);
            }
        }

        private void WaitForManualStartAndAttach()
        {
            var name = Path.GetFileNameWithoutExtension(_o.TargetPath);
            while(!_exiting && !_shutdownRequested)
            {
                try
                {
                    foreach(var p in Process.GetProcessesByName(name))
                    {
                        if(!p.HasExited && IsSameFile(SafeModulePath(p), _o.TargetPath))
                        {
                            lock(_childLock) _child = p;
                            Log("[attached_manual_start] pid=" + p.Id);
                            return;
                        }
                    }
                }
                catch { }

                Thread.Sleep(500);
            }
        }

        private void TryKill(Process p)
        {
            try
            {
                if(p == null || p.HasExited) return;
                if(p.MainWindowHandle != IntPtr.Zero)
                {
                    p.CloseMainWindow();
                    if(!p.WaitForExit(1000)) p.Kill();
                }
                else
                {
                    p.Kill();
                }
                p.WaitForExit(1000);
            }
            catch { }
        }

        private void KillAllMatchingTarget()
        {
            try
            {
                var name = Path.GetFileNameWithoutExtension(_o.TargetPath);
                foreach(var p in Process.GetProcessesByName(name))
                {
                    if(p == null || p.HasExited) continue;
                    var path = SafeModulePath(p);
                    if(IsSameFile(path, _o.TargetPath))
                    {
                        TryKill(p);
                    }
                }
            }
            catch { }
        }

        private void SwitchToOffline()
        {
            Log("[state] mode_offline");
            _shutdownRequested = true;
            lock(_childLock) TryKill(_child);
            try { NativeMethods.PostQuitMessage(0); } catch { }
            _exitEvent.Set();
        }

        private void SwitchToKilled()
        {
            Log("[state] mode_killed");
            _shutdownRequested = true;
            lock(_childLock) TryKill(_child);
            try { NativeMethods.PostQuitMessage(0); } catch { }
            _exitEvent.Set();
        }

        private void WaitWithChecks(int ms)
        {
            int step = 250, elapsed = 0;
            while(elapsed < ms && !_shutdownRequested && !_exiting)
            {
                if(!File.Exists(_flagOnline)) { SwitchToOffline(); return; }
                if(File.Exists(_flagOffline)) { SwitchToOffline(); return; }
                if(File.Exists(_flagKilled)) { SwitchToKilled(); return; }
                Thread.Sleep(step);
                elapsed += step;
            }
        }

        private void KillIfMatches(int pid, string expectedPath)
        {
            try
            {
                var p = Process.GetProcessById(pid);
                var path = SafeModulePath(p);
                if(IsSameFile(path, expectedPath)) TryKill(p);
            }
            catch { }
        }

        private void BuildPaths()
        {
            var dir = _o.FlagsDirectory;

            _flagOnline = Path.Combine(dir, "watchdog.online");
            _flagOffline = Path.Combine(dir, "watchdog.offline");
            _flagPaused = Path.Combine(dir, "watchdog.paused");
            _flagKilled = Path.Combine(dir, "watchdog.killed");
            _flagRestart = Path.Combine(dir, "watchdog.restart");
            _logPath = Path.Combine(dir, "watchdog.log");

            Touch(_flagOnline);
        }

        private void Log(string msg)
        {
            try
            {
                var line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + Environment.NewLine;
                lock(_logLock)
                {
                    File.AppendAllText(_logPath, line);
                }
            }
            catch { }
        }

        private static void Touch(string p)
        {
            try { File.WriteAllText(p, DateTime.Now.ToString("O")); } catch { }
        }

        private static bool Consume(string p)
        {
            try
            {
                if(File.Exists(p))
                {
                    File.Delete(p);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static void DeleteQuiet(string p)
        {
            try { if(File.Exists(p)) File.Delete(p); } catch { }
        }

        private static string SafeModulePath(Process p)
        {
            try { return p.MainModule.FileName; } catch { return ""; }
        }

        private static bool IsSameFile(string a, string b)
        {
            try { return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        public void Dispose()
        {
            try { TryUnregisterHotkeys(); } catch { }

            try { RequestExit(); } catch { }

            if(_monitorThread != null)
            {
                try { _monitorThread.Join(1500); } catch { }
                _monitorThread = null;
            }

            if(_mutex != null)
            {
                try { _mutex.ReleaseMutex(); } catch { }
                try { _mutex.Dispose(); } catch { }
                _mutex = null;
            }

            if(_exitEvent != null)
            {
                try { _exitEvent.Close(); } catch { }
                _exitEvent = null;
            }
        }
    }
}
