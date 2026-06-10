using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NekoLib.Diagnostics;
using NekoLib.Diagnostics.Contracts;
using NekoLib.Diagnostics.Sinks;
using NekoLib.Pipes;
using NekoLib.Watchdog;

#if NET9
using System.Text.Json;
#else
using Newtonsoft.Json.Linq;
#endif

namespace NekoLib.Observability.RuntimeTests.WinForms
{
    public sealed class RuntimeHubForm : Form
    {
        private readonly string _runtimeRoot;
        private readonly List<Process> _ownedProcesses = new List<Process>();

        private readonly TextBox _diagnosticsLog;
        private readonly TextBox _pipesLog;
        private readonly TextBox _watchdogLog;
        private readonly TextBox _integrationLog;
        private readonly TextBox _bootstrapLog;
        private readonly ToolStripStatusLabel _statusLabel;

        private PipeServer _pipeServer;
        private PipeEventClient _pipeEventClient;
        private SimplePipeMetrics _pipeMetrics;
        private string _pipeName;

        private WatchdogRuntime _watchdogRuntime;
        private Process _watchdogHostProcess;
        private PipeEventClient _watchdogEvents;
        private string _watchdogPipeName;
        private string _watchdogTargetPath;
        private string _watchdogWorkRoot;

        private TextBox _targetPathBox;
        private TextBox _targetArgsBox;
        private TextBox _hostPathBox;
        private TextBox _watchdogRootBox;

        public RuntimeHubForm()
        {
            Text = "NekoLib Observability Runtime Tests";
            Width = 1180;
            Height = 760;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(980, 620);

            _runtimeRoot = Path.Combine(
                Path.GetTempPath(),
                "NekoLib.RuntimeTests",
                "Observability-" + Process.GetCurrentProcess().Id + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_runtimeRoot);

            var tabs = new TabControl { Dock = DockStyle.Fill };
            Controls.Add(tabs);

            _diagnosticsLog = AddTab(tabs, "Diagnostics", BuildDiagnosticsTab);
            _pipesLog = AddTab(tabs, "Pipes", BuildPipesTab);
            _watchdogLog = AddTab(tabs, "Watchdog", BuildWatchdogTab);
            _integrationLog = AddTab(tabs, "Integration", BuildIntegrationTab);
            _bootstrapLog = AddTab(tabs, "Bootstrap Flow", BuildBootstrapTab);

            var status = new StatusStrip();
            _statusLabel = new ToolStripStatusLabel();
            status.Items.Add(_statusLabel);
            Controls.Add(status);
            UpdateStatus();

            FormClosing += (s, e) => Cleanup();
        }

        private TextBox AddTab(TabControl tabs, string title, Func<TextBox, Control> builder)
        {
            var page = new TabPage(title);
            var log = CreateLogBox();
            page.Controls.Add(builder(log));
            tabs.TabPages.Add(page);
            return log;
        }

        private Control BuildDiagnosticsTab(TextBox log)
        {
            var panel = CreateSplitPanel();
            var buttons = CreateButtonPanel();
            panel.Panel1.Controls.Add(buttons);
            panel.Panel2.Controls.Add(log);

            AddButton(buttons, "Emit Logs", (s, e) => RunDiagnosticsLogs());
            AddButton(buttons, "Telemetry Snapshot", (s, e) => RunTelemetrySnapshot());
            AddButton(buttons, "Diagnostics.Null", (s, e) => RunDiagnosticsNull());
            AddButton(buttons, "Crash Dry Scenario", (s, e) => RunCrashDryScenario());
            AddButton(buttons, "Open Temp Root", (s, e) => OpenFolder(_runtimeRoot));
            AddButton(buttons, "Clear", (s, e) => log.Clear());

            Append(log, "Runtime root: " + _runtimeRoot);
            return panel;
        }

        private Control BuildPipesTab(TextBox log)
        {
            var panel = CreateSplitPanel();
            var buttons = CreateButtonPanel();
            panel.Panel1.Controls.Add(buttons);
            panel.Panel2.Controls.Add(log);

            AddButton(buttons, "Start Server", (s, e) => StartPipeServer());
            AddButton(buttons, "Stop Server", (s, e) => StopPipeServer());
            AddButton(buttons, "Start Events", (s, e) => StartPipeEvents());
            AddButton(buttons, "Ping", async (s, e) => await SendPipeCommand("ping"));
            AddButton(buttons, "Echo", async (s, e) => await SendPipeCommand("echo", new { text = "hello from runtime hub" }));
            AddButton(buttons, "Slow", async (s, e) => await SendPipeCommand("slow"));
            AddButton(buttons, "Big", async (s, e) => await SendPipeCommand("big"));
            AddButton(buttons, "Unknown", async (s, e) => await SendPipeCommand("unknown"));
            AddButton(buttons, "Publish Event", async (s, e) => await PublishPipeEvent());
            AddButton(buttons, "Metrics", (s, e) => ShowPipeMetrics());
            AddButton(buttons, "Clear", (s, e) => log.Clear());

            Append(log, "Pipe runtime tab is idle. Start the server first.");
            return panel;
        }

        private Control BuildWatchdogTab(TextBox log)
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 235));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 5, Padding = new Padding(8) };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

            _targetPathBox = AddLabeledText(top, 0, "Target", CmdPath());
            _targetArgsBox = AddLabeledText(top, 1, "Args", "/c ping -n 30 127.0.0.1 > nul");
            _hostPathBox = AddLabeledText(top, 2, "Host.exe", ResolveHostPath());
            _watchdogRootBox = AddLabeledText(top, 3, "Work root", Path.Combine(_runtimeRoot, "watchdog"));
            top.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));

            var commands = CreateButtonPanel();
            commands.Dock = DockStyle.Fill;
            commands.Margin = new Padding(0, 8, 0, 0);
            AddButton(commands, "Start In-Process", (s, e) => StartInProcessWatchdog());
            AddButton(commands, "Start Host.exe", (s, e) => StartHostWatchdog());
            AddButton(commands, "Subscribe Logs", (s, e) => SubscribeWatchdogLogs());
            AddButton(commands, "Status", async (s, e) => await SendWatchdogCommand("status"));
            AddButton(commands, "Pause", async (s, e) => await SendWatchdogCommand("pause"));
            AddButton(commands, "Resume", async (s, e) => await SendWatchdogCommand("resume"));
            AddButton(commands, "Restart", async (s, e) => await SendWatchdogCommand("restart"));
            AddButton(commands, "Stop", async (s, e) => await SendWatchdogCommand("stop"));
            AddButton(commands, "Seed Rotation", (s, e) => SeedWatchdogRotation());
            AddButton(commands, "Pending Crash", (s, e) => CreatePendingCrashFolder());
            AddButton(commands, "Open Root", (s, e) => OpenFolder(CurrentWatchdogRoot()));
            AddButton(commands, "Clear", (s, e) => log.Clear());
            top.Controls.Add(commands, 0, 4);
            top.SetColumnSpan(commands, 4);

            root.Controls.Add(top, 0, 0);
            root.Controls.Add(log, 0, 1);
            Append(log, "Use In-Process for direct runtime coverage or Host.exe for deployment-shaped coverage.");
            return root;
        }

        private Control BuildIntegrationTab(TextBox log)
        {
            var panel = CreateSplitPanel();
            var buttons = CreateButtonPanel();
            panel.Panel1.Controls.Add(buttons);
            panel.Panel2.Controls.Add(log);

            AddButton(buttons, "Start Integration Watchdog", (s, e) => StartIntegrationWatchdog());
            AddButton(buttons, "Notify Crash + Bundle", (s, e) => RunIntegrationCrashScenario());
            AddButton(buttons, "Show Bundles", (s, e) => ShowBundles(_integrationLog));
            AddButton(buttons, "Stop Watchdog", async (s, e) => await SendWatchdogCommand("stop"));
            AddButton(buttons, "Open Root", (s, e) => OpenFolder(CurrentWatchdogRoot()));
            AddButton(buttons, "Clear", (s, e) => log.Clear());

            Append(log, "Integration wires CrashHandler.ExternalNotifier to WatchdogController.NotifyExceptionForTarget.");
            return panel;
        }

        private Control BuildBootstrapTab(TextBox log)
        {
            var panel = CreateSplitPanel();
            var buttons = CreateButtonPanel();
            panel.Panel1.Controls.Add(buttons);
            panel.Panel2.Controls.Add(log);

            AddButton(buttons, "Explain Flow", (s, e) => ExplainBootstrapFlow());
            AddButton(buttons, "Simulate Bootstrap", (s, e) => SimulateBootstrap());
            AddButton(buttons, "Stop Simulated Host", async (s, e) => await SendWatchdogCommand("stop"));
            AddButton(buttons, "Clear", (s, e) => log.Clear());

            ExplainBootstrapFlow();
            return panel;
        }

        private void RunDiagnosticsLogs()
        {
            var logger = new Logger(LogLevel.Trace, new UiLogSink(_diagnosticsLog));
            logger.Trace("trace sample");
            logger.Debug("debug sample");
            logger.Info("info sample");
            logger.Warn("warn sample");
            logger.Error("error sample", new InvalidOperationException("diagnostics runtime error"));
            logger.Fatal("fatal sample", new ApplicationException("diagnostics runtime fatal"));
        }

        private void RunTelemetrySnapshot()
        {
            var telemetry = new MemoryTelemetrySink();
            telemetry.Track(new TelemetryEvent(DateTime.UtcNow, "runtime.diagnostics.started"));
            telemetry.Track(new TelemetryEvent(DateTime.UtcNow, "runtime.diagnostics.operation", TimeSpan.FromMilliseconds(42),
                new Dictionary<string, object> { { "tab", "Diagnostics" }, { "ok", true } }));

            foreach (var evt in telemetry.Snapshot())
                Append(_diagnosticsLog, "telemetry " + evt.Name + " duration=" + evt.Duration + " data=" + evt.Data.Count);
        }

        private void RunDiagnosticsNull()
        {
            NekoLib.Diagnostics.Diagnostics.Null.Logger.Info("null logger info");
            NekoLib.Diagnostics.Diagnostics.Null.Logger.Error("null logger error", new Exception("swallowed"));
            NekoLib.Diagnostics.Diagnostics.Null.Telemetry.Track(new TelemetryEvent(DateTime.UtcNow, "null.telemetry"));
            Append(_diagnosticsLog, "Diagnostics.Null accepted logger and telemetry calls.");
        }

        private void RunCrashDryScenario()
        {
            var root = Path.Combine(_runtimeRoot, "diagnostics-crashes");
            using (var handler = new CrashHandler(new CrashHandlerOptions
            {
                CrashRootDirectory = root,
                DumpLevel = CrashDumpLevel.None,
                NotifyWatchdog = false,
                ExtraLines = () => new[] { "runtime-tab=Diagnostics", "mode=dry" }
            }))
            {
                handler.CrashDetected += (s, e) => Append(_diagnosticsLog, "crash detected source=" + e.Source);
                handler.CrashBundleWritten += (s, e) => Append(_diagnosticsLog, "crash folder=" + e.BundleDirectory);
                InvokeCrash(handler, "runtime-test-dry", new InvalidOperationException("dry crash scenario"), false);
            }
        }

        private void StartPipeServer()
        {
            StopPipeServer();

            _pipeName = "NekoLib.RuntimeTests.Pipes." + Guid.NewGuid().ToString("N");
            _pipeMetrics = new SimplePipeMetrics();
            _pipeServer = new PipeServer(new PipeServerOptions
            {
                PipeName = _pipeName,
                EnableEvents = true,
                MaxEventSubscribers = 4,
                Metrics = _pipeMetrics,
                ClientIdleTimeout = TimeSpan.FromSeconds(10)
            });

            _pipeServer.Map("ping", (req, ct) => Task.FromResult(PipeOk("pong")));
            _pipeServer.Map("echo", (req, ct) => Task.FromResult(PipeOk(new { received = DataToText(req.Data), utc = DateTime.UtcNow })));
            _pipeServer.Map("slow", async (req, ct) =>
            {
                await Task.Delay(750, ct).ConfigureAwait(false);
                return PipeOk("slow-ok");
            });
            _pipeServer.Map("big", (req, ct) => Task.FromResult(PipeOk(new string('x', 32 * 1024))));
            _pipeServer.Start();

            Append(_pipesLog, "server started pipe=" + _pipeName);
        }

        private void StopPipeServer()
        {
            try { _pipeEventClient?.Dispose(); } catch { }
            try { _pipeServer?.Dispose(); } catch { }
            _pipeEventClient = null;
            _pipeServer = null;
            Append(_pipesLog, "server stopped");
        }

        private void StartPipeEvents()
        {
            if (string.IsNullOrWhiteSpace(_pipeName))
            {
                Append(_pipesLog, "start server first");
                return;
            }

            try { _pipeEventClient?.Dispose(); } catch { }
            _pipeEventClient = new PipeEventClient(_pipeName) { ReconnectDelay = TimeSpan.FromMilliseconds(250) };
            _pipeEventClient.OnConnected += () => Append(_pipesLog, "event client connected");
            _pipeEventClient.OnDisconnected += () => Append(_pipesLog, "event client disconnected");
            _pipeEventClient.OnEvent += msg => Append(_pipesLog, "event " + msg.Name + " data=" + DataToText(msg.Data));
            _pipeEventClient.Start();
            Append(_pipesLog, "event client started");
        }

        private async Task SendPipeCommand(string command, object payload = null)
        {
            if (string.IsNullOrWhiteSpace(_pipeName))
            {
                Append(_pipesLog, "start server first");
                return;
            }

            try
            {
                using (var client = NewPipeClient(_pipeName))
                {
                    var response = await client.SendAsync(command, payload).ConfigureAwait(true);
                    Append(_pipesLog, command + " " + ResponseToText(response));
                }
            }
            catch (Exception ex)
            {
                Append(_pipesLog, command + " failed: " + ex.Message);
            }
        }

        private async Task PublishPipeEvent()
        {
            if (_pipeServer?.Events == null)
            {
                Append(_pipesLog, "server/events not started");
                return;
            }

            await _pipeServer.Events.PublishAsync("runtime.event", new { utc = DateTime.UtcNow, text = "manual event" });
            Append(_pipesLog, "event published");
        }

        private void ShowPipeMetrics()
        {
            if (_pipeMetrics == null)
            {
                Append(_pipesLog, "metrics unavailable; start server first");
                return;
            }

            var s = _pipeMetrics.Snapshot();
            Append(_pipesLog,
                "metrics server req=" + s.Server.Requests +
                " ok=" + s.Server.Success +
                " fail=" + s.Server.Failures +
                " clients=" + s.Server.ConnectedClients +
                " events=" + s.Events.Published + "/" + s.Events.Delivered +
                " errors=" + s.Errors.Total);
        }

        private void StartInProcessWatchdog()
        {
            StopWatchdogObjects(false);

            var options = NewWatchdogOptions("watchdog-inproc");
            _watchdogRuntime = new WatchdogRuntime(options);
            _watchdogRuntime.Start();
            SetWatchdogState(options);
            Append(_watchdogLog, "in-process watchdog started pipe=" + _watchdogPipeName);
        }

        private void StartHostWatchdog()
        {
            StopWatchdogObjects(false);

            var target = TargetPath();
            var host = _hostPathBox.Text.Trim();
            if (!File.Exists(host))
            {
                Append(_watchdogLog, "host not found: " + host);
                return;
            }

            _watchdogTargetPath = target;
            _watchdogPipeName = WatchdogController.ResolvePipeNameForTarget(target);
            _watchdogWorkRoot = CurrentWatchdogRoot();
            Directory.CreateDirectory(_watchdogWorkRoot);

            var psi = new ProcessStartInfo
            {
                FileName = host,
                Arguments = "--target " + Quote(target) +
                    " --args " + Quote(_targetArgsBox.Text) +
                    " --workdir " + Quote(_watchdogWorkRoot),
                WorkingDirectory = _watchdogWorkRoot,
                UseShellExecute = false
            };

            _watchdogHostProcess = Process.Start(psi);
            TrackProcess(_watchdogHostProcess);
            Append(_watchdogLog, "Host.exe started pid=" + _watchdogHostProcess.Id + " pipe=" + _watchdogPipeName);
        }

        private void StartIntegrationWatchdog()
        {
            StopWatchdogObjects(false);
            var options = NewWatchdogOptions("integration");
            options.TargetArguments = "/c ping -n 4 127.0.0.1 > nul";
            _watchdogRuntime = new WatchdogRuntime(options);
            _watchdogRuntime.Start();
            SetWatchdogState(options);
            Append(_integrationLog, "integration watchdog started pipe=" + _watchdogPipeName);
        }

        private async Task SendWatchdogCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(_watchdogPipeName))
            {
                Append(_watchdogLog, "start watchdog first");
                Append(_integrationLog, "start watchdog first");
                Append(_bootstrapLog, "start/simulate watchdog first");
                return;
            }

            try
            {
                using (var client = NewPipeClient(_watchdogPipeName))
                {
                    var response = await client.SendAsync(command).ConfigureAwait(true);
                    var line = "watchdog " + command + " " + ResponseToText(response);
                    Append(_watchdogLog, line);
                    Append(_integrationLog, line);
                    Append(_bootstrapLog, line);
                }
            }
            catch (Exception ex)
            {
                Append(_watchdogLog, "watchdog " + command + " failed: " + ex.Message);
            }
        }

        private void SubscribeWatchdogLogs()
        {
            if (string.IsNullOrWhiteSpace(_watchdogPipeName))
            {
                Append(_watchdogLog, "start watchdog first");
                return;
            }

            try { _watchdogEvents?.Dispose(); } catch { }
            ReplayWatchdogHistory(_watchdogLog);

            _watchdogEvents = new PipeEventClient(_watchdogPipeName);
            _watchdogEvents.OnConnected += () => Append(_watchdogLog, "watchdog event stream connected");
            _watchdogEvents.OnDisconnected += () => Append(_watchdogLog, "watchdog event stream disconnected");
            _watchdogEvents.OnEvent += msg =>
            {
                if (msg.Name == "log")
                    Append(_watchdogLog, "live " + DataToText(msg.Data));
                else
                    Append(_watchdogLog, "event " + msg.Name + " " + DataToText(msg.Data));
            };
            _watchdogEvents.Start();
        }

        private void ReplayWatchdogHistory(TextBox log)
        {
            try
            {
                using (var client = NewPipeClient(_watchdogPipeName))
                {
                    var response = client.SendAsync("log_history").GetAwaiter().GetResult();
                    Append(log, "history ok=" + response.Ok + " data=" + DataToText(response.Data));
                }
            }
            catch (Exception ex)
            {
                Append(log, "history failed: " + ex.Message);
            }
        }

        private void SeedWatchdogRotation()
        {
            var root = CurrentWatchdogRoot();
            Directory.CreateDirectory(root);
            var log = Path.Combine(root, "watchdog.log");
            File.WriteAllText(log, new string('x', 70 * 1024));
            Append(_watchdogLog, "seeded log over 64KiB: " + log);
        }

        private void CreatePendingCrashFolder()
        {
            var root = CurrentWatchdogRoot();
            var pending = Path.Combine(root, "crash", "pending", "crash-manual-" + DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss-fffZ"));
            Directory.CreateDirectory(pending);
            File.WriteAllText(Path.Combine(pending, "crash.txt"), "manual pending crash " + DateTime.UtcNow.ToString("O"));
            Append(_watchdogLog, "pending crash folder created: " + pending);
            Append(_watchdogLog, "Use Restart or wait for child exit to finalize.");
        }

        private void RunIntegrationCrashScenario()
        {
            if (string.IsNullOrWhiteSpace(_watchdogPipeName) || string.IsNullOrWhiteSpace(_watchdogTargetPath))
            {
                Append(_integrationLog, "start integration watchdog first");
                return;
            }

            var old = Environment.GetEnvironmentVariable("NEKO_UNDER_WATCHDOG");
            try
            {
                Environment.SetEnvironmentVariable("NEKO_UNDER_WATCHDOG", "1");
                using (var handler = new CrashHandler(new CrashHandlerOptions
                {
                    CrashRootDirectory = Path.Combine(CurrentWatchdogRoot(), "crash", "pending"),
                    DumpLevel = CrashDumpLevel.None,
                    ExternalNotifier = e => WatchdogController.NotifyExceptionForTarget(
                        _watchdogTargetPath,
                        e.Exception == null ? null : e.Exception.GetType().FullName,
                        e.Exception == null ? null : e.Exception.Message,
                        e.Source)
                }))
                {
                    InvokeCrash(handler, "runtime-integration", new InvalidOperationException("integration crash"), false);
                }

                Append(_integrationLog, "crash notified. Waiting for child exit/final bundle...");
                Task.Run(async () =>
                {
                    await Task.Delay(5500).ConfigureAwait(false);
                    BeginInvoke(new Action(() => ShowBundles(_integrationLog)));
                });
            }
            finally
            {
                Environment.SetEnvironmentVariable("NEKO_UNDER_WATCHDOG", old);
            }
        }

        private void ShowBundles(TextBox log)
        {
            var root = Path.Combine(CurrentWatchdogRoot(), "crash", "bundles");
            if (!Directory.Exists(root))
            {
                Append(log, "bundle root does not exist: " + root);
                return;
            }

            var dirs = Directory.GetDirectories(root, "bundle-*");
            Append(log, "bundles found=" + dirs.Length);
            foreach (var dir in dirs)
            {
                Append(log, "bundle " + dir);
                foreach (var file in Directory.GetFiles(dir))
                    Append(log, "  " + Path.GetFileName(file));
            }
        }

        private void ExplainBootstrapFlow()
        {
            Append(_bootstrapLog, "recommended real-app flow:");
            Append(_bootstrapLog, "1. App starts normally.");
            Append(_bootstrapLog, "2. App checks NEKO_UNDER_WATCHDOG.");
            Append(_bootstrapLog, "3. If missing, app launches NekoLib.Watchdog.Host.exe --target <app>.");
            Append(_bootstrapLog, "4. Original app exits cleanly; supervisor does not need to kill it.");
            Append(_bootstrapLog, "5. Watchdog launches the supervised app with NEKO_UNDER_WATCHDOG=1.");
            Append(_bootstrapLog, "6. Supervised app sees the env var and does not bootstrap again.");
            Append(_bootstrapLog, "alternatives: launcher-first shortcut, same-exe dual-mode, or Windows Service.");
        }

        private void SimulateBootstrap()
        {
            Append(_bootstrapLog, "bootstrap instance pid=" + Process.GetCurrentProcess().Id);
            Append(_bootstrapLog, "NEKO_UNDER_WATCHDOG=" + (Environment.GetEnvironmentVariable("NEKO_UNDER_WATCHDOG") ?? "(missing)"));
            Append(_bootstrapLog, "simulating host launch against this app's --supervised-probe mode, without exiting this hub.");

            var probePath = Path.Combine(_runtimeRoot, "bootstrap", "supervised-probe.txt");
            _targetPathBox.Text = Process.GetCurrentProcess().MainModule.FileName;
            _targetArgsBox.Text = "--supervised-probe " + Quote(probePath) + " 4";
            StartHostWatchdog();
            Append(_bootstrapLog, "simulated bootstrap would now exit cleanly in a real app.");
            Append(_bootstrapLog, "probe output: " + probePath);
        }

        private WatchdogOptions NewWatchdogOptions(string name)
        {
            var root = Path.Combine(_runtimeRoot, name);
            Directory.CreateDirectory(root);

            var options = new WatchdogOptions
            {
                TargetPath = TargetPath(),
                TargetArguments = _targetArgsBox.Text,
                WorkingDirectory = root,
                LogPath = Path.Combine(root, "watchdog.log"),
                PendingCrashRoot = Path.Combine(root, "crash", "pending"),
                BundleRoot = Path.Combine(root, "crash", "bundles"),
                RestartDelayMs = 500,
                MonitorPollMs = 100,
                HeartbeatIntervalMs = 0,
                GracefulKillTimeoutMs = 200,
                ForceKillTimeoutMs = 1000,
                MaxLogBytes = 64 * 1024,
                LogSinks = new ILogSink[] { new UiLogSink(_watchdogLog), new UiLogSink(_integrationLog) }
            };
            options.Normalize();
            return options;
        }

        private void SetWatchdogState(WatchdogOptions options)
        {
            _watchdogTargetPath = options.TargetPath;
            _watchdogPipeName = options.PipeName;
            _watchdogWorkRoot = options.WorkingDirectory;
            _watchdogRootBox.Text = _watchdogWorkRoot;
        }

        private string CurrentWatchdogRoot()
        {
            if (!string.IsNullOrWhiteSpace(_watchdogWorkRoot))
                return _watchdogWorkRoot;

            var value = _watchdogRootBox == null ? null : _watchdogRootBox.Text;
            return string.IsNullOrWhiteSpace(value) ? Path.Combine(_runtimeRoot, "watchdog") : value.Trim();
        }

        private string TargetPath()
        {
            var target = _targetPathBox == null ? null : _targetPathBox.Text;
            return string.IsNullOrWhiteSpace(target) ? CmdPath() : target.Trim();
        }

        private static string CmdPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        }

        private string ResolveHostPath()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var tfm = baseDir.IndexOf("net9.0-windows", StringComparison.OrdinalIgnoreCase) >= 0 ? "net9.0-windows" : "net481";
            foreach (var start in new[] { baseDir, Directory.GetCurrentDirectory() })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    var debug = Path.Combine(dir.FullName, "src", "Watchdog", "NekoLib.Watchdog.Host", "bin", "Debug", tfm, "NekoLib.Watchdog.Host.exe");
                    if (File.Exists(debug))
                        return debug;

                    var release = Path.Combine(dir.FullName, "src", "Watchdog", "NekoLib.Watchdog.Host", "bin", "Release", tfm, "NekoLib.Watchdog.Host.exe");
                    if (File.Exists(release))
                        return release;

                    dir = dir.Parent;
                }
            }

            return Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "..", "src", "Watchdog", "NekoLib.Watchdog.Host", "bin", "Debug", tfm, "NekoLib.Watchdog.Host.exe"));
        }

        private static PipeClient NewPipeClient(string pipeName)
        {
            return new PipeClient(new PipeClientOptions
            {
                PipeName = pipeName,
                ConnectTimeout = TimeSpan.FromSeconds(3),
                RequestTimeout = TimeSpan.FromSeconds(5)
            });
        }

        private static PipeMessage PipeOk(object payload)
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

        private static string DataToText(object data)
        {
            if (data == null)
                return "";

#if NET9
            if (data is JsonElement element)
                return element.ToString();
#else
            if (data is JToken token)
                return token.ToString(Newtonsoft.Json.Formatting.None);
#endif

            return data.ToString();
        }

        private static string ResponseToText(PipeMessage response)
        {
            if (response == null)
                return "ok=False error=(null response)";

            var text = "ok=" + response.Ok;

            var data = DataToText(response.Data);
            if (!string.IsNullOrWhiteSpace(data))
                text += " data=" + data;

            if (response.Error != null)
                text += " error=" + response.Error.Code + ":" + response.Error.Message;

            return text;
        }

        private static void InvokeCrash(CrashHandler handler, string source, Exception ex, bool terminating)
        {
            var method = typeof(CrashHandler).GetMethod("HandleCrash", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
                throw new InvalidOperationException("CrashHandler.HandleCrash not found.");

            method.Invoke(handler, new object[] { source, ex, terminating });
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }

        private void TrackProcess(Process process)
        {
            if (process != null)
                _ownedProcesses.Add(process);
        }

        private void StopWatchdogObjects(bool finalCleanup)
        {
            try { _watchdogEvents?.Dispose(); } catch { }
            _watchdogEvents = null;

            try
            {
                if (!string.IsNullOrWhiteSpace(_watchdogPipeName))
                {
                    using (var client = NewPipeClient(_watchdogPipeName))
                        client.SendAsync("stop").GetAwaiter().GetResult();
                }
            }
            catch { }

            try { _watchdogRuntime?.Stop(true); } catch { }
            try { _watchdogRuntime?.Dispose(); } catch { }
            _watchdogRuntime = null;

            try
            {
                if (_watchdogHostProcess != null && !_watchdogHostProcess.HasExited)
                {
                    if (!_watchdogHostProcess.WaitForExit(1500))
                        _watchdogHostProcess.Kill();
                }
            }
            catch { }

            _watchdogHostProcess = null;
            if (finalCleanup)
            {
                _watchdogPipeName = null;
                _watchdogTargetPath = null;
            }
        }

        private void Cleanup()
        {
            StopPipeServer();
            StopWatchdogObjects(true);

            foreach (var process in _ownedProcesses.ToArray())
            {
                try
                {
                    if (process != null && !process.HasExited)
                        process.Kill();
                }
                catch { }
            }
        }

        private void UpdateStatus()
        {
            _statusLabel.Text = "TFM=" + TfmName() +
                " | PID=" + Process.GetCurrentProcess().Id +
                " | Temp=" + _runtimeRoot;
        }

        private static string TfmName()
        {
#if NET9
            return "net9.0-windows";
#else
            return "net481";
#endif
        }

        private static SplitContainer CreateSplitPanel()
        {
            var panel = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 240,
                FixedPanel = FixedPanel.Panel1
            };
            return panel;
        }

        private static FlowLayoutPanel CreateButtonPanel()
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = true,
                Padding = new Padding(8)
            };
        }

        private static void AddButton(FlowLayoutPanel panel, string text, EventHandler handler)
        {
            var button = new Button
            {
                Text = text,
                Width = 150,
                Height = 30,
                Margin = new Padding(4)
            };
            button.Click += handler;
            panel.Controls.Add(button);
        }

        private static TextBox AddLabeledText(TableLayoutPanel table, int row, string label, string value)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
            var lbl = new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            var box = new TextBox { Text = value, Dock = DockStyle.Fill };
            table.Controls.Add(lbl, 0, row);
            table.Controls.Add(box, 1, row);
            table.SetColumnSpan(box, 3);
            return box;
        }

        private static TextBox CreateLogBox()
        {
            return new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                ReadOnly = true,
                WordWrap = false,
                Font = new Font(FontFamily.GenericMonospace, 9f)
            };
        }

        private static void OpenFolder(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch { }
        }

        private static void Append(TextBox box, string message)
        {
            if (box == null)
                return;

            if (box.InvokeRequired)
            {
                try { box.BeginInvoke(new Action(() => Append(box, message))); } catch { }
                return;
            }

            box.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + Environment.NewLine);
        }

        private sealed class UiLogSink : ILogSink
        {
            private readonly TextBox _box;

            public UiLogSink(TextBox box)
            {
                _box = box;
            }

            public void Write(LogEntry entry)
            {
                Append(_box, entry == null ? "(null log)" : entry.ToString());
            }
        }
    }
}
