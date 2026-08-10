#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace NekoLib.Watchdog.RuntimeTests.CrashRecovery.Shared
{
    internal sealed class ChildPlanEvent
    {
        public string Id = string.Empty;
        public string Kind = string.Empty;
        public double OffsetSeconds;
        public int Repetitions = 1;
    }

    /// <summary>
    /// Scenario-owned instructions consumed by the application child.
    /// <para/>
    /// The controller writes this file durably before it starts any process.
    /// The changing monotonic origin is provenance only; <see cref="ScheduleHash"/>
    /// is the deterministic hash of the normalized harness schedule and is what
    /// comparable runs use.
    /// </summary>
    internal sealed class ChildPlan
    {
        private const string Header = "e3wdog-child-plan-v1";

        public string CampaignId = string.Empty;
        public string ScheduleHash = string.Empty;
        public long OriginTimestamp;
        public long TimestampFrequency;
        public readonly List<ChildPlanEvent> Events = new List<ChildPlanEvent>();

        public static ChildPlan Load(string path)
        {
            string[] lines = File.ReadAllLines(path);
            if (lines.Length < 5 || !string.Equals(lines[0], Header, StringComparison.Ordinal))
                throw new InvalidDataException("Unsupported E3-WDOG child plan.");

            ChildPlan plan = new ChildPlan();
            for (int i = 1; i < lines.Length; i++)
            {
                string[] parts = lines[i].Split('\t');
                if (parts.Length == 0) continue;

                switch (parts[0])
                {
                    case "campaign":
                        Require(parts, 2, lines[i]);
                        plan.CampaignId = parts[1];
                        break;
                    case "schedule-hash":
                        Require(parts, 2, lines[i]);
                        plan.ScheduleHash = parts[1];
                        break;
                    case "origin-timestamp":
                        Require(parts, 2, lines[i]);
                        plan.OriginTimestamp = long.Parse(parts[1], CultureInfo.InvariantCulture);
                        break;
                    case "timestamp-frequency":
                        Require(parts, 2, lines[i]);
                        plan.TimestampFrequency = long.Parse(parts[1], CultureInfo.InvariantCulture);
                        break;
                    case "event":
                        Require(parts, 5, lines[i]);
                        plan.Events.Add(new ChildPlanEvent
                        {
                            Id = parts[1],
                            Kind = parts[2],
                            OffsetSeconds = double.Parse(parts[3], CultureInfo.InvariantCulture),
                            Repetitions = int.Parse(parts[4], CultureInfo.InvariantCulture)
                        });
                        break;
                }
            }

            if (plan.CampaignId.Length == 0 || plan.ScheduleHash.Length == 0 ||
                plan.OriginTimestamp <= 0 || plan.TimestampFrequency <= 0)
            {
                throw new InvalidDataException("The E3-WDOG child plan is incomplete.");
            }

            plan.Events.Sort((left, right) => left.OffsetSeconds.CompareTo(right.OffsetSeconds));
            return plan;
        }

        public void SaveDurably(string path)
        {
            ValidateField(CampaignId, nameof(CampaignId));
            ValidateField(ScheduleHash, nameof(ScheduleHash));

            StringBuilder text = new StringBuilder();
            text.AppendLine(Header);
            text.Append("campaign\t").Append(CampaignId).AppendLine();
            text.Append("schedule-hash\t").Append(ScheduleHash).AppendLine();
            text.Append("origin-timestamp\t")
                .Append(OriginTimestamp.ToString(CultureInfo.InvariantCulture)).AppendLine();
            text.Append("timestamp-frequency\t")
                .Append(TimestampFrequency.ToString(CultureInfo.InvariantCulture)).AppendLine();

            foreach (ChildPlanEvent planned in Events)
            {
                ValidateField(planned.Id, nameof(planned.Id));
                ValidateField(planned.Kind, nameof(planned.Kind));
                text.Append("event\t")
                    .Append(planned.Id).Append('\t')
                    .Append(planned.Kind).Append('\t')
                    .Append(planned.OffsetSeconds.ToString("F3", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(planned.Repetitions.ToString(CultureInfo.InvariantCulture)).AppendLine();
            }

            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            byte[] bytes = new UTF8Encoding(false).GetBytes(text.ToString());
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        public double ElapsedSeconds(long timestamp) =>
            (timestamp - OriginTimestamp) / (double)TimestampFrequency;

        private static void Require(string[] parts, int count, string line)
        {
            if (parts.Length != count)
                throw new InvalidDataException("Malformed E3-WDOG child plan line: " + line);
        }

        private static void ValidateField(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value) || value.IndexOfAny(new[] { '\t', '\r', '\n' }) >= 0)
                throw new InvalidDataException(name + " is not a safe child-plan field.");
        }
    }
}
