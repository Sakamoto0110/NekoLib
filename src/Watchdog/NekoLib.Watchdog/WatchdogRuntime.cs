using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using NekoLib.Diagnostics.Contracts;
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
        private const int MaxQueuedEvents = 1024;

        private readonly WatchdogOptions _o;

        private Process _child;
        private Thread _monitorThread;
        private Thread _hotkeyThread;
        private Thread _eventThread;
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
        private string _lastRestartReason = "startup";
        private volatile bool _crashNotificationReceived;

        private volatile bool _enabled = true;
        private volatile bool _shutdownRequested;
        private volatile bool _exiting;
        private volatile bool _started;
        private volatile bool _stopped;

        // Buffered structured log entries (replay on subscribe)
        private readonly Queue<LogEntry> _logBuffer = new Queue<LogEntry>(MaxBufferedLogs);
        private readonly BlockingCollection<QueuedEvent> _eventQueue =
            new BlockingCollection<QueuedEvent>(MaxQueuedEvents);

        private sealed class QueuedEvent
        {
            public string Name;
            public object Payload;
        }

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
            {
                if (_o.BringToFrontOnStartIfRunning)
                    TryBringExistingTargetToFront();

                throw new InvalidOperationException(
                    $"Watchdog already running for target: {_o.TargetPath}");
            }

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

            _eventThread = new Thread(EventLoop)
            {
                IsBackground = true,
                Name = "WDG-Events"
            };
            _eventThread.Start();

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

                try { _eventQueue.CompleteAdding(); } catch { }
                try { _eventThread?.Join(1500); } catch { }

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
                _lastRestartReason = "command_restart";

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
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        Thread.Sleep(100);
                        Stop(true);
                    }
                    catch { }
                });
                return PipeOk("stopped");
            });

            _rpc.Map("update", async (req, ct) =>
            {
                if (!_o.EnableUpdates)
                    return PipeErrorResponse("updates_disabled", "Updates are disabled.");

                return PipeErrorResponse("not_implemented", "Watchdog update orchestration is not implemented yet.");
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

                _crashNotificationReceived = true;
                _lastRestartReason = "exception_notify";

                var formatted = $"Crash detected: {type} - {message} (source={source})";

                LogError(formatted, new
                {
                    type,
                    message,
                    source
                });

                return PipeOk("ok");
            });

            _rpc.Map("log_write", async (req, ct) =>
            {
#if NET9
                var root = req.Data.Value;
                string level = root.TryGetProperty("level", out var l) ? l.GetString() : null;
                string message = root.TryGetProperty("message", out var m) ? m.GetString() : null;
                string category = root.TryGetProperty("category", out var c) ? c.GetString() : null;
#else
                string level = req.Data?["level"]?.ToString();
                string message = req.Data?["message"]?.ToString();
                string category = req.Data?["category"]?.ToString();
#endif
                Log(LogSeverity.info, string.IsNullOrWhiteSpace(message) ? "[external_log]" : message,
                    new { level, category });

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
                restartReason = _lastRestartReason,
                childPid = (_child != null && !_child.HasExited) ? _child.Id : (int?)null,
                lastExitCode = _lastExitCode,
                updates = new
                {
                    enabled = _o.EnableUpdates,
                    stagingRoot = _o.UpdateStagingRoot
                },
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

        private PipeMessage PipeErrorResponse(string code, string message)
        {
            return new PipeMessage
            {
                Ok = false,
                Error = new PipeError
                {
                    Code = code,
                    Message = message
                }
            };
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
                    Thread.Sleep(_o.MonitorPollMs);
                    continue;
                }

                StartChild();

                var start = DateTime.UtcNow;
                var nextHeartbeat = _o.HeartbeatIntervalMs > 0
                    ? DateTime.UtcNow.AddMilliseconds(_o.HeartbeatIntervalMs)
                    : DateTime.MaxValue;

                while (!_shutdownRequested)
                {
                    Process current;
                    lock (_childLock)
                        current = _child;

                    if (current == null)
                        break;

                    bool exited = false;
                    try { exited = current.WaitForExit(_o.MonitorPollMs); }
                    catch { exited = true; }

                    if (exited)
                        break;

                    if (_o.HeartbeatIntervalMs > 0 && DateTime.UtcNow >= nextHeartbeat)
                    {
                        LogInfo("[heartbeat]", new { pid = current.Id, uptimeMs = _childUptime?.ElapsedMilliseconds });
                        PublishTelemetry();
                        nextHeartbeat = DateTime.UtcNow.AddMilliseconds(_o.HeartbeatIntervalMs);
                    }
                }

                if (_shutdownRequested)
                    break;

                var runtime = DateTime.UtcNow - start;

                _lastExitCode = _child?.ExitCode;

                LogWarn("[child_exit]", new
                {
                    code = _lastExitCode,
                    uptimeSec = runtime.TotalSeconds
                });

                TryFinalizeCrashBundle(_lastRestartReason, "child_exit");

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

                SleepWithShutdown(_o.RestartDelayMs);

                PublishTelemetry();
            }

            LogInfo("[monitor] exit");
        }

        private void PublishTelemetry()
        {
            var telemetry = BuildTelemetry();
            QueueEvent("telemetry", telemetry);
            TrackTelemetry("watchdog.telemetry", telemetry);
        }

        private void QueueEvent(string name, object payload)
        {
            if (_eventQueue.IsAddingCompleted)
                return;

            try
            {
                _eventQueue.TryAdd(new QueuedEvent
                {
                    Name = name,
                    Payload = payload
                });
            }
            catch { }
        }

        private void EventLoop()
        {
            foreach (var evt in _eventQueue.GetConsumingEnumerable())
            {
                try
                {
                    using (var publishCts = new CancellationTokenSource(1000))
                    {
                        _rpc?.Events?.PublishAsync(evt.Name, evt.Payload, publishCts.Token)
                            .GetAwaiter()
                            .GetResult();
                    }
                }
                catch { }
            }
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

        private void TryFinalizeCrashBundle(string restartReason, string fallbackReason)
        {
            if (!_o.EnableCrashBundling)
                return;

            try
            {
                var reason = _crashNotificationReceived
                    ? restartReason
                    : fallbackReason;

                CrashBundler.TryFinalizeLatestCrashBundle(
                    new CrashBundlerOptions
                    {
                        PendingCrashRoot = _o.PendingCrashRoot,
                        BundleRoot = _o.BundleRoot,
                        MaxBundles = _o.MaxBundles,
                        EnableChecksums = _o.EnableBundleChecksums,
                        EnableManifests = _o.EnableBundleManifests,
                        WatchdogLogPath = _o.LogPath,
                        CopyWatchdogLogTail = _o.EnableFileLogging,
                        GetWatchdogStatus = () => SafeStatusText(),
                        GetWatchdogVersion = () => typeof(WatchdogRuntime).Assembly.GetName().Version?.ToString()
                    },
                    reason,
                    _restartCount,
                    line => LogInfo(line));

                _crashNotificationReceived = false;
            }
            catch { }
        }

        private string SafeStatusText()
        {
            try
            {
#if NET9
                return JsonSerializer.Serialize(BuildTelemetry());
#else
                return Newtonsoft.Json.JsonConvert.SerializeObject(BuildTelemetry());
#endif
            }
            catch { return null; }
        }

        private void SleepWithShutdown(int delayMs)
        {
            var remaining = delayMs;
            while (remaining > 0 && !_shutdownRequested)
            {
                var slice = Math.Min(_o.MonitorPollMs, remaining);
                Thread.Sleep(slice);
                remaining -= slice;
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
                    })?.WaitForExit(_o.ForceKillTimeoutMs);
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
            QueueEvent("log", entry);
            WriteDiagnosticsLog(sev, msg);
            TrackTelemetry("watchdog.log", new { level = sev.ToString(), message = msg, meta });

            // file logging
            if (_o.EnableFileLogging)
            {
                WriteLogFile(line);
            }
        }

        private void WriteDiagnosticsLog(LogSeverity sev, string msg)
        {
            var sinks = _o.LogSinks;
            if (sinks == null || sinks.Length == 0)
                return;

            var level = ToLogLevel(sev);
            var entry = new NekoLib.Diagnostics.Contracts.LogEntry(
                DateTime.UtcNow,
                level,
                "watchdog",
                msg);

            for (int i = 0; i < sinks.Length; i++)
            {
                try { sinks[i]?.Write(entry); }
                catch { }
            }
        }

        private static LogLevel ToLogLevel(LogSeverity sev)
        {
            switch (sev)
            {
                case LogSeverity.warn: return LogLevel.Warn;
                case LogSeverity.error: return LogLevel.Error;
                default: return LogLevel.Info;
            }
        }

        private void TrackTelemetry(string name, object payload)
        {
            var sink = _o.TelemetrySink;
            if (sink == null)
                return;

            try
            {
                var data = new Dictionary<string, object>
                {
                    { "payload", payload }
                };

                sink.Track(new TelemetryEvent(DateTime.UtcNow, name, data: data));
            }
            catch { }
        }

        private void WriteLogFile(string line)
        {
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
                if (string.IsNullOrWhiteSpace(_o.LogPath) || !File.Exists(_o.LogPath))
                    return;

                var info = new FileInfo(_o.LogPath);
                if (info.Length < _o.MaxLogBytes)
                    return;

                var rotated = _o.LogPath + ".1";
                try { if (File.Exists(rotated)) File.Delete(rotated); } catch { }
                try { File.Move(_o.LogPath, rotated); } catch { }
            }
            catch { }
        }

        private void TryBringExistingTargetToFront()
        {
            try
            {
                var exeName = Path.GetFileNameWithoutExtension(_o.TargetPath);
                if (string.IsNullOrWhiteSpace(exeName))
                    return;

                foreach (var p in Process.GetProcessesByName(exeName))
                {
                    try
                    {
                        if (p.MainWindowHandle == IntPtr.Zero)
                            continue;

                        Win32.ShowWindow(p.MainWindowHandle, Win32.SW_RESTORE);
                        Win32.SetForegroundWindow(p.MainWindowHandle);
                        return;
                    }
                    catch { }
                }
            }
            catch { }
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
            public const int SW_RESTORE = 9;

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

            [DllImport("user32.dll")]
            public static extern bool SetForegroundWindow(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

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
