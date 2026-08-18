#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using NekoLib.Pipes;
using NekoLib.RuntimeTests.Harness;

namespace NekoLib.Watchdog.RuntimeTests.CrashRecovery
{
    internal sealed class WatchdogStatus
    {
        public string State = string.Empty;
        public int ChildPid;
        public int RestartCount;
        public int? LastExitCode;
        public string RestartReason = string.Empty;
        public long EventsDropped;
    }

    internal static class WatchdogProtocol
    {
        public static string PipeName(string targetPath)
        {
            string full = Path.GetFullPath(targetPath).ToLowerInvariant();
            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(full));
                string id = BitConverter.ToString(hash).Replace("-", string.Empty).Substring(0, 16);
                return "NekoLib.Watchdog." + id;
            }
        }

        public static string SendText(string pipeName, string command)
        {
            PipeMessage response = Send(pipeName, command);
            return DataText(response);
        }

        public static WatchdogStatus ReadStatus(string pipeName)
        {
            Dictionary<string, object?> root = JsonParser.AsObject(
                JsonParser.Parse(DataJson(Send(pipeName, "status"))), "Watchdog status");

            WatchdogStatus status = new WatchdogStatus
            {
                State = JsonParser.RequireString(root, "state"),
                RestartCount = (int)JsonParser.RequireInt(root, "restartCount"),
                EventsDropped = JsonParser.RequireInt(root, "eventsDropped"),
                RestartReason = JsonParser.OptionalString(root, "restartReason") ?? string.Empty
            };

            if (root.TryGetValue("childPid", out object? child) && child is double childNumber)
                status.ChildPid = (int)childNumber;
            if (root.TryGetValue("lastExitCode", out object? exitCode) && exitCode is double exitNumber)
                status.LastExitCode = (int)exitNumber;

            return status;
        }

        public static string ChildHealth(string pipeName) => SendText(pipeName, "health");

        public static bool WaitUntil(Func<bool> predicate, TimeSpan timeout, CancellationToken ct)
        {
            DateTime deadline = DateTime.UtcNow.Add(timeout);
            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();
                try { if (predicate()) return true; } catch { }
                ct.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(100));
            }
            return false;
        }

        private static PipeMessage Send(string pipeName, string command)
        {
            PipeClient client = new PipeClient(new PipeClientOptions
            {
                PipeName = pipeName,
                ConnectTimeout = TimeSpan.FromMilliseconds(750),
                RequestTimeout = TimeSpan.FromSeconds(3)
            });
            PipeMessage response = client.SendAsync(command).GetAwaiter().GetResult();
            if (!response.Ok)
            {
                string code = response.Error == null ? "unknown" : response.Error.Code;
                throw new IOException("Pipe command '" + command + "' failed with " + code + ".");
            }
            return response;
        }

        private static string DataText(PipeMessage response)
        {
#if NET9_0_OR_GREATER
            if (!response.Data.HasValue) return string.Empty;
            System.Text.Json.JsonElement value = response.Data.Value;
            return value.ValueKind == System.Text.Json.JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : value.ToString();
#else
            return response.Data == null ? string.Empty : response.Data.ToString();
#endif
        }

        private static string DataJson(PipeMessage response)
        {
#if NET9_0_OR_GREATER
            return response.Data.HasValue ? response.Data.Value.GetRawText() : "null";
#else
            return response.Data == null
                ? "null"
                : response.Data.ToString(Newtonsoft.Json.Formatting.None);
#endif
        }
    }
}
