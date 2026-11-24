using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Core.Logging;
using NekoLib.Core.Telemetry;
using NekoLib.Data;
using NekoLib.Data.Gateway;
using NekoLib.Data.Query;
using NekoLib.Data.Tests.Unit.Gateway;
using NekoLib.Diagnostics;
using NekoLib.Http;
using NekoLib.Inspection;
using NekoLib.Logging;
using NekoLib.Mvvm;
using NekoLib.Pipes;
using NekoLib.Telemetry;

namespace NekoLib.PackageConsumers
{
    internal static class ModuleProbe
    {
        internal static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }

        internal static async Task<T> Within<T>(Task<T> task)
        {
            if (await Task.WhenAny(task, Task.Delay(5000)) != task)
                throw new TimeoutException("Native qualification operation did not complete within five seconds.");
            return await task;
        }

        internal static async Task RunAsync()
        {
            Console.WriteLine("Runtime: {0}; process bitness: {1}", Environment.Version, IntPtr.Size * 8);
#if NET9_0_OR_GREATER
            Require(Environment.Version.Major == 9, "Consumer executes on .NET 9");
#else
            Require(Environment.Version.Major == 4, "Consumer executes on .NET Framework CLR 4");
#endif
            Require(Environment.Is64BitProcess, "Desktop qualification consumer executes as x64");
            using (var logger = new Logger())
            using (var inspection = new InspectionRuntime())
            {
                var telemetry = new TelemetryPipeline();
                logger.Log(LogLevel.Info, "package behavior probe");
                var operation = telemetry.StartOperation("PackageConsumer", "Integration");
                operation.Checkpoint("ready");
                operation.Complete(TelemetryOutcome.Succeeded);
                inspection.Record("PackageConsumer", "Integration");
                using (inspection.RegisterStateProvider("PackageConsumer", "pending", () => 1))
                {
                    var root = Path.Combine(Path.GetTempPath(), "NekoLib.PackageProbe", Guid.NewGuid().ToString("N"));
                    using (var crash = new CrashHandler(new CrashHandlerOptions
                    {
                        CrashRootDirectory = root, Logger = logger,
                        TelemetrySnapshotSource = telemetry, InspectionSnapshotSource = inspection,
                        DumpLevel = CrashDumpLevel.None
                    }))
                    {
                        string? crashText = null;
                        crash.CrashBundleWritten += (_, e) => crashText = e.CrashTextPath;
                        crash.Install();
                        CrashHandler.ReportExternalCrash("PackageConsumer", new IOException("recoverable probe"), false);
                        Require(crashText != null && File.ReadAllText(crashText).Contains("recoverable probe"), "Diagnostics writes a readable incident bundle");
                        Require(logger.GetRecentEntries(20).Count > 0 && logger.Flush(TimeSpan.FromSeconds(1)), "Logging retains and flushes entries");
                        Require(telemetry.GetRecentOperations(10).Count == 1, "Telemetry retains completed integration operation");
                        Require(inspection.CaptureSnapshot(10, TimeSpan.FromSeconds(1)).State.Count == 1, "Inspection captures registered application state");
                    }
                    // Only this unique, test-owned directory is removed.
                    Directory.Delete(root, true);
                }
            }

            var model = new ProbeModel();
            var notifications = 0;
            model.PropertyChanged += (_, __) => notifications++;
            model.Value = "one";
            model.Value = "one";
            Require(notifications == 1, "Mvvm emits one change for an effective update");

            var raw = new NekoLib.Devices.Core.Protocols.ProtocolRaw();
            var command = raw.BuildCommand(new NekoLib.Devices.Core.Abstractions.HardwareOperation
            {
                Args = new Dictionary<string, object> { { "RawText", "ping" } }
            });
            Require(raw.ParseResponse(command, new NekoLib.Devices.Core.Abstractions.HardwareOperation()).RawText == "ping", "Devices raw protocol round-trips bytes without hardware");

            var endpoint = HttpEndpoint.Get<Cat[]>("cats", RelativeUri.FromPathSegments("v1", "images", "search"));
            var catalog = HttpApiCatalog.Create(b => b.Register(endpoint));
            using (var handler = new OfflineHandler())
            using (var http = new HttpClient(handler) { BaseAddress = new Uri("https://qualification.invalid/") })
            {
                var api = new HttpApiClient(http, catalog);
                var offline = await api.SendAsync(endpoint).ConfigureAwait(false);
                Require(offline.StatusCode == HttpStatusCode.ServiceUnavailable, "Http exposes offline status without retrying");
                handler.Online = true;
                var online = await api.SendAsync(endpoint).ConfigureAwait(false);
                Require(online.HasValue && online.Value!.Length == 1 && online.Value[0].Id == "qualification", "Http materializes a typed response after reconnection");
                Require(handler.Requests == 2, "Http dispatches exactly once per caller request");
            }

            await VerifyData(new SqlServerQueryTranslator(), DbParameterBindingMode.Named).ConfigureAwait(false);
            await VerifyData(new AccessQueryTranslator(), DbParameterBindingMode.Positional).ConfigureAwait(false);

            var pipeName = "NekoLib.PackageProbe." + Guid.NewGuid().ToString("N");
            using (var server = new PipeServer(new PipeServerOptions { PipeName = pipeName, AccessPolicy = PipeAccessPolicy.CurrentUserOnly, EnableEvents = false }))
            {
                server.Map("echo", (request, token) => Task.FromResult(new PipeMessage { Ok = true, Data = request.Data }));
                server.Start();
                var client = new PipeClient(new PipeClientOptions { PipeName = pipeName, ConnectTimeout = TimeSpan.FromSeconds(3), RequestTimeout = TimeSpan.FromSeconds(3) });
                var response = await client.SendAsync("echo", new { value = 42 }).ConfigureAwait(false);
                Require(response.Ok && response.Name == "echo" && (response.Data?.ToString() ?? "").Contains("42"), "Pipes completes a current-user RPC round trip");
            }
        }

        private static async Task VerifyData(IDbQueryTranslator translator, DbParameterBindingMode bindingMode)
        {
            var commands = new List<FakeNonQueryCommand>();
            var fail = false;
            var factory = new FakeNonQueryConnectionFactory(() =>
            {
                var command = new FakeNonQueryCommand { Result = 1, ExecuteException = fail ? new IOException("provider offline") : null! };
                commands.Add(command);
                return command;
            });
            using (var context = new QueryExecutionContext(factory, translator, new DatabaseGatewayOptions { ParameterBindingMode = bindingMode }))
            {
                var gateway = new DatabaseGateway(context);
                using (var session = await gateway.OpenSessionAsync().ConfigureAwait(false))
                {
                    session.BeginTransaction();
                    var transaction = factory.LastConnection.LastTransaction;
                    var result = await gateway.Insert(new QueryBuilder().InsertInto("Pending").Value("Id", 7).Value("Value", "cat"), session).ConfigureAwait(false);
                    Require(result == 1 && ReferenceEquals(commands.Last().Transaction, transaction), bindingMode + " Data enlists structured write in the caller transaction");
                    session.Commit();
                    Require(transaction.CommitCalls == 1, bindingMode + " Data commits explicitly");
                }
                using (var session = await gateway.OpenSessionAsync().ConfigureAwait(false))
                {
                    session.BeginTransaction();
                    var transaction = factory.LastConnection.LastTransaction;
                    fail = true;
                    var failed = false;
                    try { await gateway.Insert(new QueryBuilder().InsertInto("Pending").Value("Id", 8), session).ConfigureAwait(false); }
                    catch (IOException) { failed = true; }
                    Require(failed && commands.Last().ExecuteNonQueryAsyncCalls == 1, bindingMode + " Data propagates provider failure without replay");
                    session.Rollback();
                    Require(transaction.RollbackCalls == 1, bindingMode + " Data rolls back explicitly after interruption");
                }
                fail = false;
                await gateway.Update("UPDATE Pending SET Value = @p2 WHERE Id = @p1 OR Id = @p1", new Dictionary<string, object?> { { "@p1", 7 }, { "@p2", "reconnected" } }).ConfigureAwait(false);
                var last = commands.Last();
                if (bindingMode == DbParameterBindingMode.Positional)
                    Require(last.CommandText.Contains("?") && last.Parameters.Count == 3 && Equals(last.Parameters[0].Value, "reconnected") && Equals(last.Parameters[1].Value, 7) && Equals(last.Parameters[2].Value, 7), "Access positional binding follows every SQL occurrence");
                else
                    Require(last.CommandText.Contains("@p1") && last.Parameters.Count == 2, "SQL named binding preserves logical parameters");
            }
        }

        public sealed class Cat { public string? Id { get; set; } }
        private sealed class ProbeModel : ViewModelBase
        {
            private string _value = "";
            public string Value { get => _value; set => SetProperty(ref _value, value); }
        }
        private sealed class OfflineHandler : HttpMessageHandler
        {
            internal bool Online;
            internal int Requests;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests++;
                return Task.FromResult(new HttpResponseMessage(Online ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable)
                { Content = new StringContent(Online ? "[{\"Id\":\"qualification\"}]" : "offline") });
            }
        }
    }

    internal sealed class OwnedObserver : NekoLib.Navigation.Contracts.Runtime.IInteractionObserverService, IDisposable
    {
        private readonly NekoLib.Navigation.Contracts.Runtime.IInteractionObserverService _inner;
        internal int DisposedOnThread;
        internal OwnedObserver(NekoLib.Navigation.Contracts.Runtime.IInteractionObserverService inner) { _inner = inner; }
        public event Action? InteractionDetected { add { _inner.InteractionDetected += value; } remove { _inner.InteractionDetected -= value; } }
        public void Dispose()
        {
            DisposedOnThread = Thread.CurrentThread.ManagedThreadId;
            (_inner as IDisposable)?.Dispose();
        }
    }
}
