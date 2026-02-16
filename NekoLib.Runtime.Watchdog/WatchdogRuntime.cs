using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace NekoLib.Runtime.Watchdog
{
    internal enum KillOutcome
    {
        None,
        GracefulSuccess,
        GracefulTimeout,
        ForceSuccess,
        ForceTimeout,
        Error
    }

    internal enum ExitReason
    {
        None,
        NaturalExit,
        Restart,
        Pause,
        Stop,
        ForceKill,
        Crash
    }

    internal enum RestartReason
    {
        None,
        InitialStart,
        NaturalExit,
        Crash,
        ForceKilled,
        PauseResume,
        ManualStart
    }

    public sealed class WatchdogRuntime : IDisposable
    {
        private readonly object _childLock = new object();
        private readonly object _logLock = new object();
        private readonly WatchdogOptions _o;

        private Process _child;
        private Thread _monitorThread;
        private Thread _pipeThread;
        private WatchdogLogPipeServer _logPipe;
        private Mutex _instanceMutex;

        // -------------------------
        // Global hotkeys (WinAPI)
        // -------------------------
        private Thread _hotkeyThread;
        private IntPtr _hotkeyHwnd;
        private volatile bool _hotkeysEnabled = true;

        // Hotkey IDs
        private const int HK_PAUSE = 1;
        private const int HK_RESUME = 2;
        private const int HK_STOP = 3;

        // Ctrl+Alt+P / R / Q
        private const uint HK_MOD = Win32.MOD_CONTROL | Win32.MOD_ALT;
        private const uint VK_P = 0x50;
        private const uint VK_R = 0x52;
        private const uint VK_Q = 0x51;

        private readonly Stopwatch _uptime = Stopwatch.StartNew();
        private Stopwatch _childUptime;
        private long _restartCount;

        private volatile bool _enabled = true;
        private volatile bool _exiting;
        private volatile bool _shutdownRequested;

        private volatile KillOutcome _lastKillOutcome = KillOutcome.None;
        private volatile ExitReason _lastExitReason = ExitReason.None;
        private volatile RestartReason _lastRestartReason = RestartReason.None;

        private Action<string> _LogEmitted;
        public event Action<string> LogEmitted
        {
            add { _LogEmitted += value; }
            remove { _LogEmitted -= value; }
        }

        public WatchdogRuntime(WatchdogOptions options)
        {
            _o = options ?? throw new ArgumentNullException(nameof(options));
            _o.Normalize();
        }

        public void Start()
        {
            _instanceMutex = new Mutex(
                true,
                @"Global\NekoLib.Watchdog::" + _o.PipeName,
                out bool created);

            if (!created)
                throw new InvalidOperationException("Watchdog already running.");

            _lastRestartReason = RestartReason.InitialStart;

            _logPipe = new WatchdogLogPipeServer(_o.PipeName + ".logs");
            _logPipe.Start();

            Log("[watchdog_start]");

            _monitorThread = new Thread(MonitorLoop)
            {
                IsBackground = true,
                Name = "WDG-Monitor"
            };
            _monitorThread.Start();

            _pipeThread = new Thread(PipeLoop)
            {
                IsBackground = true,
                Name = "WDG-Pipe"
            };
            _pipeThread.Start();

            _hotkeyThread = new Thread(HotkeyLoop)
            {
                IsBackground = true,
                Name = "WDG-Hotkeys"
            };
            _hotkeyThread.Start();
        }

        public void WaitForExit()
        {
            var thread = _monitorThread;
            if (thread == null)
                return;

            try
            {
                if (thread.IsAlive)
                    thread.Join();
            }
            catch (ThreadStateException)
            {
                // Thread already stopped
            }
            catch
            {
                // ignore
            }
        }

        // ============================================================
        // Child start / kill
        // ============================================================

        private Process StartChildProcess()
        {
            // MUST match WatchdogEntrypoint.ResolveMode
            var psi = new ProcessStartInfo
            {
                FileName = _o.TargetPath,
                WorkingDirectory = _o.WorkingDirectory,
                Arguments = "--under-watchdog",
                UseShellExecute = true
            };

            return Process.Start(psi);
        }

        private KillOutcome TryKill(Process child)
        {
            if (child == null)
                return KillOutcome.None;

            try
            {
                bool exited;
                try { exited = child.HasExited; }
                catch { exited = true; }

                if (exited)
                    return KillOutcome.None;

                // Try graceful close (best-effort)
                try { child.CloseMainWindow(); } catch { }

                if (child.WaitForExit(_o.GracefulKillTimeoutMs))
                {
                    Log("[kill] graceful success");
                    return KillOutcome.GracefulSuccess;
                }

                Log("[kill] graceful timeout");

                // Force kill
                try { child.Kill(); } catch { }

                if (child.WaitForExit(_o.ForceKillTimeoutMs))
                {
                    Log("[kill] force success");
                    return KillOutcome.ForceSuccess;
                }

                Log("[kill] force timeout");
                return KillOutcome.ForceTimeout;
            }
            catch (Exception ex)
            {
                Log("[kill] error " + ex.Message);
                return KillOutcome.Error;
            }
        }

        // ============================================================
        // Global Hotkeys (WinAPI RegisterHotKey)
        // ============================================================
        private void HotkeyLoop()
        {
            try
            {
                _hotkeyHwnd = Win32.CreateMessageOnlyWindow();
                if (_hotkeyHwnd == IntPtr.Zero)
                {
                    Log("[hotkey] failed to create message-only window");
                    return;
                }

                bool ok1 = Win32.RegisterHotKey(_hotkeyHwnd, HK_PAUSE, HK_MOD, VK_P);
                bool ok2 = Win32.RegisterHotKey(_hotkeyHwnd, HK_RESUME, HK_MOD, VK_R);
                bool ok3 = Win32.RegisterHotKey(_hotkeyHwnd, HK_STOP, HK_MOD, VK_Q);

                Log("[hotkey] register Ctrl+Alt+P=" + ok1 + " Ctrl+Alt+R=" + ok2 + " Ctrl+Alt+Q=" + ok3);

                while (!_exiting && _hotkeysEnabled)
                {
                    Win32.MSG msg;
                    if (!Win32.GetMessage(out msg, IntPtr.Zero, 0, 0))
                        break; // WM_QUIT

                    if (msg.message == Win32.WM_HOTKEY)
                    {
                        int id = (int)msg.wParam;

                        switch (id)
                        {
                            case HK_PAUSE:
                                Log("[hotkey] pause");
                                _enabled = false;
                                _lastExitReason = ExitReason.Pause;
                                _lastRestartReason = RestartReason.PauseResume;
                                break;

                            case HK_RESUME:
                                Log("[hotkey] resume");
                                _enabled = true;
                                _shutdownRequested = false;
                                _lastRestartReason = RestartReason.PauseResume;
                                break;

                            case HK_STOP:
                                Log("[hotkey] stop");
                                _shutdownRequested = true;
                                _exiting = true;
                                _lastExitReason = ExitReason.Stop;

                                // Kill child on stop
                                lock (_childLock)
                                {
                                    if (_child != null)
                                        _lastKillOutcome = TryKill(_child);
                                }
                                break;
                        }
                    }

                    Win32.TranslateMessage(ref msg);
                    Win32.DispatchMessage(ref msg);
                }
            }
            catch (Exception ex)
            {
                Log("[hotkey] error " + ex.Message);
            }
            finally
            {
                try { if (_hotkeyHwnd != IntPtr.Zero) Win32.UnregisterHotKey(_hotkeyHwnd, HK_PAUSE); } catch { }
                try { if (_hotkeyHwnd != IntPtr.Zero) Win32.UnregisterHotKey(_hotkeyHwnd, HK_RESUME); } catch { }
                try { if (_hotkeyHwnd != IntPtr.Zero) Win32.UnregisterHotKey(_hotkeyHwnd, HK_STOP); } catch { }

                try
                {
                    if (_hotkeyHwnd != IntPtr.Zero)
                        Win32.DestroyWindow(_hotkeyHwnd);
                }
                catch { }

                _hotkeyHwnd = IntPtr.Zero;
            }
        }

        // ============================================================
        // Pipe control
        // ============================================================
        private void PipeLoop()
        {
            while (!_exiting)
            {
                try
                {
                    using (var server = new NamedPipeServerStream(
                        _o.PipeName,
                        PipeDirection.InOut,
                        1,
                        PipeTransmissionMode.Message))
                    {
                        server.WaitForConnection();

                        using (var reader = new StreamReader(server, Encoding.UTF8, true, 1024, leaveOpen: true))
                        using (var writer = new StreamWriter(server, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true })
                        {
                            var cmd = reader.ReadLine();
                            var resp = HandleCommand(cmd);
                            writer.WriteLine(resp ?? "");
                        }
                    }
                }
                catch
                {
                    Thread.Sleep(100);
                }
            }
        }

        private string HandleCommand(string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd))
                return "empty";

            cmd = cmd.Trim().ToLowerInvariant();

            switch (cmd)
            {
                case "ping":
                    return "pong";

                case "status":
                    return GetStatus();

                case "pause":
                    Log("[cmd] pause");
                    _enabled = false;
                    _lastExitReason = ExitReason.Pause;
                    _lastRestartReason = RestartReason.PauseResume;
                    return "paused";

                case "resume":
                case "start":
                    Log("[cmd] resume");
                    _enabled = true;
                    _shutdownRequested = false;
                    _lastRestartReason = RestartReason.PauseResume;
                    return "running";

                case "restart":
                    Log("[cmd] restart");
                    _lastExitReason = ExitReason.Restart;
                    _lastRestartReason = RestartReason.ManualStart;
                    lock (_childLock)
                    {
                        if (_child != null)
                            _lastKillOutcome = TryKill(_child);

                        _child = null;
                        _childUptime = null;
                    }
                    return "restarting";

                case "stop":
                    Log("[cmd] stop");
                    _shutdownRequested = true;
                    _exiting = true;

                    lock (_childLock)
                    {
                        if (_child != null)
                            _lastKillOutcome = TryKill(_child);

                        _child = null;
                        _childUptime = null;
                    }

                    _lastExitReason = ExitReason.Stop;
                    return "stopped";

                default:
                    return "unknown";
            }
        }

        private string GetStatus()
        {
            var state =
                _exiting || _shutdownRequested ? "stopped" :
                !_enabled ? "paused" :
                "running";

            var childUptimeMs =
                _childUptime != null ? _childUptime.ElapsedMilliseconds : 0;

            return
                "state=" + state +
                ";uptimeMs=" + _uptime.ElapsedMilliseconds +
                ";childUptimeMs=" + childUptimeMs +
                ";restartCount=" + _restartCount +
                ";exitReason=" + _lastExitReason +
                ";restartReason=" + _lastRestartReason +
                ";killOutcome=" + _lastKillOutcome;
        }

        // ============================================================
        // Monitor loop (restored features)
        // ============================================================
        private void MonitorLoop()
        {
            Log("[monitor] started");

            while (!_exiting)
            {
                // Hard stop requested
                if (_shutdownRequested)
                {
                    _lastExitReason = ExitReason.Stop;
                    break;
                }

                // Paused state: do not start/restart child
                if (!_enabled)
                {
                    _lastExitReason = ExitReason.Pause;
                    Thread.Sleep(Math.Max(50, _o.MonitorPollMs));
                    continue;
                }

                // Start/restart child if needed
                EnsureChildRunning();

                // Heartbeat interval
                Thread.Sleep(Math.Max(
                    50,
                    _o.HeartbeatIntervalMs > 0 ? _o.HeartbeatIntervalMs : _o.MonitorPollMs));

                Process child;
                Stopwatch cu;
                lock (_childLock)
                {
                    child = _child;
                    cu = _childUptime;
                }

                if (child == null)
                {
                    Log("[hb] no_child");
                    continue;
                }

                bool exited;
                try { exited = child.HasExited; }
                catch { exited = true; }

                if (exited)
                {
                    HandleObservedExit(child);
                    Thread.Sleep(Math.Max(250, _o.RestartDelayMs));
                    continue;
                }

                var ms = cu != null ? cu.ElapsedMilliseconds : 0;
                Log("[hb] child_alive pid=" + SafePid(child) + " childUptimeMs=" + ms + " restartCount=" + _restartCount);
            }

            Log("[monitor] exit");
        }

        private void EnsureChildRunning()
        {
            lock (_childLock)
            {
                if (_child != null)
                    return;

                try
                {
                    _child = StartChildProcess();
                    _childUptime = Stopwatch.StartNew();
                    _restartCount++;

                    // Keep a sane reason
                    if (_restartCount == 1 && _lastRestartReason == RestartReason.None)
                        _lastRestartReason = RestartReason.InitialStart;

                    Log("[child_start] pid=" + SafePid(_child) + " restartCount=" + _restartCount);
                }
                catch (Exception ex)
                {
                    Log("[child_start] failed: " + ex.Message);
                    Thread.Sleep(Math.Max(250, _o.RestartDelayMs));
                }
            }
        }

        private void HandleObservedExit(Process child)
        {
            int exitCode = -1;
            bool gotExitCode = false;

            try
            {
                exitCode = child.ExitCode;
                gotExitCode = true;
            }
            catch { }

            lock (_childLock)
            {
                try { _childUptime?.Stop(); } catch { }
                _childUptime = null;
                _child = null;
            }

            if (_shutdownRequested || _exiting)
            {
                _lastExitReason = ExitReason.Stop;
                Log("[child_exit] stop");
                return;
            }

            if (_lastExitReason == ExitReason.Restart)
            {
                Log("[child_exit] restart");
                return;
            }

            if (gotExitCode && exitCode == 0)
            {
                _lastExitReason = ExitReason.NaturalExit;
                _lastRestartReason = RestartReason.NaturalExit;
                Log("[child_exit] natural code=0");
            }
            else
            {
                _lastExitReason = ExitReason.Crash;
                _lastRestartReason = RestartReason.Crash;
                Log("[child_exit] crash code=" + exitCode);
            }
        }

        private static int SafePid(Process p)
        {
            try { return p != null ? p.Id : -1; } catch { return -1; }
        }

        // ============================================================
        // Logging
        // ============================================================
        private void Log(string msg)
        {
            var line =
                "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " +
                msg;

            try { _LogEmitted?.Invoke(line); } catch { }
            WatchdogLog.Emit(line);
            try { _logPipe?.Enqueue(line); } catch { }

            if (!_o.EnableFileLogging)
                return;

            try
            {
                lock (_logLock)
                {
                    RotateLogIfNeeded();
                    File.AppendAllText(_o.LogPath, line + Environment.NewLine);
                }
            }
            catch { }
        }

        private void RotateLogIfNeeded()
        {
            try
            {
                var fi = new FileInfo(_o.LogPath);
                if (!fi.Exists || fi.Length < _o.MaxLogBytes)
                    return;

                var rotated = _o.LogPath + ".1";

                try { if (File.Exists(rotated)) File.Delete(rotated); } catch { }
                try { File.Move(_o.LogPath, rotated); } catch { }
            }
            catch { }
        }

        // ============================================================
        // Cleanup
        // ============================================================
        public void Dispose()
        {
            _shutdownRequested = true;
            _exiting = true;
            _hotkeysEnabled = false;

            // Best-effort: stop child
            try
            {
                lock (_childLock)
                {
                    if (_child != null)
                    {
                        _lastKillOutcome = TryKill(_child);
                        _child = null;
                        _childUptime = null;
                    }
                }
            }
            catch { }

            try { _monitorThread?.Join(1500); } catch { }
            try { _pipeThread?.Join(1500); } catch { }

            // Stop hotkey loop (send WM_QUIT)
            try
            {
                if (_hotkeyThread != null && _hotkeyThread.IsAlive)
                {
                    Win32.PostThreadMessage((uint)_hotkeyThread.ManagedThreadId, Win32.WM_QUIT, UIntPtr.Zero, IntPtr.Zero);
                    _hotkeyThread.Join(1500);
                }
            }
            catch { }

            try { _logPipe?.Dispose(); } catch { }

            try
            {
                _instanceMutex?.ReleaseMutex();
                _instanceMutex?.Dispose();
            }
            catch { }
        }
    }

    internal static class Win32
    {
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);

        public const int SW_RESTORE = 9;

        public const int WM_HOTKEY = 0x0312;
        public const int WM_QUIT = 0x0012;

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;

        private const int HWND_MESSAGE = -3;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        public static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        public static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateWindowExW(
            int dwExStyle,
            [MarshalAs(UnmanagedType.LPWStr)] string lpClassName,
            [MarshalAs(UnmanagedType.LPWStr)] string lpWindowName,
            int dwStyle,
            int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool PostThreadMessage(uint idThread, uint Msg, UIntPtr wParam, IntPtr lParam);

        public static IntPtr CreateMessageOnlyWindow()
        {
            // Use a built-in class name that exists ("STATIC") to avoid class registration.
            return CreateWindowExW(
                0,
                "STATIC",
                "",
                0,
                0, 0, 0, 0,
                new IntPtr(HWND_MESSAGE),
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public UIntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }
    }

    internal sealed class WatchdogLogPipeServer : IDisposable
    {
        private sealed class Client
        {
            public NamedPipeServerStream Pipe;
            public StreamWriter Writer;
        }

        private readonly string _pipeName;
        private readonly object _lock = new object();
        private readonly List<Client> _clients = new List<Client>();

        private readonly BlockingCollection<string> _queue =
            new BlockingCollection<string>(2048);

        private volatile bool _exiting;
        private Thread _acceptThread;
        private Thread _dispatchThread;

        public WatchdogLogPipeServer(string pipeName)
        {
            if (string.IsNullOrWhiteSpace(pipeName))
                throw new ArgumentException(nameof(pipeName));

            _pipeName = pipeName.Trim();
        }

        public void Start()
        {
            if (_acceptThread != null) return;

            _acceptThread = new Thread(AcceptLoop)
            { IsBackground = true, Name = "WDG-LogPipe-Accept" };

            _dispatchThread = new Thread(DispatchLoop)
            { IsBackground = true, Name = "WDG-LogPipe-Dispatch" };

            _acceptThread.Start();
            _dispatchThread.Start();
        }

        public void Enqueue(string line)
        {
            if (_exiting || string.IsNullOrEmpty(line))
                return;

            // never block watchdog: drop if full
            try { _queue.TryAdd(line); } catch { }
        }

        private void AcceptLoop()
        {
            while (!_exiting)
            {
                NamedPipeServerStream pipe = null;

                try
                {
                    pipe = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.Out,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Message,
                        PipeOptions.Asynchronous);

                    pipe.WaitForConnection();

                    var writer = new StreamWriter(pipe, new UTF8Encoding(false))
                    { AutoFlush = true };

                    lock (_lock)
                        _clients.Add(new Client { Pipe = pipe, Writer = writer });

                    pipe = null; // ownership transferred
                }
                catch
                {
                    try { pipe?.Dispose(); } catch { }
                    Thread.Sleep(100);
                }
            }
        }

        private void DispatchLoop()
        {
            while (!_exiting)
            {
                string line;

                try { line = _queue.Take(); }
                catch
                {
                    if (_exiting) break;
                    Thread.Sleep(25);
                    continue;
                }

                List<Client> snapshot;
                lock (_lock)
                    snapshot = new List<Client>(_clients);

                for (int i = 0; i < snapshot.Count; i++)
                {
                    var c = snapshot[i];
                    try
                    {
                        if (c?.Pipe == null || !c.Pipe.IsConnected)
                            throw new IOException("disconnected");

                        c.Writer.WriteLine(line);
                    }
                    catch
                    {
                        lock (_lock)
                            _clients.Remove(c);

                        try { c.Writer?.Dispose(); } catch { }
                        try { c.Pipe?.Dispose(); } catch { }
                    }
                }
            }
        }

        public void Dispose()
        {
            _exiting = true;

            try { _queue.CompleteAdding(); } catch { }

            try { _acceptThread?.Join(800); } catch { }
            try { _dispatchThread?.Join(800); } catch { }

            lock (_lock)
            {
                for (int i = 0; i < _clients.Count; i++)
                {
                    var c = _clients[i];
                    try { c.Writer?.Dispose(); } catch { }
                    try { c.Pipe?.Dispose(); } catch { }
                }
                _clients.Clear();
            }

            try { _queue.Dispose(); } catch { }
        }
    }

    public static class WatchdogLog
    {
        public static event Action<string> OnLog;

        internal static void Emit(string msg)
        {
            try { OnLog?.Invoke(msg); }
            catch { }
        }
    }
}
