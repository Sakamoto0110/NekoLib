#nullable enable
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Core.Logging;
using NekoLib.Pipes;
using NekoLib.Watchdog.RuntimeTests.CrashRecovery.Shared;

namespace NekoLib.Watchdog.RuntimeTests.CrashRecovery.Child
{
    internal sealed class ChildRuntime
    {
        private readonly ChildOptions _options;
        private readonly string[] _arguments;
        private readonly ManualResetEventSlim _shutdown = new ManualResetEventSlim(false);

        public ChildRuntime(ChildOptions options, string[] arguments)
        {
            _options = options;
            _arguments = arguments;
        }

        public int Run()
        {
            ChildPlan plan = ChildPlan.Load(_options.PlanPath);
            Directory.CreateDirectory(_options.RunRoot);

            // Initial launch starts the deployed Host and waits for the bounded
            // PID/token attach. Host-started generations carry the recursion
            // environment marker and this call becomes the documented no-op.
            WatchdogBootstrap.EnsureStarted(_arguments, 10000);

            using (Process current = Process.GetCurrentProcess())
            {
                string argumentsHash = ChildState.ArgumentsHash(_arguments);
                int generation = ChildState.ClaimGeneration(
                    _options.RunRoot, plan.CampaignId, current.Id, argumentsHash);
                string controlPipe = ControlPipe(plan.CampaignId, generation);
                string logToken = "E3-WDOG/" + plan.CampaignId + "/generation/" +
                                  generation.ToString(CultureInfo.InvariantCulture) + "/ready";

                using (PipeServer server = BuildControlServer(controlPipe, plan, generation, current.Id, argumentsHash))
                {
                    server.Start();

                    long readyTimestamp = Stopwatch.GetTimestamp();
                    ChildState.WriteReady(
                        _options.RunRoot,
                        plan.CampaignId,
                        generation,
                        current.Id,
                        controlPipe,
                        argumentsHash,
                        logToken,
                        readyTimestamp);

                    WatchdogController.NotifyLog(new LogEntry(
                        DateTime.UtcNow,
                        LogLevel.Info,
                        logToken,
                        category: "E3-WDOG.Child"));

                    while (!_shutdown.IsSet)
                    {
                        ChildPlanEvent? due = FindDueChildEvent(plan, generation);
                        if (due != null)
                        {
                            WaitForControllerProbe(generation);
                            return ExecutePlannedEvent(plan, due, generation, current.Id);
                        }

                        _shutdown.Wait(TimeSpan.FromMilliseconds(50));
                    }
                }
            }

            return 0;
        }

        private void WaitForControllerProbe(int generation)
        {
            string path = ChildState.ProbePath(_options.RunRoot, generation);
            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            while (!File.Exists(path) && DateTime.UtcNow < deadline && !_shutdown.IsSet)
                _shutdown.Wait(TimeSpan.FromMilliseconds(50));

            if (!File.Exists(path))
            {
                throw new InvalidOperationException(
                    "E3-WDOG controller did not acknowledge health for generation " + generation + ".");
            }
        }

        private PipeServer BuildControlServer(
            string pipeName,
            ChildPlan plan,
            int generation,
            int pid,
            string argumentsHash)
        {
            PipeServer server = new PipeServer(new PipeServerOptions
            {
                PipeName = pipeName,
                EnableEvents = false,
                MaxClients = 2,
                AccessPolicy = PipeAccessPolicy.CurrentUserOnly
            });

            server.Map("health", (request, ct) => Task.FromResult(Reply(
                plan.CampaignId + "|" + generation.ToString(CultureInfo.InvariantCulture) + "|" +
                pid.ToString(CultureInfo.InvariantCulture) + "|ready|" + argumentsHash)));

            server.Map("shutdown", (request, ct) =>
            {
                _shutdown.Set();
                return Task.FromResult(Reply("shutting-down"));
            });

            return server;
        }

        private ChildPlanEvent? FindDueChildEvent(ChildPlan plan, int generation)
        {
            double elapsed = plan.ElapsedSeconds(Stopwatch.GetTimestamp());

            foreach (ChildPlanEvent planned in plan.Events)
            {
                if (planned.OffsetSeconds > elapsed) return null;

                if (planned.Kind == "ordinary-child-exit" || planned.Kind == "unhandled-child-crash")
                {
                    string completed = Path.Combine(
                        _options.RunRoot, "state", "completed", ChildState.Safe(planned.Id) + ".json");
                    if (!File.Exists(completed) &&
                        !ChildState.IsEventClaimed(_options.RunRoot, planned.Id)) return planned;
                }
                else if (planned.Kind == "fast-crash-loop")
                {
                    int armed = ChildState.CountArmed(_options.RunRoot, planned.Id);
                    if (armed < planned.Repetitions) return planned;
                    ChildState.MarkCompleted(_options.RunRoot, planned.Id, generation);
                }
            }

            return null;
        }

        private int ExecutePlannedEvent(ChildPlan plan, ChildPlanEvent planned, int generation, int pid)
        {
            if (planned.Kind == "ordinary-child-exit")
            {
                if (!ChildState.TryClaimEvent(_options.RunRoot, planned.Id, generation))
                    throw new InvalidOperationException("The due ordinary-exit event was already claimed.");

                ChildState.WriteArmed(
                    _options.RunRoot, plan.CampaignId, planned.Id, planned.Kind,
                    generation, pid, Stopwatch.GetTimestamp());
                ChildState.MarkCompleted(_options.RunRoot, planned.Id, generation);
                return 0;
            }

            if (planned.Kind == "unhandled-child-crash")
            {
                if (!ChildState.TryClaimEvent(_options.RunRoot, planned.Id, generation))
                    throw new InvalidOperationException("The due crash event was already claimed.");
                ArmCrash(plan, planned, generation, pid);
                throw new InvalidOperationException(
                    "E3-WDOG planned unhandled crash " + planned.Id + " generation " + generation);
            }

            if (planned.Kind == "fast-crash-loop")
            {
                string armedPath = ChildState.ArmedPath(_options.RunRoot, planned.Id, generation);
                if (File.Exists(armedPath)) return 0;

                ArmCrash(plan, planned, generation, pid);
                throw new InvalidOperationException(
                    "E3-WDOG planned fast crash " + planned.Id + " generation " + generation);
            }

            return 0;
        }

        private void ArmCrash(ChildPlan plan, ChildPlanEvent planned, int generation, int pid)
        {
            long timestamp = Stopwatch.GetTimestamp();
            ChildState.WriteArmed(
                _options.RunRoot, plan.CampaignId, planned.Id, planned.Kind,
                generation, pid, timestamp);

            string pending = Path.Combine(
                _options.RunRoot,
                "crash",
                "pending",
                "crash-" + ChildState.Safe(planned.Id) + "-g" +
                generation.ToString("D6", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(pending);

            ChildState.WriteDurably(
                Path.Combine(pending, "application.json"),
                ChildState.Object(
                    "campaignId", plan.CampaignId,
                    "eventId", planned.Id,
                    "generation", generation.ToString(CultureInfo.InvariantCulture),
                    "pid", pid.ToString(CultureInfo.InvariantCulture),
                    "version", typeof(ChildRuntime).Assembly.GetName().Version?.ToString() ?? "unknown",
                    "status", "armed"));
            ChildState.WriteDurably(
                Path.Combine(pending, "event.json"),
                ChildState.Object(
                    "campaignId", plan.CampaignId,
                    "eventId", planned.Id,
                    "kind", planned.Kind,
                    "generation", generation.ToString(CultureInfo.InvariantCulture),
                    "armedTimestamp", timestamp.ToString(CultureInfo.InvariantCulture)));

            WatchdogController.NotifyException(
                "E3WatchdogPlannedCrash",
                planned.Id + " generation " + generation.ToString(CultureInfo.InvariantCulture),
                "E3-WDOG.Child");
        }

        private static string ControlPipe(string campaignId, int generation) =>
            "nekolib.e3wdog.child." + ChildState.Safe(campaignId) + ".g" +
            generation.ToString("D6", CultureInfo.InvariantCulture);

        private static PipeMessage Reply(string value)
        {
#if NET9_0_OR_GREATER
            return new PipeMessage
            {
                Ok = true,
                Data = System.Text.Json.JsonSerializer.SerializeToElement(value)
            };
#else
            return new PipeMessage
            {
                Ok = true,
                Data = new Newtonsoft.Json.Linq.JValue(value)
            };
#endif
        }
    }
}
