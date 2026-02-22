using NekoLib.Pipes;
using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
#if NET9
using System.Text.Json;
#else
using Newtonsoft.Json.Linq;
#endif

namespace NekoLib.Runtime.Watchdog
{
    public static class WatchdogController
    {
        private const string PipeName = "NekoLib.Watchdog";

        private static PipeClient CreateClient()
        {
            return new PipeClient(new PipeClientOptions
            {
                PipeName = PipeName,
                ConnectTimeout = TimeSpan.FromMilliseconds(1500),
                RequestTimeout = TimeSpan.FromMilliseconds(3000)
            });
        }

        private static string Send(string cmd)
        {
            try
            {
                using (var client = CreateClient())
                {
                    var response = client
                        .SendAsync(cmd)
                        .GetAwaiter()
                        .GetResult();

                    if (!response.Ok)
                        return "error=" + (response.Error != null ? response.Error.Code : "unknown");

#if NET9
                    if (response.Data.HasValue && response.Data.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                        return response.Data.Value.GetString();
                    return response.Data.HasValue ? response.Data.Value.ToString() : "";
#else
                    return response.Data != null ? response.Data.ToString() : "";
#endif
                }
            }
            catch (TimeoutException)
            {
                return "error=watchdog_not_running";
            }
            catch
            {
                return "error=pipe_io";
            }
        }

        public static void Start() => Send("start");
        public static void Pause() => Send("pause");
        public static void Stop() => Send("stop");

        public static bool Ping() => Send("ping") == "pong";

        public static string Status(){ 
            var r = Send("status"); 
            return r; }  

        // -----------------------------------------------------------
        // Event Subscription (new system)
        // -----------------------------------------------------------

        public static IDisposable SubscribeLogs(Action<string> onLogLine)
        {
            if (onLogLine == null)
                throw new ArgumentNullException(nameof(onLogLine));

            var client = new PipeEventClient(PipeName);

            client.OnEvent += msg =>
            {
#if NET9
                if (msg.Data.HasValue &&
    msg.Data.Value.ValueKind == JsonValueKind.Object &&
    msg.Data.Value.TryGetProperty("line", out var line) &&
    line.ValueKind == JsonValueKind.String)
{
    onLogLine(line.GetString());
}
#else
                var token =   msg.Data;
                if (token != null && token["line"] != null)
                    onLogLine(token["line"].ToString());
#endif
            };

            client.Start();
            return client;
        }
    }
}
