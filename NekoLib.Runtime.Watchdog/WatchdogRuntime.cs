using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace NekoLib.Runtime.Watchdog
{
    /// <summary>
    /// Describes the outcome of a watchdog-initiated kill operation.
    /// 
    /// Used only for observability and diagnostics.
    /// </summary>
    internal enum KillOutcome
    {
        None,
        GracefulSuccess,
        GracefulTimeout,
        ForceSuccess,
        ForceTimeout,
        Error
    }
    /// <summary>
    /// Describes why the child process stopped running.
    /// 
    /// This reflects the cause of termination, not the restart policy.
    /// </summary>

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
    /// <summary>
    /// Describes why the watchdog decided to start (or restart) the child process.
    /// 
    /// This does NOT describe how the child exited.
    /// 
    /// Examples:
    /// - InitialStart: first launch after watchdog start
    /// - PauseResume: restart caused by resuming from pause
    /// - NaturalExit: restart after a normal process exit
    /// - ForceKilled: restart after watchdog force-killed the process
    /// </summary>

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
    /// <summary>
    /// Runtime watchdog responsible for supervising a single target process.
    /// 
    /// Responsibilities:
    /// - Owns the lifecycle of the target process
    /// - Restarts the process on exit or crash
    /// - Supports pause / resume / stop semantics
    /// - Exposes runtime state via named-pipe commands
    /// - Emits diagnostic logs and structured status information
    /// 
    /// Design principles:
    /// - Single watchdog instance per PipeName (enforced via mutex)
    /// - Watchdog owns the child process unconditionally
    /// - Control plane (pipe) is separate from policy (options)
    /// - Observability is explicit and non-invasive
    /// 
    /// This class is intentionally synchronous, defensive, and dependency-free,
    /// suitable for kiosk, embedded, and unattended environments.
    /// </summary>

    public sealed class WatchdogRuntime : IDisposable
    {
        private readonly object _childLock = new object();
        private readonly object _logLock = new object();

        private readonly WatchdogOptions _o;

        private Process _child;
        private Thread _monitorThread;
        private Thread _pipeThread;
        private Mutex _instanceMutex;

        private readonly Stopwatch _uptime = Stopwatch.StartNew();
        private Stopwatch _childUptime;
        private long _restartCount;

        private volatile bool _enabled = true;
        private volatile bool _exiting;
        private volatile bool _shutdownRequested;

        private volatile KillOutcome _lastKillOutcome = KillOutcome.None;
        private volatile ExitReason _lastExitReason = ExitReason.None;
        private volatile RestartReason _lastRestartReason = RestartReason.None;

        public event Action<string> LogEmitted;

        public WatchdogRuntime(WatchdogOptions options)
        {
            _o = options ?? throw new ArgumentNullException(nameof(options));
            _o.Normalize();
        }


        /// <summary>
/// Main supervision loop.
/// 
/// Loop responsibilities:
/// - Waits while paused
/// - Starts the child process when enabled
/// - Monitors the child until it exits or is killed
/// - Classifies exit reasons (natural, crash, pause, stop)
/// - Applies restart policy with configurable delays
/// 
/// ExitReason describes why the child stopped.
/// RestartReason describes why the next start happens.
/// 
/// The loop runs until Stop is requested or the runtime is disposed.
/// </summary>
        public void Start()
        {
            _instanceMutex = new Mutex(
                true,
                @"Global\NekoLib.Watchdog::" + _o.PipeName,
                out bool created);

            if (!created)
                throw new InvalidOperationException("Watchdog already running.");

            // Initial start applies only once (before first child start).
            _lastRestartReason = RestartReason.InitialStart;

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
        }

        private void Enable()
        {
            // If we are resuming from pause, the next start is due to PauseResume.
            if (!_enabled)
                _lastRestartReason = RestartReason.PauseResume;

            _enabled = true;
            _shutdownRequested = false;
        }

        // ============================================================
        // Monitor loop (original semantics)
        // ============================================================

        /// <summary>
        /// Main supervision loop.
        /// 
        /// Loop responsibilities:
        /// - Waits while paused
        /// - Starts the child process when enabled
        /// - Monitors the child until it exits or is killed
        /// - Classifies exit reasons (natural, crash, pause, stop)
        /// - Applies restart policy with configurable delays
        /// 
        /// ExitReason describes why the child stopped.
        /// RestartReason describes why the next start happens.
        /// 
        /// The loop runs until Stop is requested or the runtime is disposed.
        /// </summary>

        private void MonitorLoop()
        {
            while (!_exiting)
            {
                // Hard stop requested
                if (_shutdownRequested)
                {
                    _lastExitReason = ExitReason.Stop;
                    break;
                }

                // Paused state
                if (!_enabled)
                {
                    _lastExitReason = ExitReason.Pause;
                    IdleUntilEnabledOrExit();
                    continue;
                }

                // Start / restart child (restart reason must already be set by:
                // - Start(): InitialStart
                // - Enable(): PauseResume
                // - Natural exit block below: NaturalExit / Crash
                // - TryKill(force): ForceKilled
                var child = StartChild();
                if (child == null)
                {
                    Log("[monitor] start_failed");
                    Thread.Sleep(_o.RestartDelayMs);
                    continue;
                }

                // Monitor running child
                while (!_exiting && !_shutdownRequested && !child.HasExited)
                {
                    if (!_enabled)
                    {
                        _lastExitReason = ExitReason.Pause;
                        TryKill(child);
                        break;
                    }

                    Thread.Sleep(_o.MonitorPollMs);
                }

                // Shutdown path
                if (_shutdownRequested || _exiting)
                {
                    _lastExitReason = ExitReason.Stop;
                    break;
                }

                // Child exited by itself
                if (child.HasExited)
                {
                    _childUptime?.Stop();
                    _childUptime = null;

                    // Heuristic: non-zero exit code => "Crash"
                    if (child.ExitCode != 0)
                    {
                        _lastExitReason = ExitReason.Crash;
                        _lastRestartReason = RestartReason.Crash;
                    }
                    else
                    {
                        _lastExitReason = ExitReason.NaturalExit;
                        _lastRestartReason = RestartReason.NaturalExit;
                    }

                    Log("[child_exit] code=" + child.ExitCode);
                }

                // Restart delay before next loop
                Thread.Sleep(_o.RestartDelayMs);
            }

            // Final cleanup
            TryKill(_child);
            Log("[monitor] exit");
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

                        using (var reader = new StreamReader(server))
                        using (var writer = new StreamWriter(server) { AutoFlush = true })
                        {
                            var cmd = reader.ReadLine();
                            writer.WriteLine(HandleCommand(cmd));
                        }
                    }
                }
                catch
                {
                    Thread.Sleep(100);
                }
            }
        }
        /// <summary>
        /// Handles control commands received via named pipe.
        /// 
        /// Supported commands:
        /// - start  : enable / resume supervision
        /// - pause  : pause supervision and kill the child
        /// - stop   : stop supervision and shut down watchdog
        /// - ping   : liveness probe
        /// - status : returns structured runtime state
        /// 
        /// Commands are synchronous and processed one at a time.
        /// </summary>

        private string HandleCommand(string cmd)
        {
            switch (cmd)
            {
                case "start":
                    Log("[cmd] start");
                    Enable();
                    return "running";

                case "pause":
                    Log("[cmd] pause");
                    _enabled = false;
                    _lastExitReason = ExitReason.Pause;
                    TryKill(_child);
                    return "paused";

                case "stop":
                    Log("[cmd] stop");
                    _shutdownRequested = true;
                    _exiting = true;
                    _lastExitReason = ExitReason.Stop;
                    TryKill(_child);
                    return "stopped";

                case "ping":
                    return "pong";

                case "status":
                    return GetStatus();

                default:
                    return "unknown";
            }
        }
        /// <summary>
        /// Returns the current watchdog status as a structured string.
        /// 
        /// Format:
        /// state=<running|paused|stopped>;
        /// uptimeMs=<watchdog uptime>;
        /// childUptimeMs=<current child uptime or 0>;
        /// restartCount=<number of child starts>;
        /// exitReason=<last ExitReason>;
        /// restartReason=<last RestartReason>;
        /// killOutcome=<last KillOutcome>
        /// 
        /// This method is intended for:
        /// - external monitoring
        /// - diagnostics
        /// - CLI / tooling integration
        /// </summary>
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
        // Process helpers
        // ============================================================

        private Process StartChild()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _o.TargetPath,
                    WorkingDirectory = _o.WorkingDirectory,
                    UseShellExecute = true
                };

                var p = Process.Start(psi);
                lock (_childLock)
                {
                    _child = p;
                    _childUptime = Stopwatch.StartNew();
                    _restartCount++;
                }

                Log("[child_start] pid=" + p.Id + " restartCount=" + _restartCount + " restartReason=" + _lastRestartReason);
                return p;
            }
            catch (Exception ex)
            {
                Log("[error] StartChild: " + ex.Message);
                return null;
            }
        }
        /// <summary>
        /// Attempts to terminate the child process.
        /// 
        /// Kill strategy:
        /// 1) If the process has a window, request graceful shutdown
        ///    and wait GracefulKillTimeoutMs.
        /// 2) If graceful shutdown fails, force-kill the process
        ///    and wait ForceKillTimeoutMs.
        /// 
        /// This method:
        /// - Measures elapsed kill time using Stopwatch
        /// - Records KillOutcome
        /// - Records ExitReason and RestartReason when force-kill occurs
        /// - Never throws (best-effort semantics)
        /// </summary>

        private void TryKill(Process p)
        {
            lock (_childLock)
            {
                var sw = Stopwatch.StartNew();

                try
                {
                    if (p == null || p.HasExited)
                        return;

                    // Graceful attempt
                    if (p.MainWindowHandle != IntPtr.Zero)
                    {
                        Log("[kill] graceful_request pid=" + p.Id);
                        p.CloseMainWindow();

                        if (p.WaitForExit(_o.GracefulKillTimeoutMs))
                        {
                            sw.Stop();
                            _lastKillOutcome = KillOutcome.GracefulSuccess;
                            Log($"[kill] graceful_success pid={p.Id} elapsed={sw.ElapsedMilliseconds}ms");
                            return; // DO NOT overwrite ExitReason here (Pause/Stop already set by caller)
                        }

                        Log("[kill] graceful_timeout pid=" + p.Id);
                        _lastKillOutcome = KillOutcome.GracefulTimeout;
                    }

                    // Force kill
                    Log("[kill] force pid=" + p.Id);
                    p.Kill();

                    _lastExitReason = ExitReason.ForceKill;
                    _lastRestartReason = RestartReason.ForceKilled;

                    if (p.WaitForExit(_o.ForceKillTimeoutMs))
                    {
                        sw.Stop();
                        _lastKillOutcome = KillOutcome.ForceSuccess;
                        Log($"[kill] force_success pid={p.Id} elapsed={sw.ElapsedMilliseconds}ms");
                    }
                    else
                    {
                        sw.Stop();
                        _lastKillOutcome = KillOutcome.ForceTimeout;
                        Log($"[kill] force_timeout pid={p.Id} elapsed={sw.ElapsedMilliseconds}ms");
                    }
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    _lastKillOutcome = KillOutcome.Error;

                    // If kill blew up, treat as ForceKill attempt.
                    _lastExitReason = ExitReason.ForceKill;
                    _lastRestartReason = RestartReason.ForceKilled;

                    Log($"[error] Kill: {ex.Message} elapsed={sw.ElapsedMilliseconds}ms");
                }
                finally
                {
                    _childUptime?.Stop();
                    _childUptime = null;
                    _child = null;
                }
            }
        }

        private void IdleUntilEnabledOrExit()
        {
            while (true)
            {
                if (_exiting || _shutdownRequested)
                    return;

                if (_enabled)
                    return;

                Thread.Sleep(_o.MonitorPollMs);
            }
        }

        // ============================================================
        // Logging (with rotation)
        // ============================================================
        /// <summary>
        /// Emits a watchdog log entry.
        /// 
        /// Behavior:
        /// - Always fires LogEmitted event (best-effort)
        /// - Writes to file only if EnableFileLogging is true
        /// - Applies size-based log rotation
        /// - Never throws
        /// 
        /// Logging is intentionally synchronous and minimal.
        /// </summary>
        private void Log(string msg)
        {
            var line =
                "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " +
                msg;

            try { LogEmitted?.Invoke(line); } catch { }

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

        /// <summary>
        /// Stops the watchdog and releases all resources.
        /// 
        /// This method:
        /// - Signals monitor and pipe threads to exit
        /// - Waits briefly for thread termination
        /// - Releases the instance mutex
        /// 
        /// Safe to call multiple times.
        /// </summary>
        public void Dispose()
        {
            _shutdownRequested = true;
            _exiting = true;

            try { _monitorThread?.Join(1500); } catch { }
            try { _pipeThread?.Join(1500); } catch { }

            try
            {
                _instanceMutex?.ReleaseMutex();
                _instanceMutex?.Dispose();
            }
            catch { }
        }
    }
}
