using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using NekoLib.Pipes;
using NekoLib.Watchdog;

namespace NekoLib.Watchdog.RuntimeTests.Supervisor481
{
    /// <summary>
    /// Manual runtime test for the Watchdog module — a control dashboard that
    /// hosts a <see cref="WatchdogRuntime"/> in-process, supervising a target
    /// executable (notepad by default), and drives it over the real named-pipe
    /// RPC + event channels exactly as a separate controller process would.
    ///
    /// <para>
    /// What it exercises end-to-end:
    /// <list type="bullet">
    ///   <item>process supervision (start / monitor / auto-restart on exit)</item>
    ///   <item>RPC commands: ping, status, pause, resume, restart</item>
    ///   <item>live log streaming via <see cref="PipeEventClient"/> ("log" events)</item>
    ///   <item>telemetry events ("telemetry") surfaced into the state bar</item>
    /// </list>
    /// Close the supervised child (e.g. notepad) and watch the watchdog respawn
    /// it; hit Pause to stop respawning, Resume to continue. The runtime also
    /// registers global Ctrl+Alt+P / R / Q hotkeys while running.
    /// </para>
    /// </summary>
    public partial class MainForm : Form
    {
        private WatchdogRuntime _runtime;
        private PipeEventClient _events;
        private string _pipeName;
        private bool _running;

        public MainForm()
        {
            InitializeComponent();

            // Default target: notepad — stays open, has a main window (so the
            // graceful CloseMainWindow path is exercised), and is harmless to kill.
            txtTarget.Text = Path.Combine(Environment.SystemDirectory, "notepad.exe");

            FormClosing += MainForm_FormClosing;
        }

        // =====================================================================
        // Lifecycle
        // =====================================================================

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (_running) return;

            var target = txtTarget.Text != null ? txtTarget.Text.Trim() : "";
            if (string.IsNullOrWhiteSpace(target) || !File.Exists(target))
            {
                AppendError("Target executable not found: " + target);
                return;
            }

            try
            {
                var options = new WatchdogOptions
                {
                    TargetPath = target,
                    EnableFileLogging = false   // keep the demo to in-memory + pipe only
                };

                // WatchdogRuntime's ctor calls Normalize(), which fills PipeName
                // with the SHA1-derived identity we need for the pipe clients.
                _runtime = new WatchdogRuntime(options);
                _pipeName = options.PipeName;

                _runtime.Start();
                SubscribeEvents();

                _running = true;
                SetRunningState(true);
                AppendResult("watchdog started  (pipe = " + _pipeName + ")");
            }
            catch (Exception ex)
            {
                AppendError("Start failed: " + ex.Message);
                TeardownRuntime();
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            if (!_running) return;

            AppendResult("stopping watchdog...");
            TeardownRuntime();
            SetRunningState(false);
            SetState("stopped");
            AppendResult("watchdog stopped");
        }

        private void TeardownRuntime()
        {
            try { if (_events != null) _events.Dispose(); } catch { }
            _events = null;

            try { if (_runtime != null) _runtime.Stop(); } catch { }
            try { if (_runtime != null) _runtime.Dispose(); } catch { }
            _runtime = null;

            _running = false;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // The monitor thread is foreground; tear it down so the process exits.
            TeardownRuntime();
        }

        // =====================================================================
        // RPC commands
        // =====================================================================

        private async void btnPing_Click(object sender, EventArgs e) => await SendCommand(WatchdogCommands.Ping);
        private async void btnStatus_Click(object sender, EventArgs e) => await SendCommand(WatchdogCommands.Status);
        private async void btnPause_Click(object sender, EventArgs e) => await SendCommand(WatchdogCommands.Pause);
        private async void btnResume_Click(object sender, EventArgs e) => await SendCommand(WatchdogCommands.Resume);
        private async void btnRestart_Click(object sender, EventArgs e) => await SendCommand(WatchdogCommands.Restart);

        private async Task SendCommand(string cmd)
        {
            if (!_running || string.IsNullOrEmpty(_pipeName))
            {
                AppendError("Watchdog is not running.");
                return;
            }

            try
            {
                var client = new PipeClient(new PipeClientOptions
                {
                    PipeName = _pipeName,
                    ConnectTimeout = TimeSpan.FromMilliseconds(1500),
                    RequestTimeout = TimeSpan.FromMilliseconds(3000)
                });
                var response = await client.SendAsync(cmd);

                if (response.Ok)
                {
                    var data = response.Data != null ? response.Data.ToString() : "(no data)";
                    AppendResult(cmd + " -> " + data);
                }
                else
                {
                    var code = response.Error != null ? response.Error.Code : "unknown";
                    AppendError(cmd + " -> error=" + code);
                }
            }
            catch (TimeoutException)
            {
                AppendError(cmd + " -> watchdog_not_running (timeout)");
            }
            catch (Exception ex)
            {
                AppendError(cmd + " -> " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        // =====================================================================
        // Event stream (log + telemetry)
        // =====================================================================

        private void SubscribeEvents()
        {
            _events = new PipeEventClient(_pipeName);
            _events.OnEvent += OnPipeEvent;
            _events.Start();
        }

        private void OnPipeEvent(PipeMessage msg)
        {
            // OnEvent fires on a background listener thread — marshal to the UI.
            if (IsDisposed || !IsHandleCreated) return;

            try
            {
                BeginInvoke((Action)(() => HandleEvent(msg)));
            }
            catch
            {
                // form closing mid-event — safe to drop
            }
        }

        private void HandleEvent(PipeMessage msg)
        {
            if (msg == null || msg.Data == null) return;

            if (msg.Name == "log")
            {
                var line = msg.Data["line"] != null ? msg.Data["line"].ToString() : null;
                var level = msg.Data["level"] != null ? msg.Data["level"].ToString() : "info";
                if (string.IsNullOrEmpty(line))
                    line = msg.Data["msg"] != null ? msg.Data["msg"].ToString() : "";

                AppendColored(rtbLog, line, LevelColor(level));
            }
            else if (msg.Name == "telemetry")
            {
                var state = msg.Data["state"] != null ? msg.Data["state"].ToString() : "?";
                var pid = msg.Data["childPid"] != null ? msg.Data["childPid"].ToString() : "-";
                var restarts = msg.Data["restartCount"] != null ? msg.Data["restartCount"].ToString() : "0";
                SetState(state + "   childPid=" + pid + "   restarts=" + restarts);
            }
        }

        // =====================================================================
        // UI helpers
        // =====================================================================

        private void SetRunningState(bool running)
        {
            btnStart.Enabled = !running;
            txtTarget.Enabled = !running;
            btnBrowse.Enabled = !running;

            btnStop.Enabled = running;
            btnPing.Enabled = running;
            btnStatus.Enabled = running;
            btnPause.Enabled = running;
            btnResume.Enabled = running;
            btnRestart.Enabled = running;
        }

        private void SetState(string text)
        {
            lblState.Text = "State: " + text;
        }

        private static Color LevelColor(string level)
        {
            if (level == "error") return Color.Tomato;
            if (level == "warn") return Color.Gold;
            return Color.Gainsboro;
        }

        private void AppendResult(string text)
        {
            AppendColored(rtbErrors, "[ok]  " + text, Color.PaleGreen);
        }

        private void AppendError(string text)
        {
            AppendColored(rtbErrors, "[err] " + text, Color.Tomato);
        }

        private static void AppendColored(RichTextBox box, string text, Color color)
        {
            box.SelectionStart = box.TextLength;
            box.SelectionLength = 0;
            box.SelectionColor = color;
            box.AppendText(text + Environment.NewLine);
            box.SelectionColor = box.ForeColor;
            box.ScrollToCaret();
        }

        // =====================================================================
        // Browse
        // =====================================================================

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*";
                dlg.Title = "Choose a target executable to supervise";

                if (dlg.ShowDialog(this) == DialogResult.OK)
                    txtTarget.Text = dlg.FileName;
            }
        }
    }
}
