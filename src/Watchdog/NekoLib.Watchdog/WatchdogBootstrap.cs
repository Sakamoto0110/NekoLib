using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using NekoLib.Pipes;

#if NET9
using System.Text.Json;
#else
using Newtonsoft.Json.Linq;
#endif

namespace NekoLib.Watchdog
{
    /// <summary>
    /// Application-side bootstrap that starts the deployed Watchdog Host and
    /// hands the already-running application process to it.
    /// </summary>
    public static class WatchdogBootstrap
    {
        private static readonly object BootstrapLock = new object();

        /// <summary>Environment flag set on supervised replacements to prevent recursive Host bootstrap.</summary>
        public const string UnderWatchdogEnvironmentVariable = "NEKO_UNDER_WATCHDOG";
        /// <summary>Deployment subdirectory expected below the application base directory.</summary>
        public const string HostDirectoryName = "NekoLib.Watchdog.Host";
        /// <summary>Watchdog Host executable expected inside <see cref="HostDirectoryName"/>.</summary>
        public const string HostExecutableName = "NekoLib.Watchdog.Host.exe";

        internal const string HostProtocolVersion = "1";

        private const int DefaultHandshakeTimeoutMs = 5000;

        /// <summary>
        /// Starts supervision using the current process command-line arguments.
        /// Call this near the beginning of <c>Main</c>.
        /// </summary>
        /// <exception cref="FileNotFoundException">The deployed Host executable cannot be found.</exception>
        /// <exception cref="TimeoutException">The Host does not confirm the expected attachment within the default budget.</exception>
        /// <exception cref="InvalidOperationException">Process identity cannot be resolved or a running Host reports a different target PID.</exception>
        public static void EnsureStarted()
        {
            var commandLine = Environment.GetCommandLineArgs();
            var arguments = new string[Math.Max(0, commandLine.Length - 1)];
            if (arguments.Length > 0)
                Array.Copy(commandLine, 1, arguments, 0, arguments.Length);

            EnsureStarted(arguments, DefaultHandshakeTimeoutMs);
        }

        /// <summary>
        /// Starts supervision while preserving the supplied original application
        /// arguments for every later restart.
        /// </summary>
        /// <param name="arguments">Original application arguments, excluding the executable path.</param>
        /// <exception cref="ArgumentNullException"><paramref name="arguments"/> is <c>null</c>.</exception>
        public static void EnsureStarted(string[] arguments)
            => EnsureStarted(arguments, DefaultHandshakeTimeoutMs);

        /// <summary>
        /// Starts supervision and waits at most <paramref name="handshakeTimeoutMs"/>
        /// for the Host to confirm the expected PID and one-time attach token.
        /// </summary>
        /// <param name="arguments">Original application arguments, excluding the executable path.</param>
        /// <param name="handshakeTimeoutMs">Positive total budget for duplicate detection, Host startup, and attach confirmation.</param>
        /// <exception cref="ArgumentNullException"><paramref name="arguments"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="handshakeTimeoutMs"/> is less than 1.</exception>
        /// <exception cref="FileNotFoundException">The deployed Host executable cannot be found.</exception>
        /// <exception cref="TimeoutException">The Host does not confirm the expected attachment within the budget.</exception>
        /// <exception cref="InvalidOperationException">Process identity cannot be resolved, a running Host reports a different target PID, the Host uses an incompatible protocol version, or a launched Host exited before confirming the attachment.</exception>
        public static void EnsureStarted(string[] arguments, int handshakeTimeoutMs)
        {
            if (IsRunningUnderWatchdog())
                return;

            if (arguments == null)
                throw new ArgumentNullException(nameof(arguments));
            if (handshakeTimeoutMs < 1)
                throw new ArgumentOutOfRangeException(nameof(handshakeTimeoutMs));

            var callElapsed = Stopwatch.StartNew();
            lock (BootstrapLock)
            {
                if (IsRunningUnderWatchdog())
                    return;

                var remainingMs = RemainingBudgetMs(
                    callElapsed,
                    handshakeTimeoutMs);
                if (remainingMs < 1)
                    throw CreateHandshakeTimeout();

                EnsureStartedCore(arguments, remainingMs);
            }
        }

        private static void EnsureStartedCore(
            string[] arguments,
            int handshakeTimeoutMs)
        {
            var handshakeElapsed = Stopwatch.StartNew();

            string targetPath;
            int currentPid;
            using (var current = Process.GetCurrentProcess())
            {
                targetPath = current.MainModule?.FileName
                    ?? throw new InvalidOperationException(
                        "Unable to resolve the current process executable path.");
                currentPid = current.Id;
            }

            var pipeName = WatchdogController.ResolvePipeNameForTarget(targetPath);
            var preflightBudgetMs = Math.Min(
                500,
                Math.Max(1, handshakeTimeoutMs / 4));
            preflightBudgetMs = Math.Min(
                preflightBudgetMs,
                RemainingBudgetMs(handshakeElapsed, handshakeTimeoutMs));
            if (preflightBudgetMs < 1)
                throw CreateHandshakeTimeout();

            if (TryGetAttachedProcessId(
                    pipeName,
                    preflightBudgetMs,
                    out var attachedPid))
            {
                ValidateAttachedProcessId(attachedPid, currentPid);
                return;
            }

            if (RemainingBudgetMs(handshakeElapsed, handshakeTimeoutMs) < 1)
                throw CreateHandshakeTimeout();

            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var hostPath = Path.Combine(
                baseDirectory,
                HostDirectoryName,
                HostExecutableName);
            if (!File.Exists(hostPath))
            {
                throw new FileNotFoundException(
                    "The deployed NekoLib Watchdog Host was not found.",
                    hostPath);
            }

            var workingDirectory = Environment.CurrentDirectory;
            var attachToken = Guid.NewGuid().ToString("N");
            var targetArguments = BuildCommandLine(arguments);
            var hostArguments = BuildCommandLine(new[]
            {
                "--protocol-version",
                HostProtocolVersion,
                "--target",
                targetPath,
                "--attach-pid",
                currentPid.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--attach-token",
                attachToken,
                "--args",
                targetArguments,
                "--workdir",
                workingDirectory
            });

            Process? hostProcess = null;
            try
            {
                hostProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = hostPath,
                    Arguments = hostArguments,
                    WorkingDirectory = Path.GetDirectoryName(hostPath) ?? baseDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (hostProcess == null)
                    throw new InvalidOperationException("The Watchdog Host process did not start.");

                var attachmentBudgetMs = RemainingBudgetMs(
                    handshakeElapsed,
                    handshakeTimeoutMs);
                if (attachmentBudgetMs < 1)
                    throw CreateHandshakeTimeout();

                if (WaitForAttachment(
                        pipeName,
                        currentPid,
                        attachToken,
                        attachmentBudgetMs))
                {
                    return;
                }

                bool exited;
                try { exited = hostProcess.HasExited; }
                catch { exited = true; }

                if (exited)
                {
                    throw new InvalidOperationException(
                        "The Watchdog Host exited before confirming the initial " +
                        "process attach. Inspect fatal startup evidence at '" +
                        GetHostFatalLogPath() + "'.");
                }

                throw CreateHandshakeTimeout();
            }
            catch
            {
                TerminateUnconfirmedHost(hostProcess);
                throw;
            }
            finally
            {
                hostProcess?.Dispose();
            }
        }

        internal static bool WaitForAttachment(
            string pipeName,
            int expectedPid,
            string attachToken,
            int timeoutMs)
        {
            var expected = FormatAttachmentStatus(expectedPid, attachToken);
            var elapsed = Stopwatch.StartNew();

            while (elapsed.ElapsedMilliseconds < timeoutMs)
            {
                var protocolBudget = Math.Min(
                    300,
                    RemainingBudgetMs(elapsed, timeoutMs));
                if (protocolBudget > 0 &&
                    TryConfirmProtocolVersion(pipeName, protocolBudget))
                {
                    var attachmentBudget = Math.Min(
                        300,
                        RemainingBudgetMs(elapsed, timeoutMs));
                    if (attachmentBudget < 1)
                        break;

                    if (TrySendString(
                            pipeName,
                            WatchdogCommands.AttachStatus,
                            attachmentBudget,
                            out var response,
                            out _,
                            out _) &&
                        string.Equals(response, expected, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                var remaining = timeoutMs - (int)elapsed.ElapsedMilliseconds;
                if (remaining > 0)
                    Thread.Sleep(Math.Min(50, remaining));
            }

            return false;
        }

        private static bool TryGetAttachedProcessId(
            string pipeName,
            int timeoutMs,
            out int attachedPid)
        {
            attachedPid = 0;
            var elapsed = Stopwatch.StartNew();
            var protocolBudget = Math.Min(
                300,
                RemainingBudgetMs(elapsed, timeoutMs));
            if (protocolBudget < 1 ||
                !TryConfirmProtocolVersion(pipeName, protocolBudget))
            {
                return false;
            }

            var attachmentBudget = RemainingBudgetMs(elapsed, timeoutMs);
            if (attachmentBudget < 1)
                return false;

            var succeeded = TrySendString(
                pipeName,
                WatchdogCommands.AttachStatus,
                attachmentBudget,
                out var response,
                out var hostResponded,
                out var errorCode);

            if (!hostResponded)
                return false;

            if (!succeeded)
            {
                throw new InvalidOperationException(
                    "A Watchdog Host is already running for this target, but it " +
                    "could not confirm supervision of the current process" +
                    (string.IsNullOrWhiteSpace(errorCode)
                        ? "."
                        : " (attach_status: " + errorCode + ")."));
            }

            if (!TryParseAttachmentStatus(response, out attachedPid))
            {
                throw new InvalidOperationException(
                    "A Watchdog Host is already running for this target, but it " +
                    "returned an invalid attach_status identity.");
            }

            return true;
        }

        internal static void ValidateAttachedProcessId(
            int attachedPid,
            int currentPid)
        {
            if (attachedPid == currentPid)
                return;

            throw new InvalidOperationException(
                "A Watchdog Host is already running for this target, but it " +
                "is supervising process " +
                attachedPid.ToString(
                    System.Globalization.CultureInfo.InvariantCulture) +
                " instead of the current process " +
                currentPid.ToString(
                    System.Globalization.CultureInfo.InvariantCulture) +
                ".");
        }

        private static bool TryParseAttachmentStatus(
            string? status,
            out int attachedPid)
        {
            attachedPid = 0;
            if (status == null || string.IsNullOrWhiteSpace(status))
                return false;

            var parts = status.Split(':');
            if (parts.Length != 4 ||
                !string.Equals(parts[0], "attached", StringComparison.Ordinal) ||
                !string.Equals(
                    parts[1],
                    "v" + HostProtocolVersion,
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(parts[3]))
            {
                return false;
            }

            return int.TryParse(
                       parts[2],
                       System.Globalization.NumberStyles.None,
                       System.Globalization.CultureInfo.InvariantCulture,
                       out attachedPid) &&
                   attachedPid > 0;
        }

        internal static string FormatAttachmentStatus(int pid, string attachToken)
            => "attached:v" +
               HostProtocolVersion +
               ":" +
               pid.ToString(System.Globalization.CultureInfo.InvariantCulture) +
               ":" +
               attachToken;

        internal static string GetHostFatalLogPath()
            => Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "NekoLib",
                "Watchdog",
                "watchdog-host-fatal.log");

        private static bool IsRunningUnderWatchdog()
        {
            if (Environment.GetEnvironmentVariable(
                    UnderWatchdogEnvironmentVariable) != null)
            {
                return true;
            }

            // .NET Framework reports an explicitly empty process variable as
            // null through GetEnvironmentVariable. The environment block still
            // contains its key, which is sufficient for the recursion guard.
            return Environment.GetEnvironmentVariables()
                .Contains(UnderWatchdogEnvironmentVariable);
        }

        internal static string BuildCommandLine(IEnumerable<string> arguments)
        {
            if (arguments == null)
                throw new ArgumentNullException(nameof(arguments));

            var result = new StringBuilder();
            foreach (var argument in arguments)
            {
                if (result.Length > 0)
                    result.Append(' ');
                result.Append(QuoteArgument(argument ?? string.Empty));
            }
            return result.ToString();
        }

        internal static string QuoteArgument(string argument)
        {
            if (argument == null)
                throw new ArgumentNullException(nameof(argument));

            var result = new StringBuilder(argument.Length + 2);
            result.Append('"');

            int backslashes = 0;
            for (int i = 0; i < argument.Length; i++)
            {
                var ch = argument[i];
                if (ch == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (ch == '"')
                {
                    result.Append('\\', backslashes * 2 + 1);
                    result.Append('"');
                    backslashes = 0;
                    continue;
                }

                result.Append('\\', backslashes);
                backslashes = 0;
                result.Append(ch);
            }

            result.Append('\\', backslashes * 2);
            result.Append('"');
            return result.ToString();
        }

        private static bool TrySendString(
            string pipeName,
            string command,
            int timeoutMs,
            out string? value,
            out bool hostResponded,
            out string? errorCode)
        {
            value = null;
            hostResponded = false;
            errorCode = null;
            if (timeoutMs < 1)
                return false;

            try
            {
                using (var timeoutCts = new CancellationTokenSource())
                {
                    var client = new PipeClient(new PipeClientOptions
                    {
                        PipeName = pipeName,
                        ConnectTimeout = TimeSpan.FromMilliseconds(timeoutMs),
                        RequestTimeout = TimeSpan.FromMilliseconds(timeoutMs)
                    });
                    var sendTask = client.SendAsync(
                        command,
                        cancellationToken: timeoutCts.Token);
                    if (!sendTask.Wait(timeoutMs))
                    {
                        try { timeoutCts.Cancel(); } catch { }
                        ObserveFault(sendTask);
                        return false;
                    }

                    var response = sendTask.GetAwaiter().GetResult();
                    hostResponded = true;
                    if (!response.Ok)
                    {
                        errorCode = response.Error?.Code;
                        return false;
                    }

#if NET9
                    if (!response.Data.HasValue ||
                        response.Data.Value.ValueKind != JsonValueKind.String)
                    {
                        return false;
                    }

                    value = response.Data.Value.GetString();
#else
                    var token = response.Data;
                    if (token == null || token.Type != JTokenType.String)
                        return false;

                    value = token.Value<string>();
#endif
                    return value != null;
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool TryConfirmProtocolVersion(
            string pipeName,
            int timeoutMs)
        {
            var succeeded = TrySendString(
                pipeName,
                WatchdogCommands.ProtocolVersion,
                timeoutMs,
                out var version,
                out var hostResponded,
                out var errorCode);

            if (!hostResponded)
                return false;

            if (!succeeded ||
                !string.Equals(
                    version,
                    HostProtocolVersion,
                    StringComparison.Ordinal))
            {
                throw CreateProtocolMismatch(version, errorCode);
            }

            return true;
        }

        private static InvalidOperationException CreateProtocolMismatch(
            string? version,
            string? errorCode)
        {
            var observed = !string.IsNullOrWhiteSpace(version)
                ? "version '" + version + "'"
                : !string.IsNullOrWhiteSpace(errorCode)
                    ? "protocol error '" + errorCode + "'"
                    : "an invalid response";

            return new InvalidOperationException(
                "The deployed Watchdog Host uses an incompatible protocol (" +
                observed + "). Expected version '" +
                HostProtocolVersion +
                "'. Update NekoLib.Watchdog and NekoLib.Watchdog.Host to the " +
                "same package version and rebuild the application.");
        }

        private static void ObserveFault(System.Threading.Tasks.Task task)
        {
            task.ContinueWith(
                completed =>
                {
                    var ignored = completed.Exception;
                },
                CancellationToken.None,
                System.Threading.Tasks.TaskContinuationOptions.ExecuteSynchronously,
                System.Threading.Tasks.TaskScheduler.Default);
        }

        private static int RemainingBudgetMs(Stopwatch elapsed, int totalMs)
        {
            var remaining = totalMs - elapsed.ElapsedMilliseconds;
            if (remaining <= 0)
                return 0;
            return remaining > int.MaxValue
                ? int.MaxValue
                : (int)remaining;
        }

        private static TimeoutException CreateHandshakeTimeout()
            => new TimeoutException(
                "Timed out waiting for the Watchdog Host to confirm supervision " +
                "of the current process.");

        private static void TerminateUnconfirmedHost(Process? hostProcess)
        {
            if (hostProcess == null)
                return;

            try
            {
                if (hostProcess.HasExited)
                    return;

                hostProcess.Kill();
            }
            catch
            {
                // Best effort. The caller still receives the handshake failure.
            }
        }
    }
}
