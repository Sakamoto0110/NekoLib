using NekoLib.Pipes;
using NekoLib.Core.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;


#if NET9
using System.Text.Json;
#else
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
#endif

namespace NekoLib.Watchdog
{
    /// <summary>
    /// Synchronous application-side facade for the Watchdog Host associated with
    /// the current executable. It is not a controller for arbitrary processes.
    /// </summary>
    public static class WatchdogController
    {
        // ============================================================
        // Structured Log DTO
        // ============================================================

        /// <summary>Mutable transport DTO delivered by replay and live log subscriptions.</summary>
        public sealed class LogEvent
        {
            /// <summary>Gets or sets the UTC Unix timestamp in milliseconds, or zero when absent.</summary>
            public long TsUnixMs { get; set; }
            /// <summary>Gets or sets the optional serialized severity name.</summary>
            public string? Level { get; set; }
            /// <summary>Gets or sets the optional structured message.</summary>
            public string? Msg { get; set; }
            /// <summary>Gets or sets compact raw JSON metadata, or <c>null</c> when metadata is absent or JSON null.</summary>
            public string? MetaJson { get; set; }
            /// <summary>Gets or sets the optional preformatted display line.</summary>
            public string? Line { get; set; }
        }

        // ============================================================
        // Internal Pipe Resolution (cached)
        // ============================================================

        private static readonly string _pipeName = ResolvePipeName();

        private static string ResolvePipeName()
        {
            using var process = Process.GetCurrentProcess();
            var exe = process.MainModule?.FileName ??
                throw new InvalidOperationException("Unable to resolve the current executable path.");
            return ResolvePipeNameForTarget(exe);
        }

        /// <summary>
        /// Derives the deterministic control-pipe identity from the lowercase
        /// absolute target path. The result is stable target identity, not a secret.
        /// </summary>
        /// <param name="targetPath">Target executable path.</param>
        /// <returns>The <c>NekoLib.Watchdog.</c>-prefixed SHA-1-derived pipe name.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="targetPath"/> is <c>null</c>.</exception>
        public static string ResolvePipeNameForTarget(string targetPath)
        {
            if (targetPath == null)
                throw new ArgumentNullException(nameof(targetPath));

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
        /// <summary>Best-effort forwards exception metadata to the Host for the current executable.</summary>
        /// <param name="type">Optional exception type name.</param>
        /// <param name="message">Optional exception message.</param>
        /// <param name="source">Optional application source.</param>
        public static void NotifyException(string? type, string? message, string? source)
        {
            NotifyExceptionToPipe(_pipeName, type, message, source);
        }

        /// <summary>
        /// Best-effort forwards exception metadata to the Host identity derived
        /// from a target path. Pipe and protocol failures are swallowed.
        /// </summary>
        /// <param name="targetPath">Target executable path used only for pipe identity.</param>
        /// <param name="type">Optional exception type name.</param>
        /// <param name="message">Optional exception message.</param>
        /// <param name="source">Optional application source.</param>
        /// <exception cref="ArgumentNullException"><paramref name="targetPath"/> is <c>null</c>.</exception>
        public static void NotifyExceptionForTarget(
            string targetPath,
            string? type,
            string? message,
            string? source)
        {
            NotifyExceptionToPipe(ResolvePipeNameForTarget(targetPath), type, message, source);
        }

        private static void NotifyExceptionToPipe(
            string pipeName,
            string? type,
            string? message,
            string? source)
        {
            try
            {
                var client = CreateClient(pipeName);
                var payload = new
                {
                    type,
                    message,
                    source
                };

                client.SendAsync(WatchdogCommands.ExceptionNotify, payload)
                      .GetAwaiter()
                      .GetResult();
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
                var client = CreateClient();
                var response = client
                    .SendAsync(cmd)
                    .GetAwaiter()
                    .GetResult();

                if (!response.Ok)
                    return "error=" + (response.Error != null ? response.Error.Code : "unknown");

#if NET9
                if (response.Data.HasValue &&
                    response.Data.Value.ValueKind == JsonValueKind.String)
                    return response.Data.Value.GetString() ?? "";

                return response.Data.HasValue
                    ? response.Data.Value.ToString()
                    : "";
#else
                return response.Data != null
                    ? response.Data.ToString()
                    : "";
#endif
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

        /// <summary>Checks whether the current Host returns the expected health acknowledgement.</summary>
        /// <returns><c>true</c> only for <c>pong</c>; timeout, transport, protocol, and other responses return <c>false</c>.</returns>
        public static bool Ping() => Send(WatchdogCommands.Ping) == "pong";

        /// <summary>Requests the current Host status synchronously.</summary>
        /// <returns>Serialized status evidence, or an <c>error=...</c> value for unavailable, transport, or protocol failure.</returns>
        public static string Status() => Send(WatchdogCommands.Status);

        /// <summary>Requests that the current Host pause restart supervision.</summary>
        /// <returns><c>true</c> only when the Host returns <c>paused</c>.</returns>
        public static bool Pause() => Send(WatchdogCommands.Pause) == "paused";

        /// <summary>Requests that the current Host resume restart supervision.</summary>
        /// <returns><c>true</c> only when the Host returns <c>running</c>.</returns>
        public static bool Resume() => Send(WatchdogCommands.Resume) == "running";

        /// <summary>Requests that the current Host stop supervision and the target.</summary>
        /// <returns><c>true</c> only when the Host returns <c>stopped</c>.</returns>
        public static bool Stop() => Send(WatchdogCommands.Stop) == "stopped";

        /// <summary>Requests that the current Host replace the supervised target.</summary>
        /// <returns><c>true</c> only when the Host returns <c>restarting</c>.</returns>
        public static bool Restart() => Send(WatchdogCommands.Restart) == "restarting";

        /// <summary>
        /// Best-effort forwards one Core log entry to the current Host. Null
        /// entries and all pipe or protocol failures are ignored.
        /// </summary>
        /// <param name="entry">Caller-owned log entry, or <c>null</c>.</param>
        public static void NotifyLog(LogEntry? entry)
        {
            if (entry == null)
                return;

            try
            {
                var client = CreateClient();
                var payload = new
                {
                    level = entry.Level.ToString(),
                    category = entry.Category,
                    message = entry.Message,
                    exception = entry.Exception?.ToString()
                };

                client.SendAsync(WatchdogCommands.LogWrite, payload)
                      .GetAwaiter()
                      .GetResult();
            }
            catch
            {
                // external logging must not throw back into the app
            }
        }

        /// <summary>
        /// Forwards a batch of log entries to the watchdog over a single pipe
        /// connection (one connect amortized over the whole batch). Used by the
        /// buffered <see cref="WatchdogPipeLogSink"/>.
        /// </summary>
        internal static void NotifyLogBatch(IReadOnlyList<LogEntry>? entries)
        {
            if (entries == null || entries.Count == 0)
                return;

            try
            {
                var items = new List<object>(entries.Count);
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    if (e == null)
                        continue;

                    items.Add(new
                    {
                        level = e.Level.ToString(),
                        category = e.Category,
                        message = e.Message,
                        exception = e.Exception?.ToString()
                    });
                }

                if (items.Count == 0)
                    return;

                var client = CreateClient();
                client.SendAsync(WatchdogCommands.LogWriteBatch, new { entries = items })
                      .GetAwaiter()
                      .GetResult();
            }
            catch
            {
                // external logging must not throw back into the app
            }
        }

        // ============================================================
        // Log Subscription (Replay + Live)
        // ============================================================

        /// <summary>
        /// Replays retained structured logs synchronously, then starts a live
        /// listener. Replay/live handoff is not gapless; ordering is guaranteed
        /// only within each phase, and callback exceptions are isolated.
        /// </summary>
        /// <param name="onLog">Application callback invoked on the subscribing thread during replay and on the event-client thread for live events.</param>
        /// <returns>A caller-owned handle that stops the live subscription when disposed.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="onLog"/> is <c>null</c>.</exception>
        public static IDisposable SubscribeLogs(Action<LogEvent> onLog)
        {
            if (onLog == null)
                throw new ArgumentNullException(nameof(onLog));

            TryReplayHistory(onLog);

            var client = new PipeEventClient(_pipeName);

            client.OnEvent += msg =>
            {
                if (!string.Equals(msg.Name, "log", StringComparison.Ordinal))
                    return;

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

                if (root.TryGetProperty("meta", out var meta) &&
                    meta.ValueKind != JsonValueKind.Null &&
                    meta.ValueKind != JsonValueKind.Undefined)
                    e.MetaJson = meta.GetRawText();

                InvokeLogSubscriber(onLog, e);
#else
                var t = msg.Data;
                if (t == null || t.Type != JTokenType.Object)
                    return;

                var e = new LogEvent
                {
                    TsUnixMs = t["tsUnixMs"]?.Value<long>() ?? 0,
                    Level = ToOptionalString(t["level"]),
                    Msg = ToOptionalString(t["msg"]),
                    Line = ToOptionalString(t["line"]),
                    MetaJson = ToJsonText(t["meta"])
                };

                InvokeLogSubscriber(onLog, e);
#endif
            };

            client.Start();
            return client;
        }

        /// <summary>
        /// Subscribes to formatted log lines, falling back to the structured
        /// message when no line is present. It retains the replay, ordering,
        /// threading, and gap behavior of <see cref="SubscribeLogs"/>.
        /// </summary>
        /// <param name="onLine">Application callback for non-blank line or message text.</param>
        /// <returns>A caller-owned handle that stops the live subscription when disposed.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="onLine"/> is <c>null</c>.</exception>
        public static IDisposable SubscribeLogLines(Action<string> onLine)
        {
            if (onLine == null)
                throw new ArgumentNullException(nameof(onLine));

            return SubscribeLogs(e =>
            {
                if (!string.IsNullOrWhiteSpace(e.Line))
                    onLine(e.Line!);
                else if (!string.IsNullOrWhiteSpace(e.Msg))
                    onLine(e.Msg!);
            });
        }

        private static void TryReplayHistory(Action<LogEvent> onLog)
        {
            try
            {
                var client = CreateClient();
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

                    if (item.TryGetProperty("meta", out var meta) &&
                        meta.ValueKind != JsonValueKind.Null &&
                        meta.ValueKind != JsonValueKind.Undefined)
                        e.MetaJson = meta.GetRawText();

                    InvokeLogSubscriber(onLog, e);
                }
#else
                var arr = response.Data as JArray;
                if (arr == null) return;

                foreach (var item in arr)
                {
                    var e = new LogEvent
                    {
                        TsUnixMs = item["tsUnixMs"]?.Value<long>() ?? 0,
                        Level = ToOptionalString(item["level"]),
                        Msg = ToOptionalString(item["msg"]),
                        Line = ToOptionalString(item["line"]),
                        MetaJson = ToJsonText(item["meta"])
                    };

                    InvokeLogSubscriber(onLog, e);
                }
#endif
            }
            catch
            {
                // silent: best effort
            }
        }

        private static void InvokeLogSubscriber(Action<LogEvent> subscriber, LogEvent value)
        {
            try
            {
                subscriber(value);
            }
            catch
            {
                // One application callback cannot terminate replay or the live
                // event-client listener.
            }
        }

#if !NET9
        private static string? ToOptionalString(JToken? token)
        {
            return token == null || token.Type == JTokenType.Null
                ? null
                : token.ToString();
        }

        private static string? ToJsonText(JToken? token)
        {
            return token == null || token.Type == JTokenType.Null
                ? null
                : token.ToString(Formatting.None);
        }
#endif
    }
}
