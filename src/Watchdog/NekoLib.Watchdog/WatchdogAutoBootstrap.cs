using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using NekoLib.Pipes;

namespace NekoLib.Watchdog
{
    /// <summary>
    /// Optional control for package-generated automatic desktop startup. The
    /// package prepares this facade before application entry code and activates
    /// it at the first supported native-loop callback. No Main call is required.
    /// </summary>
    public static class WatchdogAutoBootstrap
    {
        private static readonly object Gate = new object();
        private static WatchdogAutoBootstrapOptions? _options;
        private static bool _active;
        private static bool _disabled;
        private static bool _activating;
        private static bool _detached;

        /// <summary>Receives fail-soft activation/cleanup diagnostics on the calling thread. Subscriber failures are isolated.</summary>
        public static event Action<string>? Warning;
        /// <summary>Gets whether this facade has confirmed supervision of the current application PID.</summary>
        public static bool IsActive { get { lock (Gate) return _active; } }

        /// <summary>
        /// Changes a private configuration copy before activation. Retaining the
        /// callback's options does not allow later mutation of captured policy.
        /// Configuration is also allowed after Disable, before a later Enable.
        /// Target identity and process-local sinks are not configurable here.
        /// </summary>
        /// <exception cref="InvalidOperationException">Bootstrap was not prepared, is activating or active.</exception>
        public static void Configure(Action<WatchdogAutoBootstrapOptions> configure)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            lock (Gate)
            {
                if (_options == null || _active || _activating)
                    throw new InvalidOperationException("Configure automatic Watchdog policy after preparation and before activation.");
                var copy = WatchdogSupervisorLaunch.Copy(_options);
                configure(copy);
                if (_active || _activating)
                    throw new InvalidOperationException("Bootstrap state changed during configuration.");
                _options = WatchdogSupervisorLaunch.Copy(copy);
            }
        }

        /// <summary>
        /// Prevents automatic activation, or detaches active supervision while
        /// leaving the application running. May be called from optional startup
        /// code; it does not invoke the target-terminating Stop command.
        /// </summary>
        /// <exception cref="InvalidOperationException">Bootstrap is currently activating or the supervisor rejects detachment.</exception>
        /// <exception cref="TimeoutException">Detachment is not confirmed within the configured budget.</exception>
        public static void Disable()
        {
            lock (Gate)
            {
                if (_activating) throw new InvalidOperationException("Cannot disable during automatic activation.");
                _disabled = true;
                if (_detached) return;
                if (_active || WatchdogBootstrap.IsRunningUnderWatchdog() || IsCurrentProcessSupervised())
                    Detach(_options?.HandshakeTimeoutMs ?? 5000);
                _active = false;
                _detached = true;
            }
        }

        /// <summary>Infrastructure entry used by the generated application entry point. It snapshots defaults once.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void Prepare(WatchdogAutoBootstrapOptions options)
        {
            lock (Gate)
            {
                if (_options != null) throw new InvalidOperationException("Automatic bootstrap is already prepared.");
                _options = WatchdogSupervisorLaunch.Copy(options);
            }
        }

        /// <summary>
        /// Activates prepared supervision. Generated UI callbacks call this
        /// automatically; failures emit Warning and leave the application running.
        /// Repeated calls and calls after Disable do not start another supervisor.
        /// </summary>
        public static void Activate()
            => ActivateCore(explicitEnable: false);

        /// <summary>
        /// Explicitly enables prepared automatic supervision, including after
        /// Disable. Starts a fresh supervisor session using the current copied
        /// configuration. Returns false and emits Warning if activation fails.
        /// </summary>
        public static bool Enable()
            => ActivateCore(explicitEnable: true);

        private static bool ActivateCore(bool explicitEnable)
        {
            string? warning = null;
            lock (Gate)
            {
                if (_active) return true;
                if ((_disabled && !explicitEnable) || _activating) return false;
                _disabled = false;
                _activating = true;
                try
                {
                    if (_options == null) throw new InvalidOperationException("Automatic bootstrap was not prepared. Enable the package build integration.");
                    StartSupervisor(_options, ignoreInheritedSupervisor: _detached);
                    _active = true;
                    _detached = false;
                }
                catch (Exception ex) { _disabled = true; warning = "Automatic Watchdog activation failed; the application is not confirmed supervised. " + ex.Message; }
                finally { _activating = false; }
            }
            if (warning != null) Warn(warning);
            return IsActive;
        }

        /// <summary>
        /// Confirms that supervision is disabled, then terminates the current
        /// application with Environment.Exit. Callable from any thread. Complete
        /// application-owned cleanup before calling; this is process exit, not a
        /// framework window-close notification. If detachment fails, the method
        /// throws and does not exit into an unconfirmed restart race.
        /// </summary>
        public static void Shutdown(int exitCode = 0)
        {
            Disable();
            Environment.Exit(exitCode);
        }

        /// <summary>Infrastructure normal-return cleanup. Fatal/abrupt exits must not call this method.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void CompleteApplication()
        {
            // A controller-only invocation never started supervision and must
            // not detach a different live application using the same executable.
            lock (Gate)
                if (!_active && !WatchdogBootstrap.IsRunningUnderWatchdog())
                {
                    _disabled = true;
                    _detached = true;
                    return;
                }
            try { Disable(); }
            catch (SupervisorTerminationException) { /* Let an accepted supervisor-owned stop/restart finish. */ }
            catch (Exception ex) { Warn("Normal-return Watchdog detachment failed. " + ex.Message); }
        }

        /// <summary>
        /// Handles the reserved headless supervisor invocation before application
        /// code or UI initialization. Returns false for ordinary application args.
        /// A recognized invalid invocation returns true with exit code 1.
        /// </summary>
        public static bool TryRunSupervisor(string[] arguments, out int exitCode)
        {
            if (arguments == null) throw new ArgumentNullException(nameof(arguments));
            exitCode = 0;
            if (arguments.Length == 0 || arguments[0] != WatchdogSupervisorLaunch.Argument) return false;
            try
            {
                if (arguments.Length != 2) throw new ArgumentException("Supervisor mode requires exactly one generated configuration file.");
                var launch = WatchdogSupervisorLaunch.Read(arguments[1]);
                using (var current = Process.GetCurrentProcess())
                    if (launch.RuntimeOptions.InitialProcessId == current.Id)
                        throw new InvalidOperationException("Supervisor and supervised application must be separate processes.");
                using (var runtime = new WatchdogRuntime(launch.RuntimeOptions))
                {
                    runtime.Start();
                    runtime.WaitForExit();
                }
            }
            catch (Exception ex) { exitCode = 1; Warn("Headless Watchdog supervisor failed. " + ex.Message); }
            return true;
        }

        private static void StartSupervisor(WatchdogAutoBootstrapOptions options, bool ignoreInheritedSupervisor)
        {
            var elapsed = Stopwatch.StartNew();
            using (var current = Process.GetCurrentProcess())
            {
                var target = current.MainModule?.FileName ?? throw new InvalidOperationException("Cannot resolve the application executable.");
                if (string.Equals(Path.GetFileNameWithoutExtension(target), "dotnet", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Launch the application apphost .exe directly; dotnet application.dll is not supported by automatic bootstrap.");
                var pipe = WatchdogController.ResolvePipeNameForTarget(target);
                if (!ignoreInheritedSupervisor && WatchdogBootstrap.IsRunningUnderWatchdog())
                {
                    WaitForCurrentAttachment(pipe, current.Id, options.HandshakeTimeoutMs);
                    return;
                }
                if (WatchdogBootstrap.TryGetAttachedProcessId(pipe, Math.Min(500, options.HandshakeTimeoutMs), out var existingPid))
                {
                    WatchdogBootstrap.ValidateAttachedProcessId(existingPid, current.Id);
                    return;
                }
                var runtime = WatchdogSupervisorLaunch.Deserialize<WatchdogOptions>(WatchdogSupervisorLaunch.Serialize(options.RuntimeOptions));
                runtime.TargetPath = target;
                runtime.TargetArguments = WatchdogBootstrap.BuildCommandLine(GetApplicationArguments());
                runtime.InitialProcessId = current.Id;
                runtime.AttachToken = Guid.NewGuid().ToString("N");
                runtime.WorkingDirectory = string.IsNullOrWhiteSpace(runtime.WorkingDirectory) ? Environment.CurrentDirectory : runtime.WorkingDirectory;
                if (runtime.EnableFileLogging && string.IsNullOrWhiteSpace(runtime.LogPath))
                    runtime.LogPath = Path.Combine(Path.GetDirectoryName(WatchdogSupervisorLaunch.ConfigurationDirectory)!, pipe, "watchdog.log");
                // Validate/capture before launching a process; caller options remain untouched.
                _ = WatchdogRuntimeOptions.Capture(runtime);
                var host = options.HostingMode == WatchdogHostingMode.SameExecutable ? target : Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, WatchdogBootstrap.HostDirectoryName, WatchdogBootstrap.HostExecutableName);
                if (!File.Exists(host)) throw new FileNotFoundException("The selected supervisor executable is missing. Sidecar mode requires a direct same-version Host reference.", host);
                var config = new WatchdogSupervisorLaunch { RuntimeOptions = runtime }.Write();
                Process? process = null;
                try
                {
                    process = Process.Start(new ProcessStartInfo
                    {
                        FileName = host,
                        Arguments = WatchdogBootstrap.BuildCommandLine(new[] { WatchdogSupervisorLaunch.Argument, config }),
                        WorkingDirectory = runtime.WorkingDirectory,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    var remaining = options.HandshakeTimeoutMs - (int)elapsed.ElapsedMilliseconds;
                    if (process == null || remaining < 1 || !WatchdogBootstrap.WaitForAttachment(pipe, current.Id, runtime.AttachToken, remaining))
                        throw new TimeoutException("Supervisor did not confirm the current application PID/token within the startup budget.");
                }
                catch
                {
                    try { if (process != null && !process.HasExited) process.Kill(); } catch { }
                    throw;
                }
                finally { process?.Dispose(); WatchdogSupervisorLaunch.TryDelete(config); }
            }
        }

        private static string[] GetApplicationArguments()
        {
            var args = Environment.GetCommandLineArgs();
            var result = new string[Math.Max(0, args.Length - 1)];
            if (result.Length != 0) Array.Copy(args, 1, result, 0, result.Length);
            return result;
        }

        private static bool IsCurrentProcessSupervised()
        {
            using var current = Process.GetCurrentProcess();
            var target = current.MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(target)) throw new InvalidOperationException("Cannot resolve the current executable for supervision control.");
            var pipe = WatchdogController.ResolvePipeNameForTarget(target);
            return WatchdogBootstrap.TryGetAttachedProcessId(pipe, Math.Min(100, _options?.HandshakeTimeoutMs ?? 5000), out var pid) && pid == current.Id;
        }

        private static void Detach(int timeoutMs)
        {
            var elapsed = Stopwatch.StartNew();
            using var current = Process.GetCurrentProcess();
            var pipe = WatchdogController.ResolvePipeNameForTarget(current.MainModule!.FileName);
            if (!_active && WatchdogBootstrap.IsRunningUnderWatchdog())
                WaitForCurrentAttachment(pipe, current.Id, timeoutMs);
            var requestBudget = Math.Max(1, timeoutMs - (int)elapsed.ElapsedMilliseconds);
            var client = new PipeClient(new PipeClientOptions
            {
                PipeName = pipe,
                ConnectTimeout = TimeSpan.FromMilliseconds(requestBudget),
                RequestTimeout = TimeSpan.FromMilliseconds(requestBudget)
            });
            using (var cancellation = new CancellationTokenSource(requestBudget))
            {
                var response = client.SendAsync(WatchdogCommands.Detach, new { pid = current.Id }, cancellation.Token).GetAwaiter().GetResult();
                if (!response.Ok)
                {
                    if (response.Error?.Code == "termination_in_progress") throw new SupervisorTerminationException();
                    throw new InvalidOperationException("Supervisor rejected nonterminating detach: " + response.Error?.Code);
                }
            }
            while (elapsed.ElapsedMilliseconds < timeoutMs)
            {
                try
                {
                    var remaining = Math.Max(1, timeoutMs - (int)elapsed.ElapsedMilliseconds);
                    if (!WatchdogBootstrap.TryGetAttachedProcessId(pipe, Math.Min(100, remaining), out _)) return;
                }
                catch (InvalidOperationException) { /* Endpoint may still be draining during terminal cleanup. */ }
                Thread.Sleep(10);
            }
            throw new TimeoutException("Supervisor detachment was not confirmed within the configured budget.");
        }

        private static void WaitForCurrentAttachment(string pipe, int currentPid, int timeoutMs)
        {
            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < timeoutMs)
            {
                try
                {
                    var remaining = Math.Max(1, timeoutMs - (int)watch.ElapsedMilliseconds);
                    if (WatchdogBootstrap.TryGetAttachedProcessId(pipe, Math.Min(100, remaining), out var pid) && pid == currentPid) return;
                }
                catch (InvalidOperationException) { /* A just-launched replacement can precede its parent's PID publication. */ }
                Thread.Sleep(10);
            }
            throw new TimeoutException("The existing supervisor did not confirm the current replacement PID within the startup budget.");
        }

        internal static void Warn(string message)
        {
            try { Trace.TraceWarning(message); } catch { }
            try { Console.Error.WriteLine("[NekoLib.Watchdog] " + message); } catch { }
            try
            {
                var path = Path.Combine(Path.GetDirectoryName(WatchdogSupervisorLaunch.ConfigurationDirectory)!, "auto-bootstrap.log");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var bounded = message.Length > 8192 ? message.Substring(0, 8192) : message;
                WatchdogLogFile.Append(path, DateTimeOffset.UtcNow.ToString("O") + " WARNING " + bounded, 256 * 1024);
            }
            catch { }
            var handlers = Warning;
            if (handlers == null) return;
            foreach (Action<string> handler in handlers.GetInvocationList())
                try { handler(message); } catch { }
        }

        private sealed class SupervisorTerminationException : InvalidOperationException
        {
            public SupervisorTerminationException() : base("The supervisor is already stopping or restarting this application.") { }
        }
    }
}
