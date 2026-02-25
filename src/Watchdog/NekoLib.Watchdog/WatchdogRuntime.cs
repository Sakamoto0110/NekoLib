using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using NekoLib.Pipes;

 




#if NET9
using System.Text.Json;
#else
using Newtonsoft.Json.Linq;
#endif

namespace NekoLib.Watchdog
{
    public sealed class WatchdogRuntime : IDisposable
    {
        private readonly object _childLock = new object();
        private readonly object _logLock = new object();
        private readonly object _stopLock = new object();
        private readonly object _bufferLock = new object();

        private const int MaxBufferedLogs = 300;

        private readonly WatchdogOptions _o;

        private Process _child;
        private Thread _monitorThread;
        private Thread _hotkeyThread;
        private Mutex _instanceMutex;
        private bool _ownsMutex;

        private PipeServer _rpc;
        private readonly IPipeMetrics _pipeMetrics;

        private IntPtr _hotkeyHwnd;
        private uint _hotkeyThreadId;

        private readonly Stopwatch _uptime = Stopwatch.StartNew();
        private Stopwatch _childUptime;

        private long _restartCount;
        private int? _lastExitCode;

        private volatile bool _enabled = true;
        private volatile bool _shutdownRequested;
        private volatile bool _exiting;
        private volatile bool _started;
        private volatile bool _stopped;

        // Buffered structured log entries (replay on subscribe)
        private readonly Queue<LogEntry> _logBuffer = new Queue<LogEntry>(MaxBufferedLogs);

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
            if (_started)
                throw new InvalidOperationException("Watchdog already started.");

            _started = true;

            var mutexName = @"Global\NekoLib.Watchdog::" + _o.PipeName;

            _instanceMutex = new Mutex(true, mutexName, out bool created);
            _ownsMutex = created;

            if (!created)
                throw new InvalidOperationException(
                    $"Watchdog already running for target: {_o.TargetPath}");

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

            LogInfo("[watchdog_start]", new { target = _o.TargetPath, pipe = _o.PipeName });

            _monitorThread = new Thread(MonitorLoop)
            {
                IsBackground = false,
                Name = "WDG-Monitor"
            };
            _monitorThread.Start();

            _hotkeyThread = new Thread(HotkeyLoop)
            {
                IsBackground = true,
                Name = "WDG-Hotkeys"
            };
            _hotkeyThread.Start();
        }

        public void WaitForExit()
        {
            try { _monitorThread?.Join(); } catch { }
        }

        // ============================================================
        // STOP
        // ============================================================

        public void Stop(bool exitHost = true)
        {
            lock (_stopLock)
            {
                if (_stopped)
                    return;

                _stopped = true;
                _shutdownRequested = true;
                if (exitHost)
                    _exiting = true;

                LogWarn("[stop] requested");

                lock (_childLock)
                {
                    if (_child != null)
                        TryKill(_child);
                }

                // break GetMessage() in hotkey thread
                try
                {
                    if (_hotkeyThreadId != 0)
                        Win32.PostThreadMessage(_hotkeyThreadId, Win32.WM_QUIT, UIntPtr.Zero, IntPtr.Zero);
                }
                catch { }

                try { _rpc?.Dispose(); } catch { }

                try { _monitorThread?.Join(3000); } catch { }
                try { _hotkeyThread?.Join(1000); } catch { }

                try
                {
                    if (_ownsMutex)
                        _instanceMutex?.ReleaseMutex();

                    _instanceMutex?.Dispose();
                }
                catch { }

                LogInfo("[stop] completed");
            }
        }

        // ============================================================
        // RPC
        // ============================================================

        private void RegisterRpcHandlers()
        {
            _rpc.Map("ping", async (req, ct) => PipeOk("pong"));

            _rpc.Map("status", async (req, ct) =>
                PipeOk(BuildTelemetry()));

            _rpc.Map("pause", async (req, ct) =>
            {
                _enabled = false;
                LogInfo("[cmd] pause");
                PublishTelemetry();
                return PipeOk("paused");
            });

            _rpc.Map("resume", async (req, ct) =>
            {
                _enabled = true;
                LogInfo("[cmd] resume");
                PublishTelemetry();
                return PipeOk("running");
            });

            _rpc.Map("restart", async (req, ct) =>
            {
                LogWarn("[cmd] restart");

                lock (_childLock)
                {
                    if (_child != null)
                        TryKill(_child);
                }

                PublishTelemetry();
                return PipeOk("restarting");
            });

            _rpc.Map("stop", async (req, ct) =>
            {
                LogWarn("[cmd] stop");
                Stop(true);
                return PipeOk("stopped");
            });

            // 🔥 Log replay buffer
            _rpc.Map("log_history", async (req, ct) =>
            {
                LogEntry[] history;
                lock (_bufferLock)
                    history = _logBuffer.ToArray();

                return PipeOk(history);
            });
            _rpc.Map("exception_notify", async (req, ct) =>
            {
#if NET9
    var root = req.Data.Value;

    string type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
    string message = root.TryGetProperty("message", out var m) ? m.GetString() : null;
    string source = root.TryGetProperty("source", out var s) ? s.GetString() : null;
#else
                string type = req.Data?["type"]?.ToString();
                string message = req.Data?["message"]?.ToString();
                string source = req.Data?["source"]?.ToString();
#endif

                var formatted = $"Crash detected: {type} - {message} (source={source})";

                LogError(formatted, new
                {
                    type,
                    message,
                    source
                });

                return PipeOk("ok");
            });
        }

        private object BuildTelemetry()
        {
            var snap = _pipeMetrics?.Snapshot();

            return new
            {
                state = GetState(),
                uptimeMs = _uptime.ElapsedMilliseconds,
                childUptimeMs = _childUptime?.ElapsedMilliseconds,
                restartCount = _restartCount,
                childPid = (_child != null && !_child.HasExited) ? _child.Id : (int?)null,
                lastExitCode = _lastExitCode,
                metrics = snap == null ? null : new
                {
                    server = snap.Server,
                    events = snap.Events,
                    errors = snap.Errors
                }
            };
        }

        private string GetState()
        {
            if (_shutdownRequested) return "stopped";
            if (!_enabled) return "paused";
            return "running";
        }

        private PipeMessage PipeOk(object payload)
        {
#if NET9
            return new PipeMessage
            {
                Ok = true,
                Data = JsonSerializer.SerializeToElement(payload)
            };
#else
            return new PipeMessage
            {
                Ok = true,
                Data = JToken.FromObject(payload)
            };
#endif
        }

        // ============================================================
        // MONITOR LOOP
        // ============================================================

        private void MonitorLoop()
        {
            LogInfo("[monitor] started");

            int fastCrashCount = 0;

            while (!_shutdownRequested)
            {
                if (!_enabled)
                {
                    Thread.Sleep(250);
                    continue;
                }

                StartChild();

                var start = DateTime.UtcNow;

                try { _child.WaitForExit(); } catch { }

                if (_shutdownRequested)
                    break;

                var runtime = DateTime.UtcNow - start;

                _lastExitCode = _child?.ExitCode;

                LogWarn("[child_exit]", new
                {
                    code = _lastExitCode,
                    uptimeSec = runtime.TotalSeconds
                });

                lock (_childLock)
                {
                    try { _child?.Dispose(); } catch { }
                    _child = null;
                    _childUptime = null;
                }

                if (runtime.TotalSeconds < 3)
                {
                    fastCrashCount++;
                    if (fastCrashCount >= 5)
                    {
                        LogError("[crash_loop] cooling 10s", new { fastCrashCount });
                        Thread.Sleep(10000);
                        fastCrashCount = 0;
                    }
                }
                else
                {
                    fastCrashCount = 0;
                }

                Thread.Sleep(_o.RestartDelayMs);

                PublishTelemetry();
            }

            LogInfo("[monitor] exit");
        }

        private void PublishTelemetry()
        {
            try
            {
                _rpc?.Events?.PublishAsync("telemetry", BuildTelemetry());
            }
            catch { }
        }

        private void StartChild()
        {
            lock (_childLock)
            {
                if (_child != null && !_child.HasExited)
                    return;

                var psi = new ProcessStartInfo
                {
                    FileName = _o.TargetPath,
                    Arguments = _o.TargetArguments,
                    WorkingDirectory = _o.WorkingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = false
                };

                psi.Environment["NEKO_UNDER_WATCHDOG"] = "1";

                _child = Process.Start(psi);
                _childUptime = Stopwatch.StartNew();
                _restartCount++;

                LogInfo("[child_start]", new { pid = _child.Id });
            }
        }

        private void TryKill(Process p)
        {
            try
            {
                if (p == null || p.HasExited)
                    return;

                // Graceful
                try
                {
                    p.CloseMainWindow();
                    if (p.WaitForExit(_o.GracefulKillTimeoutMs))
                    {
                        LogInfo("[kill] graceful", new { pid = p.Id });
                        return;
                    }
                }
                catch { }

                // Force kill tree (works on net481 + net9)
                try
                {
                    LogWarn("[kill] taskkill_tree", new { pid = p.Id });

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "taskkill",
                        Arguments = $"/PID {p.Id} /T /F",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    })?.WaitForExit(5000);
                }
                catch { }
            }
            catch { }
        }

        // ============================================================
        // STRUCTURED LOGGING + BUFFER
        // ============================================================

        private enum LogSeverity
        {
            info,
            warn,
            error
        }

        private sealed class LogEntry
        {
            public long tsUnixMs { get; set; }
            public string level { get; set; }    // "info" | "warn" | "error"
            public string msg { get; set; }      // original message
            public object meta { get; set; }     // structured metadata (optional)
            public string line { get; set; }     // convenience formatted line
        }

        private void LogInfo(string msg, object meta = null) => Log(LogSeverity.info, msg, meta);
        private void LogWarn(string msg, object meta = null) => Log(LogSeverity.warn, msg, meta);
        private void LogError(string msg, object meta = null) => Log(LogSeverity.error, msg, meta);

        private void Log(LogSeverity sev, string msg, object meta)
        {
            var now = DateTimeOffset.Now;
            var line = $"[{now:yyyy-MM-dd HH:mm:ss}] [{sev}] {msg}";

            var entry = new LogEntry
            {
                tsUnixMs = now.ToUnixTimeMilliseconds(),
                level = sev.ToString(),
                msg = msg,
                meta = meta,
                line = line
            };

            // buffer for replay
            lock (_bufferLock)
            {
                _logBuffer.Enqueue(entry);
                while (_logBuffer.Count > MaxBufferedLogs)
                    _logBuffer.Dequeue();
            }

            // live event
            try { _rpc?.Events?.PublishAsync("log", entry); }
            catch { }

            // file logging
            if (_o.EnableFileLogging)
            {
                try
                {
                    lock (_logLock)
                        File.AppendAllText(_o.LogPath, line + Environment.NewLine);
                }
                catch { }
            }
        }

        // ============================================================
        // HOTKEYS
        // ============================================================

        private void HotkeyLoop()
        {
            _hotkeyThreadId = Win32.GetCurrentThreadId();
            _hotkeyHwnd = Win32.CreateMessageOnlyWindow();

            Win32.RegisterHotKey(_hotkeyHwnd, 1, Win32.MOD_CONTROL | Win32.MOD_ALT, 0x50); // P
            Win32.RegisterHotKey(_hotkeyHwnd, 2, Win32.MOD_CONTROL | Win32.MOD_ALT, 0x52); // R
            Win32.RegisterHotKey(_hotkeyHwnd, 3, Win32.MOD_CONTROL | Win32.MOD_ALT, 0x51); // Q

            while (!_exiting)
            {
                if (!Win32.GetMessage(out var msg, IntPtr.Zero, 0, 0))
                    break;

                if (msg.message == Win32.WM_HOTKEY)
                {
                    int id = (int)msg.wParam;

                    if (id == 1) { _enabled = false; LogInfo("[hk] pause"); PublishTelemetry(); }
                    else if (id == 2) { _enabled = true; LogInfo("[hk] resume"); PublishTelemetry(); }
                    else if (id == 3) { LogWarn("[hk] stop"); Stop(true); }
                }

                Win32.TranslateMessage(ref msg);
                Win32.DispatchMessage(ref msg);
            }
        }

        // ============================================================
        // DISPOSE
        // ============================================================

        public void Dispose()
        {
            Stop(true);
        }

        // ============================================================
        // WIN32
        // ============================================================

        internal static class Win32
        {
            public const int WM_HOTKEY = 0x0312;
            public const int WM_QUIT = 0x0012;

            public const uint MOD_ALT = 0x0001;
            public const uint MOD_CONTROL = 0x0002;

            private const int HWND_MESSAGE = -3;

            [DllImport("kernel32.dll")]
            public static extern uint GetCurrentThreadId();

            [DllImport("user32.dll")]
            public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

            [DllImport("user32.dll")]
            public static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint min, uint max);

            [DllImport("user32.dll")]
            public static extern bool TranslateMessage(ref MSG msg);

            [DllImport("user32.dll")]
            public static extern IntPtr DispatchMessage(ref MSG msg);

            [DllImport("user32.dll")]
            public static extern bool PostThreadMessage(uint idThread, uint msg, UIntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            private static extern IntPtr CreateWindowExW(
                int exStyle,
                string className,
                string windowName,
                int style,
                int x, int y, int w, int h,
                IntPtr parent,
                IntPtr menu,
                IntPtr instance,
                IntPtr param);

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

            [StructLayout(LayoutKind.Sequential)]
            public struct MSG
            {
                public IntPtr hwnd;
                public uint message;
                public UIntPtr wParam;
                public IntPtr lParam;
                public uint time;
                public int pt_x;
                public int pt_y;
            }
        }
    }
}