#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace NekoLib.Pipes.RuntimeTests.LongRunningRecovery
{
    /// <summary>
    /// One child process the controller started and therefore owns.
    /// <para/>
    /// Identity is the PID, the process name and the start time together. A PID
    /// alone is not an identity: it is reused, and a scenario that forced "the
    /// process with this id" after a reuse would kill a stranger. E3-ORCH learnt
    /// this the same way and the rule is worth repeating in every scenario that
    /// owns processes.
    /// </summary>
    internal sealed class ChildProcess : IDisposable
    {
        private readonly Process _process;
        private readonly StringBuilder _stdout = new StringBuilder();
        private readonly StringBuilder _stderr = new StringBuilder();

        private ChildProcess(Process process, string role, string resultPath)
        {
            _process = process;
            Role = role;
            ResultPath = resultPath;
            StartedUtc = DateTime.UtcNow;

            Id = process.Id;
            ProcessName = process.ProcessName;
        }

        public string Role { get; }
        public int Id { get; }
        public string ProcessName { get; }
        public DateTime StartedUtc { get; }
        public string ResultPath { get; }

        public bool HasExited
        {
            get { try { return _process.HasExited; } catch (InvalidOperationException) { return true; } }
        }

        public int? ExitCode
        {
            get
            {
                try { return _process.HasExited ? _process.ExitCode : (int?)null; }
                catch (InvalidOperationException) { return null; }
            }
        }

        public string Stdout { get { lock (_stdout) return _stdout.ToString(); } }
        public string Stderr { get { lock (_stderr) return _stderr.ToString(); } }

        /// <summary>Starts this executable again in a child role.</summary>
        public static ChildProcess Start(string role, string arguments, string resultPath)
        {
            string executable = CurrentExecutable();

            ProcessStartInfo info = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            Process process = new Process { StartInfo = info };
            process.Start();

            // Touching Handle before the process exits is what keeps ExitCode
            // readable afterwards. Without it every child reads back as null and
            // a passing run reports its workers as failed - the exact defect the
            // orchestrator hit and recorded.
            IntPtr ignored = process.Handle;
            GC.KeepAlive(ignored);

            ChildProcess child = new ChildProcess(process, role, resultPath);

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) lock (child._stdout) child._stdout.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) lock (child._stderr) child._stderr.AppendLine(e.Data);
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return child;
        }

        private static string CurrentExecutable()
        {
#if NET9_0_OR_GREATER
            string? path = Environment.ProcessPath;
            if (path != null && path.Length > 0 &&
                !path.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
#endif
            return Process.GetCurrentProcess().MainModule!.FileName!;
        }

        public bool WaitForExit(TimeSpan timeout)
        {
            try { return _process.WaitForExit((int)timeout.TotalMilliseconds); }
            catch (InvalidOperationException) { return true; }
        }

        /// <summary>
        /// Ends this child, and only this child.
        /// <para/>
        /// The identity is re-verified before anything is forced, because a PID
        /// can be reused between the decision and the act.
        /// </summary>
        public bool Kill(out string diagnostic)
        {
            diagnostic = string.Empty;

            try
            {
                if (_process.HasExited)
                {
                    diagnostic = "already exited";
                    return true;
                }

                if (!string.Equals(_process.ProcessName, ProcessName, StringComparison.Ordinal))
                {
                    diagnostic = "pid " + Id + " is no longer '" + ProcessName + "'; refusing to force it";
                    return false;
                }

                _process.Kill();
                _process.WaitForExit(10000);
                diagnostic = "forced";
                return true;
            }
            catch (InvalidOperationException)
            {
                diagnostic = "already exited";
                return true;
            }
            catch (Exception ex)
            {
                diagnostic = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        /// <summary>The child's own result document, if it wrote one.</summary>
        public Dictionary<string, object?>? ReadResult()
        {
            try
            {
                if (ResultPath.Length == 0 || !File.Exists(ResultPath)) return null;

                return NekoLib.RuntimeTests.Harness.JsonParser.AsObject(
                    NekoLib.RuntimeTests.Harness.JsonParser.Parse(File.ReadAllText(ResultPath)),
                    "a child result");
            }
            catch (Exception) { return null; }
        }

        public void Dispose()
        {
            try { _process.Dispose(); } catch (Exception) { }
        }
    }

    /// <summary>
    /// A raw pipe peer: a client that speaks the wire directly instead of
    /// through <see cref="PipeClient"/>.
    /// <para/>
    /// This is the only way to produce a malformed or truncated frame, which the
    /// suite requires. Doing it through the client API is impossible by
    /// construction, and adding a "send me something broken" switch to the
    /// module would be the privileged control plane the suite forbids.
    /// </summary>
    internal static class RawPeer
    {
        /// <summary>
        /// Connects, writes a length prefix promising more bytes than it sends,
        /// then closes. The server must survive a peer that lies about a frame.
        /// </summary>
        public static string TruncatedFrame(string pipeName, int promisedBytes, int actualBytes)
        {
            using (System.IO.Pipes.NamedPipeClientStream stream =
                   new System.IO.Pipes.NamedPipeClientStream(
                       ".", pipeName, System.IO.Pipes.PipeDirection.InOut))
            {
                stream.Connect(5000);

                byte[] prefix = BitConverter.GetBytes(promisedBytes);
                stream.Write(prefix, 0, prefix.Length);

                byte[] body = new byte[actualBytes];
                for (int i = 0; i < body.Length; i++) body[i] = (byte)'x';
                stream.Write(body, 0, body.Length);
                stream.Flush();

                return "promised " + promisedBytes.ToString(CultureInfo.InvariantCulture) +
                       " bytes, sent " + actualBytes.ToString(CultureInfo.InvariantCulture) + ", then closed";
            }
        }

        /// <summary>Connects and closes immediately, without writing anything at all.</summary>
        public static string SilentClose(string pipeName)
        {
            using (System.IO.Pipes.NamedPipeClientStream stream =
                   new System.IO.Pipes.NamedPipeClientStream(
                       ".", pipeName, System.IO.Pipes.PipeDirection.InOut))
            {
                stream.Connect(5000);
                return "connected and closed without writing a byte";
            }
        }
    }
}
