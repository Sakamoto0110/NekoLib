#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Data.RuntimeTests.SqlServer.Container;
using NekoLib.Data.RuntimeTests.SqlServer.Faults;
using NekoLib.RuntimeTests.Harness.Faults;
using NekoLib.RuntimeTests.Harness.Reporting;
using NekoLib.Data.RuntimeTests.SqlServer.Schema;
using NekoLib.Data.RuntimeTests.SqlServer.Server;
using NekoLib.RuntimeTests.Harness;
using NekoLib.Data.RuntimeTests.SqlServer.Workload;

namespace NekoLib.Data.RuntimeTests.SqlServer
{
    /// <summary>
    /// E4-SQL: <c>NekoLib.Data</c> driven against a real SQL Server engine in a
    /// locally adopted container.
    /// <para/>
    /// What this scenario can establish is narrower than "NekoLib supports SQL
    /// Server", and the distinction is kept everywhere in the output. Three
    /// separate things are involved: Microsoft's ADO.NET abstractions, which is
    /// all <c>NekoLib.Data</c> ever sees; <c>Microsoft.Data.SqlClient</c>, the
    /// concrete provider, which lives only in this project; and the SQL Server
    /// engine, which is a container this machine happens to have. A passing run
    /// is evidence for the exact combination it recorded, and nothing more.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            ScenarioOptions options = new ScenarioOptions();
            if (!options.TryParse(args, out string diagnostic))
            {
                Console.Error.WriteLine("E4-SQL: " + diagnostic);
                Console.Error.WriteLine();
                Console.Error.WriteLine(ScenarioOptions.UsageText());
                return ExitCodes.Usage;
            }

            using (CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                bool interrupted = false;

                Console.CancelKeyPress += (_, e) =>
                {
                    // Taking the Ctrl+C rather than letting it kill the process
                    // is what makes bounded cleanup and a partial summary
                    // possible; a second press still ends things immediately.
                    if (interrupted) return;

                    interrupted = true;
                    e.Cancel = true;
                    Console.Error.WriteLine();
                    Console.Error.WriteLine("interrupt received; winding down and writing a partial summary");
                    cancellation.Cancel();
                };

                try
                {
                    return new ScenarioRun(options, cancellation.Token)
                        .ExecuteAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("E4-SQL failed outside any check: " + ex);
                    return ExitCodes.Unexpected;
                }
            }
        }
    }

    /// <summary>One execution, from preflight to the exit code.</summary>
    internal sealed class ScenarioRun
    {
        private readonly ScenarioOptions _options;
        private readonly CancellationToken _ct;
        private readonly List<string> _setupGaps = new List<string>();
        private readonly List<string> _cleanupProblems = new List<string>();

        private RunArtifacts? _artifacts;
        private CheckRunner? _runner;
        private DateTime _startedUtc = DateTime.UtcNow;
        private bool _interrupted;

        public ScenarioRun(ScenarioOptions options, CancellationToken ct)
        {
            _options = options;
            _ct = ct;
        }

        public async Task<int> ExecuteAsync()
        {
            PinnedContainerDefinition pinned;
            try
            {
                pinned = PinnedContainerDefinition.Load();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("E4-SQL: " + ex.Message);
                return ExitCodes.PrerequisiteMissing;
            }

            string containerName = _options.ContainerNameOverride ?? pinned.ContainerName;
            int port = _options.PortOverride ?? pinned.HostPort;

            if (_options.PrintScheduleOnly)
            {
                FaultSchedule preview = BuildSchedule(containerName);
                Console.WriteLine(preview.ToJson());
                Console.WriteLine();
                Console.WriteLine("normalized-hash " + preview.Hash);
                return ExitCodes.Success;
            }

            if (!SqlServerEndpoint.TryResolve(
                    pinned.PasswordVariable,
                    _options.Host,
                    port,
                    pinned.LoginUser,
                    out SqlServerEndpoint? endpoint,
                    out string endpointDiagnostic))
            {
                Console.Error.WriteLine("E4-SQL: " + endpointDiagnostic);
                return ExitCodes.PrerequisiteMissing;
            }

            using (RunArtifacts artifacts = RunArtifacts.Create(
                _options.ArtifactsRoot,
                _options.CampaignId,
                _options.ScenarioId,
                SqlServerSamples.ColumnNamesForHeader,
                _options.WorkerId))
            {
                _artifacts = artifacts;
                // Bounded only for the soak, where the result detail would
                // otherwise grow for hours and the heap it occupies is exactly
                // what this scenario's own drift check is watching. Counts stay
                // exact either way; the full detail goes to checks.ndjson.
                CheckRetention retention = CheckRetention.ForMode(_options.Mode);
                _runner = new CheckRunner(artifacts.Out, _ct, retention, artifacts.AppendCheck);

                artifacts.Out("E4-SQL  " + _options.CampaignId);
                artifacts.Out("target  " + RuntimeFacts.TargetFrameworkMoniker +
                              "  " + RuntimeFacts.RuntimeDescription);
                artifacts.Out("run     " + artifacts.CampaignDirectory);
                artifacts.Out(string.Empty);

                return await ExecuteWithArtifactsAsync(artifacts, pinned, endpoint!, containerName)
                    .ConfigureAwait(false);
            }
        }

        private async Task<int> ExecuteWithArtifactsAsync(
            RunArtifacts artifacts,
            PinnedContainerDefinition pinned,
            SqlServerEndpoint endpoint,
            string containerName)
        {
            AdoptedContainer? adopted = null;
            ContainerFacts? facts = null;
            string engineVersion = "unavailable";

            if (DockerCli.TryLocate(out DockerCli? docker, out string dockerDiagnostic))
            {
                engineVersion = dockerDiagnostic;
                adopted = new AdoptedContainer(docker!, containerName);

                if (!adopted.Exists())
                {
                    artifacts.Error("E4-SQL: no container named '" + containerName + "' to adopt. " +
                                    "This scenario never creates one - see the README.");
                    return ExitCodes.PrerequisiteMissing;
                }

                facts = adopted.Describe(engineVersion);
                artifacts.Out("container  " + facts.Name + "  found " + facts.Status +
                              "  image " + facts.ConfiguredImage);

                foreach (string gap in AdoptedContainer.Reconcile(facts, pinned))
                {
                    _setupGaps.Add(gap);
                    artifacts.Out("setup gap  " + gap);
                }

                if (!adopted.IsRunning())
                {
                    artifacts.Out("container  starting it for this run; it will be stopped again at cleanup");
                    if (!adopted.Start(out string startDiagnostic))
                    {
                        artifacts.Error("E4-SQL: could not start the adopted container: " + startDiagnostic);
                        return ExitCodes.PrerequisiteMissing;
                    }
                }
            }
            else
            {
                _setupGaps.Add("no container engine: " + dockerDiagnostic);
                artifacts.Out("container  unavailable - " + dockerDiagnostic);
            }

            bool containerFaultsAllowed = adopted != null && !_options.NoContainerFaults;

            if (_options.Mode == ScenarioMode.RecoveryRehearsal && !containerFaultsAllowed && adopted == null)
            {
                artifacts.Error("E4-SQL: a recovery rehearsal needs container control. " +
                                "Install or point to the container CLI, or pass --no-container-faults " +
                                "to run only the checks that do not need it.");
                return ExitCodes.PrerequisiteMissing;
            }

            ServerProbe probe = new ServerProbe(endpoint);

            ReadinessResult readiness = await probe
                .WaitUntilReadyAsync(TimeSpan.FromMinutes(3), _ct).ConfigureAwait(false);

            if (!readiness.Ready)
            {
                artifacts.Error("E4-SQL: the server never became ready after " + readiness.Attempts +
                                " attempt(s): " + readiness.Detail);
                await RestoreContainerAsync(adopted, artifacts).ConfigureAwait(false);
                return ExitCodes.PrerequisiteMissing;
            }

            ServerFacts server = await probe.ReadServerFactsAsync(_ct).ConfigureAwait(false);
            artifacts.Out("server     " + server.ProductVersion + " " + server.Edition +
                          " (" + server.ProductLevel + ")");
            artifacts.Out(string.Empty);

            WriteEnvironment(artifacts, pinned, endpoint, facts, server, engineVersion);

            FaultSchedule schedule = BuildSchedule(containerName);
            artifacts.WriteText(artifacts.SchedulePath, schedule.ToJson());
            artifacts.Out("schedule   " + schedule.Events.Count + " planned fault(s), " + schedule.Hash);
            artifacts.Out(string.Empty);

            string database = _options.BuildScenarioDatabaseName(pinned.ScenarioDatabasePrefix);
            WorkloadCounters counters = new WorkloadCounters();
            int exitCode = ExitCodes.Success;
            GatewayWorkspace? workspace = null;
            bool databaseCreated = false;

            try
            {
                await probe.CreateScenarioDatabaseAsync(database, _ct).ConfigureAwait(false);
                databaseCreated = true;
                artifacts.Out("database   " + database + " created");

                workspace = new GatewayWorkspace(
                    endpoint.BuildConnectionString(database),
                    GatewayWorkspace.DefaultOptions());

                ResourceSampler sampler = new ResourceSampler(
                    artifacts, counters, new SqlServerSamples(workspace));

                PhaseContext context = new PhaseContext
                {
                    Workspace = workspace,
                    Runner = _runner!,
                    Endpoint = endpoint,
                    Probe = probe,
                    Counters = counters,
                    Sampler = sampler,
                    Artifacts = artifacts,
                    Adopted = adopted,
                    DatabaseName = database,
                    Seed = _options.Seed,
                    ContainerFaultsAllowed = containerFaultsAllowed,
                    Ct = _ct
                };

                sampler.Take("preflight", "baseline");

                await ScenarioSchema.CreateAsync(workspace.Gateway, _ct).ConfigureAwait(false);
                await ScenarioSchema.SeedAsync(workspace.Gateway, _options.Seed, _ct).ConfigureAwait(false);

                string digest = await ScenarioSchema.DigestAsync(workspace.Gateway, _ct).ConfigureAwait(false);
                artifacts.Out("seed       " + digest);
                artifacts.Out(string.Empty);

                sampler.Take("preflight", "post-warm-up");

                _startedUtc = DateTime.UtcNow;
                await RunModeAsync(context, schedule).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _interrupted = true;
                artifacts.Error("interrupted before the selected mode finished");
            }
            catch (Exception ex)
            {
                artifacts.Error("E4-SQL: the run failed outside any check: " + ex);
                exitCode = ExitCodes.Unexpected;
            }
            finally
            {
                await CleanupAsync(
                    artifacts, probe, workspace, adopted, database, databaseCreated, pinned, counters)
                    .ConfigureAwait(false);
            }

            return Finish(artifacts, schedule, server, facts, database, counters, exitCode);
        }

        private async Task RunModeAsync(PhaseContext context, FaultSchedule schedule)
        {
            switch (_options.Mode)
            {
                case ScenarioMode.Smoke:
                    await RunWorkloadClassesAsync(context).ConfigureAwait(false);
                    break;

                case ScenarioMode.RecoveryRehearsal:
                    // Warm-up first: a fault dispatched before ordinary work has
                    // been shown to succeed would measure start-up.
                    await ConnectionMatrix.RunAsync(context).ConfigureAwait(false);
                    await TransactionMatrix.RunAsync(context).ConfigureAwait(false);
                    context.Sampler.Take("recovery", "post-warm-up");

                    await RecoveryMatrix.RunAsync(context, schedule, _startedUtc).ConfigureAwait(false);

                    context.Sampler.Take("recovery", "cool-down");
                    await ReadMatrix.RunAsync(context).ConfigureAwait(false);
                    await CancellationMatrix.RunAsync(context).ConfigureAwait(false);
                    break;

                case ScenarioMode.Soak:
                    await RunSoakAsync(context, schedule).ConfigureAwait(false);
                    break;
            }
        }

        private static async Task RunWorkloadClassesAsync(PhaseContext context)
        {
            await ConnectionMatrix.RunAsync(context).ConfigureAwait(false);
            await ReadMatrix.RunAsync(context).ConfigureAwait(false);
            await TransactionMatrix.RunAsync(context).ConfigureAwait(false);
            await CancellationMatrix.RunAsync(context).ConfigureAwait(false);

            // Last, because crossing the IL schema cap is permanent for the
            // process and every earlier phase should meet an untouched one.
            await DynamicLifetime.RunAsync(context).ConfigureAwait(false);
        }

        private async Task RunSoakAsync(PhaseContext context, FaultSchedule schedule)
        {
            DateTime deadline = _startedUtc + _options.SoakDuration;
            int cycle = 0;

            // The dynamic boundary is crossed once: emitted types are
            // process-wide, so repeating the phase would measure nothing new.
            await DynamicLifetime.RunAsync(context).ConfigureAwait(false);

            Task faults = RecoveryMatrix.RunAsync(context, schedule, _startedUtc);

            while (DateTime.UtcNow < deadline && !_ct.IsCancellationRequested)
            {
                cycle++;
                context.Artifacts.Out("soak cycle " + cycle.ToString(CultureInfo.InvariantCulture) +
                                      "  " + Remaining(deadline) + " remaining");

                // Exclusive against the fault task: a matrix asserts, and an
                // assertion made while a scheduled fault has the server
                // stopped is measuring the fault, not the library.
                await context.ExclusiveAsync(() => ReadMatrix.RunAsync(context)).ConfigureAwait(false);
                await context.ExclusiveAsync(() => TransactionMatrix.RunAsync(context)).ConfigureAwait(false);
                context.Sampler.Take("soak", "periodic");
            }

            await faults.ConfigureAwait(false);
            context.Sampler.Take("soak", "final");
        }

        private static string Remaining(DateTime deadline)
        {
            TimeSpan left = deadline - DateTime.UtcNow;
            if (left < TimeSpan.Zero) left = TimeSpan.Zero;
            return ((int)left.TotalMinutes).ToString(CultureInfo.InvariantCulture) + "m";
        }

        private FaultSchedule BuildSchedule(string containerName)
        {
            if (_options.FaultSchedulePath != null)
                return FaultSchedule.Load(_options.FaultSchedulePath, _options.ScenarioId);

            string mode;
            TimeSpan window;
            IReadOnlyList<string> kinds;

            switch (_options.Mode)
            {
                case ScenarioMode.RecoveryRehearsal:
                    mode = "recovery-rehearsal";
                    window = _options.RehearsalDuration;
                    kinds = FaultKinds.RecoveryRehearsalSet;
                    break;

                case ScenarioMode.Soak:
                    mode = "soak";
                    window = _options.SoakDuration;
                    kinds = FaultKinds.RecoveryRehearsalSet;
                    break;

                default:
                    // Smoke exercises every workload class without destructive
                    // fault density, so its schedule is deliberately empty and
                    // is still written, so a campaign directory always says what
                    // was planned.
                    mode = "smoke";
                    window = TimeSpan.FromMinutes(20);
                    kinds = new string[0];
                    break;
            }

            return FaultSchedule.Generate(
                _options.CampaignId,
                _options.ScenarioId,
                ScenarioOptions.ScheduleGeneratorVersion,
                mode,
                _options.Seed,
                window,
                kinds,
                new SqlServerFaultVocabulary(containerName));
        }

        /// <summary>
        /// Bounded cleanup that runs whatever happened above, including after an
        /// interrupt. It uses its own token: cleanup that stopped because the
        /// run was cancelled would leave exactly the mess it exists to prevent.
        /// </summary>
        private async Task CleanupAsync(
            RunArtifacts artifacts,
            ServerProbe probe,
            GatewayWorkspace? workspace,
            AdoptedContainer? adopted,
            string database,
            bool databaseCreated,
            PinnedContainerDefinition pinned,
            WorkloadCounters counters)
        {
            artifacts.Out(string.Empty);
            artifacts.Out("cleanup");

            if (workspace != null)
            {
                try
                {
                    workspace.Factory.ClearProviderPool();
                    workspace.Dispose();
                    artifacts.Out("  gateway    context disposed and the provider pool emptied");
                }
                catch (Exception ex)
                {
                    _cleanupProblems.Add("disposing the gateway threw " + ex.GetType().Name + ": " + ex.Message);
                }
            }

            using (CancellationTokenSource cleanup = new CancellationTokenSource(TimeSpan.FromMinutes(3)))
            {
                bool serverUp = adopted == null || adopted.IsRunning();

                if (!serverUp && databaseCreated && !_options.KeepDatabase)
                {
                    // A fault may have left it stopped. Start it regardless of
                    // the state the run found it in: there is a database to
                    // drop, and RestoreInitialState below puts the container
                    // back afterwards either way. Only restarting when the run
                    // found it running is what left a database behind after the
                    // first soak.
                    artifacts.Out("  container  starting it so the scenario database can be dropped");
                    adopted!.Start(out string restartDiagnostic);
                    artifacts.Out("  container  " + restartDiagnostic);

                    ReadinessResult ready = await probe
                        .WaitUntilReadyAsync(TimeSpan.FromMinutes(2), cleanup.Token).ConfigureAwait(false);
                    serverUp = ready.Ready;
                }

                if (databaseCreated && serverUp)
                {
                    if (_options.KeepDatabase)
                    {
                        artifacts.Out("  database   kept at the caller's request: " + database);
                    }
                    else
                    {
                        try
                        {
                            int sessions = await probe
                                .CountScenarioSessionsAsync(database, cleanup.Token).ConfigureAwait(false);

                            if (sessions > 0)
                            {
                                artifacts.Out("  sessions   " + sessions +
                                              " still attributed to this scenario before the drop");
                            }

                            await probe.DropScenarioDatabaseAsync(database, cleanup.Token).ConfigureAwait(false);
                            artifacts.Out("  database   " + database + " dropped");
                        }
                        catch (Exception ex)
                        {
                            _cleanupProblems.Add("could not drop " + database + ": " + ex.Message);
                        }
                    }
                }
                else if (databaseCreated)
                {
                    _cleanupProblems.Add(
                        "the scenario database " + database + " could not be dropped because the server is down");
                }

                if (serverUp)
                {
                    try
                    {
                        IReadOnlyList<string> leftovers = await probe
                            .ListScenarioDatabasesAsync(pinned.ScenarioDatabasePrefix, cleanup.Token)
                            .ConfigureAwait(false);

                        if (leftovers.Count > 0)
                        {
                            // Reported, never dropped: a database from another
                            // run is not this run's to remove.
                            artifacts.Out("  leftovers  " + leftovers.Count +
                                          " scenario database(s) from earlier runs: " +
                                          string.Join(", ", ToArray(leftovers)));
                        }
                    }
                    catch (Exception ex)
                    {
                        _cleanupProblems.Add("could not list leftover scenario databases: " + ex.Message);
                    }
                }
            }

            await RestoreContainerAsync(adopted, artifacts).ConfigureAwait(false);

            artifacts.Out("  counters   operations=" + counters.Operations +
                          " successes=" + counters.Successes +
                          " expectedFailures=" + counters.ExpectedFailures +
                          " unexpectedFailures=" + counters.UnexpectedFailures +
                          " cancellations=" + counters.Cancellations);
        }

        private Task RestoreContainerAsync(AdoptedContainer? adopted, RunArtifacts artifacts)
        {
            if (adopted == null) return CompletedTask;

            if (!adopted.RestoreInitialState(out string diagnostic))
                _cleanupProblems.Add("container state was not restored: " + diagnostic);

            artifacts.Out("  container  " + diagnostic);
            artifacts.Event("container", json =>
            {
                json.Prop("action", "restore");
                json.Prop("initialStatus", adopted.InitialStatus);
                json.Prop("result", diagnostic);
            });

            return CompletedTask;
        }

        private void WriteEnvironment(
            RunArtifacts artifacts,
            PinnedContainerDefinition pinned,
            SqlServerEndpoint endpoint,
            ContainerFacts? facts,
            ServerFacts server,
            string engineVersion)
        {
            RuntimeFacts.ReadRepository(out string commit, out bool dirty, out string repositoryDiagnostic);

            JsonWriter json = new JsonWriter();
            json.Object(null, () =>
            {
                json.Prop("campaignId", _options.CampaignId);
                json.Prop("scenarioId", _options.ScenarioId);
                json.Prop("artifactLayoutVersion", artifacts.ArtifactLayoutVersion);
                if (artifacts.WorkerId != null) json.Prop("workerId", artifacts.WorkerId);
                json.Prop("startedUtc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));

                json.Object("repository", () =>
                {
                    json.Prop("commit", commit);
                    json.Prop("dirty", dirty);
                    if (repositoryDiagnostic.Length > 0) json.Prop("diagnostic", repositoryDiagnostic);
                });

                json.Object("host", () =>
                {
                    json.Prop("windowsVersion", RuntimeFacts.WindowsVersion);
                    json.Prop("machineArchitecture", RuntimeFacts.MachineArchitecture);
                    json.Prop("processArchitecture", RuntimeFacts.ProcessArchitecture);
                    json.Prop("logicalProcessors", RuntimeFacts.LogicalProcessorCount);
                    json.Prop("installedMemoryBytes", (long)RuntimeFacts.InstalledMemoryBytes);
                    json.Prop("installedMemory", RuntimeFacts.DescribeBytes(RuntimeFacts.InstalledMemoryBytes));
                });

                json.Object("process", () =>
                {
                    json.Prop("targetFramework", RuntimeFacts.TargetFrameworkMoniker);
                    json.Prop("runtime", RuntimeFacts.RuntimeDescription);
                    json.Prop("scenarioVersion", ScenarioFacts.ScenarioVersion);
                    json.Prop("libraryUnderTest", ScenarioFacts.LibraryVersion);
                    json.Prop("provider", ScenarioFacts.ProviderVersion);
                });

                json.Object("container", () =>
                {
                    json.Prop("engine", engineVersion);
                    json.Prop("name", facts == null ? pinned.ContainerName : facts.Name);
                    json.Prop("statusWhenFound", facts == null ? "unknown" : facts.Status);
                    json.Prop("configuredImage", facts == null ? string.Empty : facts.ConfiguredImage);
                    json.Prop("resolvedDigest", facts == null ? string.Empty : facts.ImageDigest);
                    json.Prop("imageArchitecture", facts == null ? string.Empty : facts.ImageArchitecture);
                    json.Prop("imageOs", facts == null ? string.Empty : facts.ImageOs);
                    json.Prop("publishedHostIp", facts == null ? string.Empty : facts.PublishedHostIp);
                    json.Prop("publishedHostPort", facts == null ? string.Empty : facts.PublishedHostPort);
                    json.Prop("loopbackOnly", facts != null && facts.IsLoopbackOnly);
                    json.Prop("mounts", facts == null ? string.Empty : facts.MountsJson);
                    json.Prop("hasNoMounts", facts != null && facts.HasNoMounts);
                    json.Prop("networks", facts == null ? string.Empty : facts.NetworksSummary);
                    json.Prop("restartPolicy", facts == null ? string.Empty : facts.RestartPolicy);
                });

                json.Object("server", () =>
                {
                    json.Prop("fullVersion", server.FullVersion);
                    json.Prop("productVersion", server.ProductVersion);
                    json.Prop("productLevel", server.ProductLevel);
                    json.Prop("edition", server.Edition);
                    json.Prop("collation", server.Collation);
                });

                json.Object("connection", () =>
                {
                    json.Prop("dataSource", endpoint.DataSource);
                    json.Prop("login", endpoint.User);
                    json.Prop("template", endpoint.Describe(endpoint.BuildConnectionString("<database>")));
                    json.Prop("passwordVariable", pinned.PasswordVariable);
                    json.Prop("passwordRecorded", false);
                });

                json.Array("setupGaps", () =>
                {
                    foreach (string gap in _setupGaps) json.Item(gap);
                });

                json.Array("claimBoundaries", () =>
                {
                    json.Item("NekoLib.Data sees only System.Data.Common types; the concrete provider is supplied " +
                              "by this scenario and is referenced by no product project.");
                    json.Item("Passing establishes evidence for the recorded image, provider package, target " +
                              "framework and architecture only. It is not a general SQL Server support claim.");
                    json.Item("The connection encrypts with TrustServerCertificate enabled against the container's " +
                              "self-signed certificate, so nothing here is evidence about certificate validation.");
                    json.Item("Translator construction, fake ADO.NET contract tests and commands actually executed " +
                              "by SQL Server are three different claims; only the third is what this run produces.");
                });
            });

            artifacts.WriteText(artifacts.EnvironmentPath, json.ToString());
        }

        /// <summary>
        /// Hands the run to the shared summary writer.
        /// <para/>
        /// The document shape, the exit-code precedence and the markdown table
        /// are the suite's contract rather than this scenario's, so they moved
        /// into the harness when E3-OBS turned out to need them unchanged. What
        /// stayed here is the half only this scenario can supply: the provider,
        /// the server build and the image digest.
        /// </summary>
        private int Finish(
            RunArtifacts artifacts,
            FaultSchedule schedule,
            ServerFacts server,
            ContainerFacts? facts,
            string database,
            WorkloadCounters counters,
            int exitCode)
        {
            RunSummary summary = new RunSummary
            {
                CampaignId = _options.CampaignId,
                ScenarioId = _options.ScenarioId,
                Mode = _options.Mode.ToString(),
                Seed = _options.Seed,
                ScheduleHash = schedule.Hash,
                ScheduleFaultCount = schedule.Events.Count,
                StartedUtc = _startedUtc,
                FinishedUtc = DateTime.UtcNow,
                Interrupted = _interrupted,
                BelowSpecifiedWindow =
                    _options.Mode == ScenarioMode.RecoveryRehearsal &&
                    _options.RehearsalDuration < ScenarioOptions.MinimumSpecifiedRehearsal,
                ExplicitExitCode = exitCode
            };

            summary.SetupGaps.AddRange(_setupGaps);
            summary.CleanupProblems.AddRange(_cleanupProblems);

            return summary.Write(
                artifacts,
                _runner!,
                counters,
                new SqlServerSummary(server, facts, database));
        }

        private static string[] ToArray(IReadOnlyList<string> items)
        {
            string[] copy = new string[items.Count];
            for (int i = 0; i < items.Count; i++) copy[i] = items[i];
            return copy;
        }

        private static Task CompletedTask
        {
            get
            {
#if NET6_0_OR_GREATER
                return Task.CompletedTask;
#else
                return Task.FromResult(0);
#endif
            }
        }
    }
}
