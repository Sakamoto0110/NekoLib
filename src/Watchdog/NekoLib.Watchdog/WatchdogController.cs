using NekoLib.Pipes;
using NekoLib.Diagnostics.Contracts;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;


#if NET9
using System.Text.Json;
#else
using Newtonsoft.Json.Linq;
#endif

namespace NekoLib.Watchdog
{
    public static class WatchdogController
    {
        // ============================================================
        // Structured Log DTO
        // ============================================================

        public sealed class LogEvent
        {
            public long TsUnixMs { get; set; }
            public string Level { get; set; }
            public string Msg { get; set; }
            public object Meta { get; set; }
            public string Line { get; set; }
        }

        // ============================================================
        // Internal Pipe Resolution (cached)
        // ============================================================

        private static readonly string _pipeName = ResolvePipeName();

        private static string ResolvePipeName()
        {
            var exe = Process.GetCurrentProcess().MainModule.FileName;
            return ResolvePipeNameForTarget(exe);
        }

        public static string ResolvePipeNameForTarget(string targetPath)
        {
            var full = Path.GetFullPath(targetPath).ToLowerInvariant();
            using (var sha1 = SHA1.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(full);
                var hash = sha1.ComputeHash(bytes);
                var id = BitConverter.ToString(hash).Replace("-", "").Substring(0, 16);
                return $"NekoLib.Watchdog.{id}";
            }
        }

        private static PipeClient CreateClient()
        {
            return CreateClient(_pipeName);
        }

        private static PipeClient CreateClient(string pipeName)
        {
            return new PipeClient(new PipeClientOptions
            {
                PipeName = pipeName,
                ConnectTimeout = TimeSpan.FromMilliseconds(1500),
                RequestTimeout = TimeSpan.FromMilliseconds(3000)
            });
        }
        public static void NotifyException(string type, string message, string source)
        {
            NotifyExceptionToPipe(_pipeName, type, message, source);
        }

        public static void NotifyExceptionForTarget(string targetPath, string type, string message, string source)
        {
            NotifyExceptionToPipe(ResolvePipeNameForTarget(targetPath), type, message, source);
        }

        private static void NotifyExceptionToPipe(string pipeName, string type, string message, string source)
        {
            try
            {
                using (var client = CreateClient(pipeName))
                {
                    var payload = new
                    {
                        type,
                        message,
                        source
                    };

                    client.SendAsync("exception_notify", payload)
                          .GetAwaiter()
                          .GetResult();
                }
            }
            catch
            {
                // swallow: notify must not throw
            }
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
                    if (response.Data.HasValue &&
                        response.Data.Value.ValueKind == JsonValueKind.String)
                        return response.Data.Value.GetString();

                    return response.Data.HasValue
                        ? response.Data.Value.ToString()
                        : "";
#else
                    return response.Data != null
                        ? response.Data.ToString()
                        : "";
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

        // ============================================================
        // Public API (No targetPath needed anymore)
        // ============================================================

        public static bool Ping() => Send("ping") == "pong";

        public static string Status() => Send("status");

        public static void Pause() => Send("pause");

        public static void Resume() => Send("resume");

        public static void Stop() => Send("stop");

        public static void Restart() => Send("restart");

        public static void NotifyLog(LogEntry entry)
        {
            if (entry == null)
                return;

            try
            {
                using (var client = CreateClient())
                {
                    var payload = new
                    {
                        level = entry.Level.ToString(),
                        category = entry.Category,
                        message = entry.Message,
                        exception = entry.Exception?.ToString()
                    };

                    client.SendAsync("log_write", payload)
                          .GetAwaiter()
                          .GetResult();
                }
            }
            catch
            {
                // external logging must not throw back into the app
            }
        }

        // ============================================================
        // Log Subscription (Replay + Live)
        // ============================================================

        public static IDisposable SubscribeLogs(Action<LogEvent> onLog)
        {
            if (onLog == null)
                throw new ArgumentNullException(nameof(onLog));

            TryReplayHistory(onLog);

            var client = new PipeEventClient(_pipeName);

            client.OnEvent += msg =>
            {
#if NET9
                if (!msg.Data.HasValue ||
                    msg.Data.Value.ValueKind != JsonValueKind.Object)
                    return;

                var root = msg.Data.Value;

                var e = new LogEvent();

                if (root.TryGetProperty("tsUnixMs", out var ts) &&
                    ts.ValueKind == JsonValueKind.Number)
                    e.TsUnixMs = ts.GetInt64();

                if (root.TryGetProperty("level", out var lvl) &&
                    lvl.ValueKind == JsonValueKind.String)
                    e.Level = lvl.GetString();

                if (root.TryGetProperty("msg", out var m) &&
                    m.ValueKind == JsonValueKind.String)
                    e.Msg = m.GetString();

                if (root.TryGetProperty("line", out var l) &&
                    l.ValueKind == JsonValueKind.String)
                    e.Line = l.GetString();

                if (root.TryGetProperty("meta", out var meta))
                    e.Meta = meta.ToString();

                onLog(e);
#else
                var t = msg.Data;
                if (t == null || t.Type != JTokenType.Object)
                    return;

                var e = new LogEvent
                {
                    TsUnixMs = t["tsUnixMs"] != null ? t["tsUnixMs"].Value<long>() : 0,
                    Level = t["level"]?.ToString(),
                    Msg = t["msg"]?.ToString(),
                    Line = t["line"]?.ToString(),
                    Meta = t["meta"]
                };

                onLog(e);
#endif
            };

            client.Start();
            return client;
        }

        public static IDisposable SubscribeLogLines(Action<string> onLine)
        {
            if (onLine == null)
                throw new ArgumentNullException(nameof(onLine));

            return SubscribeLogs(e =>
            {
                if (!string.IsNullOrWhiteSpace(e.Line))
                    onLine(e.Line);
                else if (!string.IsNullOrWhiteSpace(e.Msg))
                    onLine(e.Msg);
            });
        }

        private static void TryReplayHistory(Action<LogEvent> onLog)
        {
            try
            {
                using (var client = CreateClient())
                {
                    var response = client
                        .SendAsync("log_history")
                        .GetAwaiter()
                        .GetResult();

                    if (!response.Ok)
                        return;

#if NET9
                    if (!response.Data.HasValue ||
                        response.Data.Value.ValueKind != JsonValueKind.Array)
                        return;

                    foreach (var item in response.Data.Value.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Object)
                            continue;

                        var e = new LogEvent();

                        if (item.TryGetProperty("tsUnixMs", out var ts))
                            e.TsUnixMs = ts.GetInt64();

                        if (item.TryGetProperty("level", out var lvl))
                            e.Level = lvl.GetString();

                        if (item.TryGetProperty("msg", out var m))
                            e.Msg = m.GetString();

                        if (item.TryGetProperty("line", out var l))
                            e.Line = l.GetString();

                        if (item.TryGetProperty("meta", out var meta))
                            e.Meta = meta.ToString();

                        onLog(e);
                    }
#else
                    var arr = response.Data as JArray;
                    if (arr == null) return;

                    foreach (var item in arr)
                    {
                        var e = new LogEvent
                        {
                            TsUnixMs = item["tsUnixMs"]?.Value<long>() ?? 0,
                            Level = item["level"]?.ToString(),
                            Msg = item["msg"]?.ToString(),
                            Line = item["line"]?.ToString(),
                            Meta = item["meta"]
                        };

                        onLog(e);
                    }
#endif
                }
            }
            catch
            {
                // silent: best effort
            }
        }
    }
}
