using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Pipes;
using NekoLib.Pipes.Internal;

namespace NekoLib.Runtime.Watchdog
{
    public sealed class WatchdogRuntime : IDisposable
    {
        private readonly object _childLock = new object();
        private readonly object _logLock = new object();
        private readonly WatchdogOptions _o;

        private Process _child;
        private Thread _monitorThread;
        private Thread _hotkeyThread;
        private Mutex _instanceMutex;

        private PipeServer _rpc;
        private readonly IPipeMetrics _pipeMetrics;

        private IntPtr _hotkeyHwnd;
        private volatile bool _hotkeysEnabled = true;

        private readonly Stopwatch _uptime = Stopwatch.StartNew();
        private Stopwatch _childUptime;
        private long _restartCount;

        private volatile bool _enabled = true;
        private volatile bool _exiting;
        private volatile bool _shutdownRequested;

        public WatchdogRuntime(WatchdogOptions options)
        {
            _o = options ?? throw new ArgumentNullException(nameof(options));
            _o.Normalize();
            _pipeMetrics = new SimplePipeMetrics();
        }

        // ============================================================
        // START
        // ============================================================

        public void Start()
        {
            _instanceMutex = new Mutex(
                true,
                @"Global\NekoLib.Watchdog::" + _o.PipeName,
                out bool created);

            if (!created)
                throw new InvalidOperationException("Watchdog already running.");

            // --- RPC PIPE ---
            

            _rpc = new PipeServer(new PipeServerOptions
            {
                PipeName = _o.PipeName,
                EnableEvents = true,
                MaxClients = 8,
                MaxEventSubscribers = 16,
                Metrics = _pipeMetrics
                
            });

            RegisterRpcHandlers();
            _rpc.Start();

            Log("[watchdog_start]");

            // --- MONITOR ---
            _monitorThread = new Thread(MonitorLoop)
            {
                IsBackground = true,
                Name = "WDG-Monitor"
            };
            _monitorThread.Start();

            // --- HOTKEYS ---
            _hotkeyThread = new Thread(HotkeyLoop)
            {
                IsBackground = true,
                Name = "WDG-Hotkeys"
            };
            _hotkeyThread.Start();
        }

        // ============================================================
        // RPC HANDLERS
        // ============================================================
        private string GetState()=> (_exiting || _shutdownRequested) ? "stopped"
                           : (!_enabled ? "paused" : "running");
        private object BuildStatus()
        {
            var snap = _pipeMetrics.Snapshot();
            var uptime = _uptime.ElapsedMilliseconds;
            var ts = _uptime.Elapsed;

            string uptimeStr =
                ts.TotalHours >= 1
                    ? $"{(int)ts.TotalHours}h {ts.Minutes}m {ts.Seconds}s"
                    : ts.TotalMinutes >= 1
                        ? $"{(int)ts.TotalMinutes}m {ts.Seconds}s"
                        : ts.TotalSeconds >= 1
                            ? $"{ts.TotalSeconds:F1}s"
                            : $"{ts.TotalMilliseconds:F0}ms";


            return new
            {
                state = GetState(),
                uptimeMs = uptimeStr,
                restartCount = _restartCount,
                metrics = new
                {
                    server = snap.Server,
                    events = snap.Events,
                    errors = snap.Errors
                }
            };
        }

        private void RegisterRpcHandlers()
        {
            _rpc.Map("ping", async (req, ct) =>
                PipeOk("pong"));

            _rpc.Map("status", async (req, ct) =>
            {
                return PipeOk(BuildStatus());
            });

            _rpc.Map("pause", async (req, ct) =>
            {
                Log("[cmd] pause");
                _enabled = false;
                return PipeOk("paused");
            });

            _rpc.Map("resume", async (req, ct) =>
            {
                Log("[cmd] resume");
                _enabled = true;
                _shutdownRequested = false;
                return PipeOk("running");
            });

            _rpc.Map("restart", async (req, ct) =>
            {
                Log("[cmd] restart");

                lock (_childLock)
                {
                    if (_child != null)
                        TryKill(_child);

                    _child = null;
                    _childUptime = null;
                }

                return PipeOk("restarting");
            });

            _rpc.Map("stop", async (req, ct) =>
            {
                Log("[cmd] stop");

                _shutdownRequested = true;
                _exiting = true;

                lock (_childLock)
                {
                    if (_child != null)
                        TryKill(_child);
                }

                return PipeOk("stopped");
            });
        }

        private PipeMessage PipeOk(object payload)
        {
#if NET9
            return new PipeMessage
            {
                Ok = true,
                Data = System.Text.Json.JsonSerializer.SerializeToElement(payload)
            };
#else
            return new PipeMessage
            {
                Ok = true,
                Data = Newtonsoft.Json.Linq.JToken.FromObject(payload)
            };
#endif
        }

        // ============================================================
        // MONITOR LOOP
        // ============================================================

        private void MonitorLoop()
        {
            Log("[monitor] started");

            while (!_exiting)
            {
                if (_shutdownRequested)
                    break;

                if (!_enabled)
                {
                    Thread.Sleep(250);
                    continue;
                }

                EnsureChildRunning();
                Thread.Sleep(500);

                lock (_childLock)
                {
                    if (_child != null && _child.HasExited)
                    {
                        Log("[child_exit]");
                        _child = null;
                        _childUptime = null;
                        Thread.Sleep(_o.RestartDelayMs);
                    }
                }
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
                    var psi = new ProcessStartInfo
                    {
                        FileName = _o.TargetPath,
                        WorkingDirectory = _o.WorkingDirectory,
                        Arguments = "--under-watchdog",
                        UseShellExecute = true
                    };

                    _child = Process.Start(psi);
                    _childUptime = Stopwatch.StartNew();
                    _restartCount++;

                    Log("[child_start] pid=" + _child.Id);
                }
                catch (Exception ex)
                {
                    Log("[child_start] failed: " + ex.Message);
                }
            }
        }

        private void TryKill(Process p)
        {
            try
            {
                if (!p.HasExited)
                {
                    p.CloseMainWindow();
                    if (!p.WaitForExit(_o.GracefulKillTimeoutMs))
                        p.Kill();
                }
            }
            catch { }
        }

        // ============================================================
        // HOTKEYS
        // ============================================================

        private const int HK_PAUSE = 1;
        private const int HK_RESUME = 2;
        private const int HK_STOP = 3;

        private const uint HK_MOD = Win32.MOD_CONTROL | Win32.MOD_ALT;
        private const uint VK_P = 0x50;
        private const uint VK_R = 0x52;
        private const uint VK_Q = 0x51;

        private void HotkeyLoop()
        {
            _hotkeyHwnd = Win32.CreateMessageOnlyWindow();

            Win32.RegisterHotKey(_hotkeyHwnd, HK_PAUSE, HK_MOD, VK_P);
            Win32.RegisterHotKey(_hotkeyHwnd, HK_RESUME, HK_MOD, VK_R);
            Win32.RegisterHotKey(_hotkeyHwnd, HK_STOP, HK_MOD, VK_Q);

            while (!_exiting)
            {
                Win32.MSG msg;
                if (!Win32.GetMessage(out msg, IntPtr.Zero, 0, 0))
                    break;

                if (msg.message == Win32.WM_HOTKEY)
                {
                    int id = (int)msg.wParam;

                    if (id == HK_PAUSE)
                        _enabled = false;
                    else if (id == HK_RESUME)
                        _enabled = true;
                    else if (id == HK_STOP)
                    {
                        _shutdownRequested = true;
                        _exiting = true;
                    }
                }

                Win32.TranslateMessage(ref msg);
                Win32.DispatchMessage(ref msg);
            }
        }

        // ============================================================
        // LOGGING
        // ============================================================

        private void Log(string msg)
        {
            var line =
                "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + msg;

            

            try
            {
                _rpc?.Events?.PublishAsync("log", new { line = line });
            }
            catch { }

            if (!_o.EnableFileLogging)
                return;

            try
            {
                lock (_logLock)
                {
                    File.AppendAllText(_o.LogPath, line + Environment.NewLine);
                }
            }
            catch { }
        }
        public void WaitForExit()
        {
            try { _monitorThread?.Join(); } catch { }
            try { _hotkeyThread?.Join(); } catch { }
        }

        // ============================================================
        // DISPOSE
        // ============================================================

        public void Dispose()
        {
            _shutdownRequested = true;
            _exiting = true;

            try { _rpc?.Dispose(); } catch { }

            try { _monitorThread?.Join(1500); } catch { }

            try
            {
                _instanceMutex?.ReleaseMutex();
                _instanceMutex?.Dispose();
            }
            catch { }
        }
        internal static class Win32
        {
            // ============================================================
            // Window constants
            // ============================================================

            public const int SW_RESTORE = 9;

            public const int WM_HOTKEY = 0x0312;
            public const int WM_QUIT = 0x0012;

            public const uint MOD_ALT = 0x0001;
            public const uint MOD_CONTROL = 0x0002;
            public const uint MOD_SHIFT = 0x0004;
            public const uint MOD_WIN = 0x0008;

            private const int HWND_MESSAGE = -3;

            // ============================================================
            // Foreground helpers (optional use)
            // ============================================================

            [DllImport("user32.dll")]
            public static extern bool SetForegroundWindow(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

            [DllImport("user32.dll")]
            public static extern bool IsIconic(IntPtr hWnd);

            // ============================================================
            // Hotkey API
            // ============================================================

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool RegisterHotKey(
                IntPtr hWnd,
                int id,
                uint fsModifiers,
                uint vk);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool UnregisterHotKey(
                IntPtr hWnd,
                int id);

            // ============================================================
            // Message loop
            // ============================================================

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool GetMessage(
                out MSG lpMsg,
                IntPtr hWnd,
                uint wMsgFilterMin,
                uint wMsgFilterMax);

            [DllImport("user32.dll")]
            public static extern bool TranslateMessage(ref MSG lpMsg);

            [DllImport("user32.dll")]
            public static extern IntPtr DispatchMessage(ref MSG lpMsg);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool PostThreadMessage(
                uint idThread,
                uint Msg,
                UIntPtr wParam,
                IntPtr lParam);

            // ============================================================
            // Message-only window
            // ============================================================

            [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            private static extern IntPtr CreateWindowExW(
                int dwExStyle,
                string lpClassName,
                string lpWindowName,
                int dwStyle,
                int x,
                int y,
                int nWidth,
                int nHeight,
                IntPtr hWndParent,
                IntPtr hMenu,
                IntPtr hInstance,
                IntPtr lpParam);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool DestroyWindow(IntPtr hWnd);

            /// <summary>
            /// Creates a message-only window to receive WM_HOTKEY.
            /// Uses built-in "STATIC" class to avoid registration.
            /// </summary>
            public static IntPtr CreateMessageOnlyWindow()
            {
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

            // ============================================================
            // Structs
            // ============================================================

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
    }
}
