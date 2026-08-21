using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Pipes;
using NekoLib.Watchdog;

#if NETFRAMEWORK
using Newtonsoft.Json.Linq;
#else
using System.Text.Json;
#endif

namespace NekoLib.PackageConsumers.WatchdogHostProtocol
{
    internal static class Program
    {
        private const string ProtocolVersionCommand = "protocol_version";

        private static int Main(string[] args)
        {
            try
            {
                ThreadPool.GetMinThreads(out var workers, out var completions);
                ThreadPool.SetMinThreads(Math.Max(workers, 16), completions);

                if (args.Length == 1 && args[0] == "mismatch")
                    return VerifyMismatch();

                WatchdogBootstrap.EnsureStarted(Array.Empty<string>(), 10000);
                if (!WatchdogController.Ping())
                    throw new InvalidOperationException("The packaged Host did not answer ping.");

                var status = WatchdogController.Status();
                if (status.StartsWith("error=", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The packaged Host returned an error status: " + status);
                }

                if (!WatchdogController.Stop())
                    throw new InvalidOperationException("The packaged Host did not accept stop.");

                var elapsed = Stopwatch.StartNew();
                while (elapsed.Elapsed < TimeSpan.FromSeconds(10) &&
                       WatchdogController.Ping())
                {
                    Thread.Sleep(50);
                }

                if (WatchdogController.Ping())
                    throw new InvalidOperationException("The packaged Host did not stop.");

                Console.WriteLine("Packaged Watchdog Host protocol startup passed.");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        private static int VerifyMismatch()
        {
            string targetPath;
            using (var process = Process.GetCurrentProcess())
            {
                targetPath = process.MainModule?.FileName
                    ?? throw new InvalidOperationException(
                        "Unable to resolve the package consumer executable path.");
            }

            var pipeName = WatchdogController.ResolvePipeNameForTarget(targetPath);
            using (var server = new PipeServer(new PipeServerOptions
            {
                PipeName = pipeName,
                AccessPolicy = PipeAccessPolicy.CurrentUserOnly,
                MaxClients = 2,
                EnableEvents = false
            }))
            {
                server.Map(
                    ProtocolVersionCommand,
                    (request, cancellationToken) => Task.FromResult(
                        StringResponse("0")));
                server.Start();
                WaitForMismatchServer(pipeName);

                try
                {
                    WatchdogBootstrap.EnsureStarted(Array.Empty<string>(), 5000);
                }
                catch (InvalidOperationException exception)
                    when (exception.Message.IndexOf(
                        "incompatible protocol",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Console.WriteLine("Packaged Watchdog protocol mismatch passed.");
                    return 0;
                }
            }

            throw new InvalidOperationException(
                "The packaged bootstrap accepted an incompatible Host protocol.");
        }

        private static void WaitForMismatchServer(string pipeName)
        {
            var elapsed = Stopwatch.StartNew();
            Exception? lastError = null;
            while (elapsed.Elapsed < TimeSpan.FromSeconds(10))
            {
                try
                {
                    var client = new PipeClient(new PipeClientOptions
                    {
                        PipeName = pipeName,
                        ConnectTimeout = TimeSpan.FromSeconds(1),
                        RequestTimeout = TimeSpan.FromSeconds(1)
                    });
                    var response = client.SendAsync(ProtocolVersionCommand)
                        .GetAwaiter()
                        .GetResult();
                    if (response.Ok)
                        return;
                }
                catch (Exception exception)
                {
                    lastError = exception;
                }

                Thread.Sleep(50);
            }

            throw new InvalidOperationException(
                "The incompatible protocol fixture did not become ready.",
                lastError);
        }

        private static PipeMessage StringResponse(string value)
        {
#if NETFRAMEWORK
            return new PipeMessage
            {
                Ok = true,
                Data = JToken.FromObject(value)
            };
#else
            return new PipeMessage
            {
                Ok = true,
                Data = JsonSerializer.SerializeToElement(value)
            };
#endif
        }
    }
}
