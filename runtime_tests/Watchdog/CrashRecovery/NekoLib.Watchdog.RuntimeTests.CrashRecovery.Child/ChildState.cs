#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace NekoLib.Watchdog.RuntimeTests.CrashRecovery.Child
{
    internal static class ChildState
    {
        public static int ClaimGeneration(string runRoot, string campaignId, int pid, string argumentsHash)
        {
            string root = Path.Combine(runRoot, "state", "generations");
            Directory.CreateDirectory(root);

            for (int generation = 1; generation < 1000000; generation++)
            {
                string path = Path.Combine(root, "generation-" + generation.ToString("D6", CultureInfo.InvariantCulture) + ".json");
                try
                {
                    WriteNewDurably(path, Object(
                        "campaignId", campaignId,
                        "generation", generation.ToString(CultureInfo.InvariantCulture),
                        "pid", pid.ToString(CultureInfo.InvariantCulture),
                        "argumentsHash", argumentsHash,
                        "claimedUtc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)));
                    return generation;
                }
                catch (IOException)
                {
                    // Another generation already owns this number.
                }
            }

            throw new InvalidOperationException("The E3-WDOG generation space was exhausted.");
        }

        public static bool TryClaimEvent(string runRoot, string eventId, int generation)
        {
            string root = Path.Combine(runRoot, "state", "claims");
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, Safe(eventId) + ".json");

            try
            {
                WriteNewDurably(path, Object(
                    "eventId", eventId,
                    "generation", generation.ToString(CultureInfo.InvariantCulture),
                    "claimedUtc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)));
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        public static bool IsEventClaimed(string runRoot, string eventId) =>
            File.Exists(Path.Combine(runRoot, "state", "claims", Safe(eventId) + ".json"));

        public static string ArmedPath(string runRoot, string eventId, int generation)
        {
            string root = Path.Combine(runRoot, "state", "armed");
            Directory.CreateDirectory(root);
            return Path.Combine(
                root,
                Safe(eventId) + "-g" + generation.ToString("D6", CultureInfo.InvariantCulture) + ".json");
        }

        public static string ProbePath(string runRoot, int generation) => Path.Combine(
            runRoot,
            "state",
            "probes",
            "generation-" + generation.ToString("D6", CultureInfo.InvariantCulture) + ".ack");

        public static int CountArmed(string runRoot, string eventId)
        {
            string root = Path.Combine(runRoot, "state", "armed");
            return Directory.Exists(root)
                ? Directory.GetFiles(root, Safe(eventId) + "-g*.json").Length
                : 0;
        }

        public static void MarkCompleted(string runRoot, string eventId, int generation)
        {
            string root = Path.Combine(runRoot, "state", "completed");
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, Safe(eventId) + ".json");
            if (File.Exists(path)) return;

            try
            {
                WriteNewDurably(path, Object(
                    "eventId", eventId,
                    "generation", generation.ToString(CultureInfo.InvariantCulture),
                    "completedUtc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)));
            }
            catch (IOException) { }
        }

        public static void WriteReady(
            string runRoot,
            string campaignId,
            int generation,
            int pid,
            string controlPipe,
            string argumentsHash,
            string logToken,
            long readyTimestamp)
        {
            string root = Path.Combine(runRoot, "state", "ready");
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "generation-" + generation.ToString("D6", CultureInfo.InvariantCulture) + ".json");

            WriteDurably(path, Object(
                "campaignId", campaignId,
                "generation", generation.ToString(CultureInfo.InvariantCulture),
                "pid", pid.ToString(CultureInfo.InvariantCulture),
                "controlPipe", controlPipe,
                "argumentsHash", argumentsHash,
                "applicationVersion", typeof(ChildState).Assembly.GetName().Version?.ToString() ?? "unknown",
                "status", "ready",
                "logToken", logToken,
                "readyTimestamp", readyTimestamp.ToString(CultureInfo.InvariantCulture),
                "readyUtc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)));
        }

        public static void WriteArmed(
            string runRoot,
            string campaignId,
            string eventId,
            string kind,
            int generation,
            int pid,
            long timestamp)
        {
            WriteDurably(ArmedPath(runRoot, eventId, generation), Object(
                "campaignId", campaignId,
                "eventId", eventId,
                "kind", kind,
                "generation", generation.ToString(CultureInfo.InvariantCulture),
                "pid", pid.ToString(CultureInfo.InvariantCulture),
                "status", "armed",
                "armedTimestamp", timestamp.ToString(CultureInfo.InvariantCulture),
                "armedUtc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)));
        }

        public static string ArgumentsHash(string[] args)
        {
            ulong hash = 14695981039346656037UL;
            string normalized = string.Join("\u001f", args);
            unchecked
            {
                foreach (char character in normalized)
                {
                    hash ^= character;
                    hash *= 1099511628211UL;
                }
            }
            return "fnv1a64:" + hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        public static string Object(params string[] pairs)
        {
            StringBuilder json = new StringBuilder();
            json.AppendLine("{");
            for (int i = 0; i < pairs.Length; i += 2)
            {
                json.Append("  \"").Append(Escape(pairs[i])).Append("\": \"")
                    .Append(Escape(pairs[i + 1])).Append('"');
                if (i + 2 < pairs.Length) json.Append(',');
                json.AppendLine();
            }
            json.AppendLine("}");
            return json.ToString();
        }

        public static void WriteDurably(string path, string content)
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            byte[] bytes = new UTF8Encoding(false).GetBytes(content);
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        private static void WriteNewDurably(string path, string content)
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(content);
            using (FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        public static string Safe(string value)
        {
            StringBuilder safe = new StringBuilder(value.Length);
            foreach (char character in value)
                if (char.IsLetterOrDigit(character) || character == '-' || character == '_') safe.Append(character);
            return safe.ToString();
        }

        private static string Escape(string value) =>
            (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"")
                .Replace("\r", "\\r").Replace("\n", "\\n");
    }
}
