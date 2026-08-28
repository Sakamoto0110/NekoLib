using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using NekoLib.Core.Logging;
using NekoLib.Core.Telemetry;
using NekoLib.Pipes;

 




#if NET9
using System.Text.Json;
#else
using Newtonsoft.Json.Linq;
#endif

namespace NekoLib.Watchdog
{
    /// <summary>
    /// Advanced one-shot Windows process supervisor. It owns the current target
    /// process handle, local IPC endpoints, instance semaphore, and runtime
    /// threads, but never disposes caller-supplied sinks or telemetry.
    /// </summary>
    public sealed class WatchdogRuntime : IDisposable
    {
        private readonly object _childLock = new object();
        private readonly object _logLock = new object();
        private readonly object _lifecycleLock = new object();
        private readonly object _bufferLock = new object();

        private const int MaxBufferedLogs = 300;
        private const int MaxQueuedEvents = 1024;

        private readonly WatchdogRuntimeOptions _o;

        private Process? _child;
        private Thread? _monitorThread;
        private Thread? _hotkeyThread;
        private Thread? _eventThread;
        // Single-instance guard. A named Semaphore (not a Mutex) because the permit
        // is released from whatever thread runs Stop() — often a ThreadPool thread
        // from the "stop" RPC, not the thread that acquired it in Start(). Mutex has
        // thread affinity and would throw on release from a foreign thread; Semaphore
        // does not.
        private Semaphore? _instanceLock;
        private bool _ownsInstanceLock;

        private PipeServer? _rpc;

        internal PipeAccessPolicy ControlPipeAccessPolicy =>
            _rpc?.AccessPolicy ?? PipeAccessPolicy.PlatformDefault;
        private readonly IPipeMetrics _pipeMetrics;

        private IntPtr _hotkeyHwnd;
        private uint _hotkeyThreadId;
        private readonly ManualResetEventSlim _hotkeyReady = new ManualResetEventSlim(false);
        private readonly ManualResetEventSlim _stoppedSignal = new ManualResetEventSlim(false);

        private readonly Stopwatch _uptime = Stopwatch.StartNew();
        private Stopwatch? _childUptime;

        private long _restartCount;
        private long _historyEvictions;
        private long _eventQueueDrops;
        private long _eventPublishFailures;
        private bool _hasSupervisedProcess;
        private int? _lastExitCode;
        private string _lastRestartReason = "startup";
        private volatile bool _crashNotificationReceived;

        private volatile bool _enabled = true;
        private volatile bool _shutdownRequested;
        private volatile bool _attachStatusReady;
        private int? _attachedInitialProcessId;
        private LifecycleState _lifecycleState;
        private bool _startedSuccessfully;

        // Buffered structured log entries (replay on subscribe)
        private readonly Queue<LogEntry> _logBuffer = new Queue<LogEntry>(MaxBufferedLogs);
        private readonly BlockingCollection<QueuedEvent> _eventQueue =
            new BlockingCollection<QueuedEvent>(MaxQueuedEvents);

        private sealed class QueuedEvent
        {
            public string Name = "";
            public object Payload = new object();
        }

        private enum LifecycleState
        {
            Created,
            Starting,
            Running,
            Stopping,
            Stopped
        }

        /// <summary>
        /// Validates, normalizes, and captures configuration without starting
        /// supervision or mutating the caller's options object.
        /// </summary>
        /// <param name="options">Required runtime configuration.</param>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">Required target or attach configuration is missing or invalid.</exception>
        /// <exception cref="FileNotFoundException">The target executable does not exist.</exception>
        public WatchdogRuntime(WatchdogOptions options)
        {
            _o = WatchdogRuntimeOptions.Capture(options);
            _pipeMetrics = new SimplePipeMetrics();
        }

        /// <summary>The effective RPC and event pipe identity for this runtime.</summary>
        public string PipeName => _o.PipeName;

        internal WatchdogRuntimeOptions CapturedOptions => _o;
        internal bool IsMonitorThreadAlive => _monitorThread?.IsAlive == true;
        internal bool IsEventThreadAlive => _eventThread?.IsAlive == true;
        internal bool IsHotkeyThreadAlive => _hotkeyThread?.IsAlive == true;
        internal static string SystemTaskkillPath => Path.Combine(
            Environment.SystemDirectory,
            "taskkill.exe");

        // ============================================================
        // START
        // ============================================================

        /// <summary>
        /// Starts the one-shot runtime, claims the per-target instance slot,
        /// attaches or launches the initial process, and starts current-user IPC
        /// and owned worker threads. A failed start performs terminal cleanup.
        /// </summary>
        /// <exception cref="InvalidOperationException">The runtime is not in its created state or another runtime owns the target.</exception>
        public void Start()
        {
            lock (_lifecycleLock)
            {
                if (_lifecycleState != LifecycleState.Created)
                {
                    throw new InvalidOperationException(
                        "WatchdogRuntime can be started only once.");
                }

                _lifecycleState = LifecycleState.Starting;
                try
                {
                    var lockName = @"Global\NekoLib.Watchdog::" + _o.PipeName;
                    _instanceLock = new Semaphore(1, 1, lockName, out bool _);
                    _ownsInstanceLock = _instanceLock.WaitOne(0);

                    if (!_ownsInstanceLock)
                    {
                        if (_o.BringToFrontOnStartIfRunning)
                            TryBringExistingTargetToFront();

                        throw new InvalidOperationException(
                            $"Watchdog already running for target: {_o.TargetPath}");
                    }

                    AttachInitialProcess();

                    _rpc = new PipeServer(new PipeServerOptions
                    {
                        PipeName = _o.PipeName,
                        EnableEvents = true,
                        MaxClients = 8,
                        MaxEventSubscribers = 16,
                        AccessPolicy = PipeAccessPolicy.CurrentUserOnly,
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

                    LogInfo("[watchdog_start]", new
                    {
                        target = _o.TargetPath,
                        pipe = _o.PipeName,
                        attachedPid = _attachedInitialProcessId
                    });

                    _monitorThread = new Thread(MonitorLoop)
                    {
                        IsBackground = false,
                        Name = "WDG-Monitor"
                    };
                    _monitorThread.Start();

                    if (_o.EnableHotkeys)
                    {
                        _hotkeyThread = new Thread(HotkeyLoop)
                        {
                            IsBackground = true,
                            Name = "WDG-Hotkeys"
                        };
                        _hotkeyThread.Start();
                        _hotkeyReady.Wait();
                    }

                    _attachStatusReady = true;
                    _startedSuccessfully = true;
                    _lifecycleState = LifecycleState.Running;
                }
                catch
                {
                    _shutdownRequested = true;
                    CleanupOwnedResources(killChild: !_attachedInitialProcessId.HasValue);
                    _lifecycleState = LifecycleState.Stopped;
                    _stoppedSignal.Set();
                    throw;
                }
            }
        }

        /// <summary>Blocks until successful supervision reaches complete terminal cleanup.</summary>
        /// <exception cref="InvalidOperationException"><see cref="Start"/> has not completed successfully.</exception>
        public void WaitForExit()
        {
            lock (_lifecycleLock)
            {
                if (!_startedSuccessfully)
                {
                    throw new InvalidOperationException(
                        "WatchdogRuntime must be started successfully before waiting for exit.");
                }
            }

            _stoppedSignal.Wait();
        }

        // ============================================================
        // STOP
        // ============================================================

        /// <summary>
        /// Requests terminal shutdown, stops supervision, terminates the current
        /// target through the configured graceful/forced bounds, joins owned
        /// threads, and releases IPC and the instance slot. Concurrent and repeated
        /// callers join or observe the same terminal state; stopping before start
        /// makes the instance permanently stopped.
        /// </summary>
        public void Stop()
        {
            lock (_lifecycleLock)
            {
                if (_lifecycleState == LifecycleState.Stopped ||
                    _lifecycleState == LifecycleState.Stopping)
                    return;

                _lifecycleState = LifecycleState.Stopping;
                _shutdownRequested = true;
                _attachStatusReady = false;
                try
                {
                    LogWarn("[stop] requested");
                    CleanupOwnedResources(killChild: true);
                    LogInfo("[stop] completed");
                }
                finally
                {
                    _lifecycleState = LifecycleState.Stopped;
                    _stoppedSignal.Set();
                }
            }
        }

        // ============================================================
        // RPC
        // ============================================================

        private void RegisterRpcHandlers()
        {
            var rpc = _rpc ?? throw new InvalidOperationException("RPC server is not initialized.");

            rpc.Map(WatchdogCommands.Ping, async (req, ct) => PipeOk("pong"));

            rpc.Map(WatchdogCommands.ProtocolVersion, async (req, ct) =>
                PipeOk(WatchdogBootstrap.HostProtocolVersion));

            rpc.Map(WatchdogCommands.Status, async (req, ct) =>
                PipeOk(BuildTelemetry()));

            rpc.Map(WatchdogCommands.AttachStatus, async (req, ct) =>
            {
                if (!_attachedInitialProcessId.HasValue)
                    return PipeErrorResponse(
                        "attach_not_requested",
                        "This Watchdog Host was not started in attach mode.");

                if (!_attachStatusReady)
                    return PipeErrorResponse(
                        "attach_not_ready",
                        "The Watchdog Host has not completed startup.");

                int currentPid;
                lock (_childLock)
                {
                    if (_child == null)
                        return PipeErrorResponse(
                            "attach_not_active",
                            "No target process is currently supervised.");

                    try
                    {
                        if (_child.HasExited)
                            return PipeErrorResponse(
                                "attach_not_active",
                                "The supervised target process has exited.");
                        currentPid = _child.Id;
                    }
                    catch
                    {
                        return PipeErrorResponse(
                            "attach_not_active",
                            "The supervised target process is unavailable.");
                    }
                }

                return PipeOk(WatchdogBootstrap.FormatAttachmentStatus(
                    currentPid,
                    _o.AttachToken));
            });

            rpc.Map(WatchdogCommands.Pause, async (req, ct) =>
            {
                _enabled = false;
                LogInfo("[cmd] pause");
                PublishTelemetry();
                return PipeOk("paused");
            });

            rpc.Map(WatchdogCommands.Resume, async (req, ct) =>
            {
                _enabled = true;
                LogInfo("[cmd] resume");
                PublishTelemetry();
                return PipeOk("running");
            });

            rpc.Map(WatchdogCommands.Restart, async (req, ct) =>
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

            rpc.Map(WatchdogCommands.Stop, async (req, ct) =>
            {
                LogWarn("[cmd] stop");
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        Thread.Sleep(100);
                        Stop();
                    }
                    catch { }
                });
                return PipeOk("stopped");
            });

            rpc.Map(WatchdogCommands.Update, async (req, ct) =>
            {
                return PipeErrorResponse("not_implemented", "Watchdog update orchestration is not implemented yet.");
            });

            // 🔥 Log replay buffer
            rpc.Map(WatchdogCommands.LogHistory, async (req, ct) =>
            {
                LogEntry[] history;
                lock (_bufferLock)
                    history = _logBuffer.ToArray();

                return PipeOk(history);
            });
            rpc.Map(WatchdogCommands.ExceptionNotify, async (req, ct) =>
            {
#if NET9
                var root = req.Data.GetValueOrDefault();

                string? type = root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty("type", out var t) ? t.GetString() : null;
                string? message = root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty("message", out var m) ? m.GetString() : null;
                string? source = root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty("source", out var s) ? s.GetString() : null;
#else
                string? type = req.Data?["type"]?.ToString();
                string? message = req.Data?["message"]?.ToString();
                string? source = req.Data?["source"]?.ToString();
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

            rpc.Map(WatchdogCommands.LogWrite, async (req, ct) =>
            {
#if NET9
                var root = req.Data.GetValueOrDefault();
                string? level = root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty("level", out var l) ? l.GetString() : null;
                string? message = root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty("message", out var m) ? m.GetString() : null;
                string? category = root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty("category", out var c) ? c.GetString() : null;
#else
                string? level = req.Data?["level"]?.ToString();
                string? message = req.Data?["message"]?.ToString();
                string? category = req.Data?["category"]?.ToString();
#endif
                // forwardToSinks: false — an externally-received log must not be
                // re-emitted to LogSinks (would loop back through a pipe sink).
                Log(LogSeverity.info, string.IsNullOrWhiteSpace(message) ? "[external_log]" : message!,
                    new { level, category }, forwardToSinks: false);

                return PipeOk("ok");
            });

            rpc.Map(WatchdogCommands.LogWriteBatch, async (req, ct) =>
            {
#if NET9
                if (req.Data.HasValue &&
                    req.Data.Value.TryGetProperty("entries", out var arr) &&
                    arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in arr.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Object)
                            continue;

                        string? level = item.TryGetProperty("level", out var l) ? l.GetString() : null;
                        string? message = item.TryGetProperty("message", out var m) ? m.GetString() : null;
                        string? category = item.TryGetProperty("category", out var c) ? c.GetString() : null;

                        Log(LogSeverity.info, string.IsNullOrWhiteSpace(message) ? "[external_log]" : message!,
                            new { level, category }, forwardToSinks: false);
                    }
                }
#else
                var arr = req.Data?["entries"] as JArray;
                if (arr != null)
                {
                    foreach (var item in arr)
                    {
                        if (item == null || item.Type != JTokenType.Object)
                            continue;

                        string? level = item["level"]?.ToString();
                        string? message = item["message"]?.ToString();
                        string? category = item["category"]?.ToString();

                        Log(LogSeverity.info, string.IsNullOrWhiteSpace(message) ? "[external_log]" : message!,
                            new { level, category }, forwardToSinks: false);
                    }
                }
#endif
                return PipeOk("ok");
            });
        }

        private object BuildTelemetry()
        {
            var snap = _pipeMetrics?.Snapshot();
            int? childPid = null;
            long? childUptimeMs = null;
            lock (_childLock)
            {
                childUptimeMs = _childUptime?.ElapsedMilliseconds;
                try
                {
                    if (_child != null && !_child.HasExited)
                        childPid = _child.Id;
                }
                catch
                {
                    childPid = null;
                }
            }

            var eventQueueDrops = Interlocked.Read(ref _eventQueueDrops);

            return new
            {
                state = GetState(),
                uptimeMs = _uptime.ElapsedMilliseconds,
                childUptimeMs,
                restartCount = Interlocked.Read(ref _restartCount),
                restartReason = _lastRestartReason,
                eventsDropped = eventQueueDrops,
                eventQueueDropped = eventQueueDrops,
                historyEvictions = Interlocked.Read(ref _historyEvictions),
                eventPublishFailures = Interlocked.Read(ref _eventPublishFailures),
                childPid,
                attachedInitialProcessId = _attachedInitialProcessId,
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

        private void AttachInitialProcess()
        {
            if (!_o.InitialProcessId.HasValue)
                return;

            Process? process = null;
            try
            {
                process = Process.GetProcessById(_o.InitialProcessId.Value);
                if (process.HasExited)
                    throw new InvalidOperationException("The initial process has already exited.");

                var actualPath = process.MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(actualPath) ||
                    !string.Equals(
                        Path.GetFullPath(actualPath),
                        _o.TargetPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "The initial process executable does not match TargetPath.");
                }

                // A Process obtained only by ID can otherwise reopen its native
                // handle too late, after the initial application has exited and
                // the OS object is no longer available. Materialize the handle
                // while the process is alive so ExitCode remains observable by
                // the monitor even when no launcher retains another handle.
                _ = process.Handle;

                lock (_childLock)
                {
                    _child = process;
                    _childUptime = Stopwatch.StartNew();
                    _attachedInitialProcessId = process.Id;
                    _hasSupervisedProcess = true;
                    process = null;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Unable to attach Watchdog to initial PID {_o.InitialProcessId.Value}.",
                    ex);
            }
            finally
            {
                try { process?.Dispose(); } catch { }
            }
        }

        private void CleanupOwnedResources(bool killChild)
        {
            _shutdownRequested = true;
            _attachStatusReady = false;

            try
            {
                if (_hotkeyThreadId != 0)
                {
                    Win32.PostThreadMessage(
                        _hotkeyThreadId,
                        Win32.WM_QUIT,
                        UIntPtr.Zero,
                        IntPtr.Zero);
                }
            }
            catch
            {
            }

            Process? child;
            lock (_childLock)
                child = _child;
            if (killChild && child != null)
                TryKill(child);

            JoinOwnedThread(_monitorThread);
            JoinOwnedThread(_hotkeyThread);

            lock (_childLock)
            {
                try
                {
                    _child?.Dispose();
                }
                catch
                {
                }

                _child = null;
                _childUptime = null;
            }

            try
            {
                _eventQueue.CompleteAdding();
            }
            catch
            {
            }

            JoinOwnedThread(_eventThread);

            try
            {
                _rpc?.Dispose();
            }
            catch
            {
            }
            _rpc = null;

            try
            {
                if (_ownsInstanceLock)
                {
                    _ownsInstanceLock = false;
                    _instanceLock?.Release();
                }
            }
            catch
            {
            }

            try
            {
                _instanceLock?.Dispose();
            }
            catch
            {
            }
            _instanceLock = null;
        }

        private static void JoinOwnedThread(Thread? thread)
        {
            if (thread == null || ReferenceEquals(thread, Thread.CurrentThread))
                return;

            try
            {
                thread.Join();
            }
            catch
            {
            }
        }

        private void MonitorLoop()
        {
            LogInfo("[monitor] started");

            int fastCrashCount = 0;

            while (!_shutdownRequested)
            {
                if (!_enabled)
                {
                    SleepWithShutdown(_o.MonitorPollMs);
                    continue;
                }

                if (!StartChild())
                {
                    SleepWithShutdown(_o.RestartDelayMs);
                    continue;
                }

                var start = DateTime.UtcNow;
                var nextHeartbeat = _o.HeartbeatIntervalMs > 0
                    ? DateTime.UtcNow.AddMilliseconds(_o.HeartbeatIntervalMs)
                    : DateTime.MaxValue;

                while (!_shutdownRequested)
                {
                    Process? current;
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

                try { _lastExitCode = _child?.ExitCode; }
                catch { _lastExitCode = null; }

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
                        SleepWithShutdown(10000);
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
                var added = _eventQueue.TryAdd(new QueuedEvent
                {
                    Name = name,
                    Payload = payload
                });

                if (!added)
                    OnEventDropped();
            }
            catch { }
        }

        private void OnEventDropped()
        {
            var total = Interlocked.Increment(ref _eventQueueDrops);

            // Rate-limited so a saturated queue does not spam the log: warn on the
            // first drop and then every 1000th. The warning is written directly to
            // file/sinks WITHOUT re-queuing — the event queue is full.
            if (total == 1 || total % 1000 == 0)
                WriteDropWarning(total);
        }

        private void WriteDropWarning(long total)
        {
            var msg = $"[events] dropped {total} telemetry/log events (queue full, cap={MaxQueuedEvents})";
            var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] [warn] {msg}";

            if (_o.EnableFileLogging)
                WriteLogFile(line);

            WriteDiagnosticsLog(LogSeverity.warn, msg);
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
                catch
                {
                    Interlocked.Increment(ref _eventPublishFailures);
                }
            }
        }

        private bool StartChild()
        {
            lock (_childLock)
            {
                if (_child != null && !_child.HasExited)
                    return true;

                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = _o.TargetPath,
                        Arguments = _o.TargetArguments,
                        WorkingDirectory = _o.WorkingDirectory,
                        UseShellExecute = false,
                        CreateNoWindow = false
                    };

                    psi.Environment[WatchdogBootstrap.UnderWatchdogEnvironmentVariable] = "1";

                    var child = Process.Start(psi);
                    if (child == null)
                        throw new InvalidOperationException(
                            "The target process did not start.");

                    _child = child;
                    _childUptime = Stopwatch.StartNew();
                    if (_hasSupervisedProcess)
                        Interlocked.Increment(ref _restartCount);
                    else
                        _hasSupervisedProcess = true;

                    LogInfo("[child_start]", new { pid = child.Id });
                    return true;
                }
                catch (Exception ex)
                {
                    try { _child?.Dispose(); } catch { }
                    _child = null;
                    _childUptime = null;
                    LogError("[child_start_failed]", new
                    {
                        target = _o.TargetPath,
                        error = ex.Message
                    });
                    return false;
                }
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
                    Interlocked.Read(ref _restartCount),
                    line => LogInfo(line));
            }
            catch { }
            finally
            {
                // Reset in finally so a throwing bundler can't leave the flag set
                // and contaminate the reason of the next finalize.
                _crashNotificationReceived = false;
            }
        }

        private string? SafeStatusText()
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

                    using var taskkill = Process.Start(new ProcessStartInfo
                    {
                        FileName = SystemTaskkillPath,
                        Arguments = "/PID " +
                            p.Id.ToString(CultureInfo.InvariantCulture) +
                            " /T /F",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    taskkill?.WaitForExit(_o.ForceKillTimeoutMs);
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
            public string level { get; set; } = "";
            public string msg { get; set; } = "";
            public object? meta { get; set; }
            public string line { get; set; } = "";
        }

        private void LogInfo(string msg, object? meta = null) => Log(LogSeverity.info, msg, meta);
        private void LogWarn(string msg, object? meta = null) => Log(LogSeverity.warn, msg, meta);
        private void LogError(string msg, object? meta = null) => Log(LogSeverity.error, msg, meta);

        private void Log(LogSeverity sev, string msg, object? meta, bool forwardToSinks = true)
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
                {
                    _logBuffer.Dequeue();
                    Interlocked.Increment(ref _historyEvictions);
                }
            }

            // live event
            QueueEvent("log", entry);

            // Externally-received logs (log_write / log_write_batch) are NOT
            // re-forwarded to LogSinks: a WatchdogPipeLogSink would push them
            // back onto the control pipe and loop.
            if (forwardToSinks)
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
            if (sinks.Length == 0)
                return;

            var level = ToLogLevel(sev);
            var entry = new NekoLib.Core.Logging.LogEntry(
                DateTime.UtcNow,
                level,
                msg,
                category: "Watchdog");

            for (int i = 0; i < sinks.Length; i++)
            {
                var sink = sinks[i];
                if (sink == null)
                    continue;

                // Guard against an infinite log loop: a WatchdogPipeLogSink forwards
                // to the watchdog's own control pipe, which maps log_write(_batch)
                // back into Log(...). A runtime must never re-forward its own logs
                // to such a sink (defense-in-depth alongside the forwardToSinks flag).
                if (sink is WatchdogPipeLogSink)
                    continue;

                try { sink.Write(entry); }
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
            var telemetry = _o.Telemetry;
            if (telemetry == null)
                return;

            try
            {
                var data = new Dictionary<string, object>
                {
                    { "payload", payload }
                };

                var operation = telemetry.StartOperation(
                    "Watchdog",
                    name,
                    dimensions: data);
                operation.Complete(TelemetryOutcome.Succeeded);
            }
            catch { }
        }

        private void WriteLogFile(string line)
        {
            var path = _o.LogPath;
            if (string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                lock (_logLock)
                    WatchdogLogFile.Append(path!, line, _o.MaxLogBytes);
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

                var processes = Process.GetProcessesByName(exeName);
                try
                {
                    foreach (var process in processes)
                    {
                        try
                        {
                            if (process.MainWindowHandle == IntPtr.Zero)
                                continue;

                            Win32.ShowWindow(process.MainWindowHandle, Win32.SW_RESTORE);
                            Win32.SetForegroundWindow(process.MainWindowHandle);
                            return;
                        }
                        catch
                        {
                        }
                    }
                }
                finally
                {
                    foreach (var process in processes)
                    {
                        try { process.Dispose(); } catch { }
                    }
                }
            }
            catch { }
        }

        // ============================================================
        // HOTKEYS
        // ============================================================

        private void HotkeyLoop()
        {
            try
            {
                _hotkeyThreadId = Win32.GetCurrentThreadId();
                _hotkeyHwnd = Win32.CreateMessageOnlyWindow();
                _hotkeyReady.Set();

                RegisterHotkey(1, WatchdogHotkeys.PauseKey, "Ctrl+Alt+P");
                RegisterHotkey(2, WatchdogHotkeys.ResumeKey, "Ctrl+Alt+R");
                RegisterHotkey(3, WatchdogHotkeys.StopKey, "Ctrl+Alt+Q");

                while (!_shutdownRequested)
                {
                    if (!Win32.GetMessage(out var msg, IntPtr.Zero, 0, 0))
                        break;

                    if (msg.message == Win32.WM_HOTKEY)
                    {
                        int id = (int)msg.wParam;

                        if (id == 1)
                        {
                            _enabled = false;
                            LogInfo("[hk] pause");
                            PublishTelemetry();
                        }
                        else if (id == 2)
                        {
                            _enabled = true;
                            LogInfo("[hk] resume");
                            PublishTelemetry();
                        }
                        else if (id == 3)
                        {
                            LogWarn("[hk] stop");
                            ThreadPool.QueueUserWorkItem(_ => Stop());
                            break;
                        }
                    }

                    Win32.TranslateMessage(ref msg);
                    Win32.DispatchMessage(ref msg);
                }
            }
            catch (Exception ex)
            {
                LogWarn("[hotkeys] listener_failed", new { error = ex.Message });
            }
            finally
            {
                _hotkeyReady.Set();
                try { Win32.UnregisterHotKey(_hotkeyHwnd, 1); } catch { }
                try { Win32.UnregisterHotKey(_hotkeyHwnd, 2); } catch { }
                try { Win32.UnregisterHotKey(_hotkeyHwnd, 3); } catch { }
                try
                {
                    if (_hotkeyHwnd != IntPtr.Zero)
                        Win32.DestroyWindow(_hotkeyHwnd);
                }
                catch { }
                _hotkeyHwnd = IntPtr.Zero;
                _hotkeyThreadId = 0;
            }
        }

        private void RegisterHotkey(int id, uint key, string displayName)
        {
            var modifiers = WatchdogHotkeys.ModControl | WatchdogHotkeys.ModAlt;
            if (Win32.RegisterHotKey(_hotkeyHwnd, id, modifiers, key))
                return;

            LogWarn(
                "[hotkeys] registration_failed",
                new
                {
                    hotkey = displayName,
                    win32Error = Marshal.GetLastWin32Error()
                });
        }

        // ============================================================
        // DISPOSE
        // ============================================================

        /// <summary>Performs the same terminal, idempotent cleanup as <see cref="Stop"/>.</summary>
        public void Dispose()
        {
            Stop();
        }

        // ============================================================
        // WIN32
        // ============================================================

        internal static class Win32
        {
            public const int WM_HOTKEY = 0x0312;
            public const int WM_QUIT = 0x0012;
            public const int SW_RESTORE = 9;

            private const int HWND_MESSAGE = -3;

            [DllImport("kernel32.dll")]
            public static extern uint GetCurrentThreadId();

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

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

            [DllImport("user32.dll")]
            public static extern bool DestroyWindow(IntPtr hWnd);

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
