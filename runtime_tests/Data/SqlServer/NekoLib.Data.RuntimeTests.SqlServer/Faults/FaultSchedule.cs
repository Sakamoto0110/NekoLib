#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NekoLib.Data.RuntimeTests.SqlServer.Support;

namespace NekoLib.Data.RuntimeTests.SqlServer.Faults
{
    /// <summary>
    /// The fault kinds this scenario owns. Each one names a transition the
    /// recovery rehearsal has to prove at least once.
    /// </summary>
    internal static class FaultKinds
    {
        public const string ConnectWhileServerDown = "connect-while-server-down";
        public const string TransportLossDuringCommand = "transport-loss-during-command";
        public const string TransportLossDuringTransaction = "transport-loss-during-transaction";
        public const string TransportLossDuringStream = "transport-loss-during-stream";
        public const string StalePooledConnection = "stale-pooled-connection";
        public const string ContainerRestart = "container-restart";
        public const string SchemaRecreation = "schema-recreation";

        /// <summary>The kinds a rehearsal must cover, in the order they are generated from.</summary>
        public static readonly string[] RecoveryRehearsalSet =
        {
            ConnectWhileServerDown,
            TransportLossDuringCommand,
            TransportLossDuringTransaction,
            TransportLossDuringStream,
            StalePooledConnection,
            ContainerRestart,
            SchemaRecreation
        };

        /// <summary>True when acting on the fault requires stopping the adopted container.</summary>
        public static bool NeedsContainerControl(string kind)
        {
            return kind == ConnectWhileServerDown
                || kind == TransportLossDuringCommand
                || kind == TransportLossDuringTransaction
                || kind == TransportLossDuringStream
                || kind == StalePooledConnection
                || kind == ContainerRestart;
        }
    }

    /// <summary>One planned fault, dispatched at a monotonic offset from campaign start.</summary>
    internal sealed class FaultEvent
    {
        public string Id = string.Empty;
        public double OffsetSeconds;
        public string ScenarioId = string.Empty;
        public string Kind = string.Empty;
        public string Target = string.Empty;
        public string ExpectedRecovery = string.Empty;

        /// <summary>Kept ordered so the normalized form is stable across runtimes.</summary>
        public List<KeyValuePair<string, string>> Parameters = new List<KeyValuePair<string, string>>();
    }

    /// <summary>
    /// A schedule generated once, before any work starts, and never changed
    /// during the run.
    /// <para/>
    /// Two properties matter more than the contents. It is persisted before the
    /// first operation, so a machine that dies mid-campaign still says what
    /// should have happened; and it is a function of the seed alone, so the same
    /// seed on the same generator produces a byte-identical normalized document
    /// — including across <c>net481</c> and <c>net9.0</c>, which is why the
    /// random source here is written out rather than taken from the BCL, whose
    /// <c>Random</c> is not the same algorithm on both.
    /// </summary>
    internal sealed class FaultSchedule
    {
        public const int SchemaVersion = 1;
        public const string GeneratorVersion = "e4sql-schedule-1";

        public string CampaignId = string.Empty;
        public string ScenarioId = "E4-SQL";
        public string Mode = string.Empty;
        public int Seed;
        public double RequestedDurationSeconds;
        public double QuietStartSeconds;
        public double QuietEndSeconds;
        public double MinRecoveryIntervalSeconds;
        public int MaxFaults;
        public string GeneratedUtc = string.Empty;
        public List<FaultEvent> Events = new List<FaultEvent>();

        /// <summary>Stable hash of the normalized schedule, provenance excluded.</summary>
        public string Hash = string.Empty;

        public static FaultSchedule Generate(
            string campaignId,
            string scenarioId,
            string mode,
            int seed,
            TimeSpan requestedDuration,
            IReadOnlyList<string> kinds,
            string containerName)
        {
            double total = requestedDuration.TotalSeconds;

            // Quiet windows scale with the run but never vanish: a fault landing
            // during warm-up would measure start-up, and one landing during
            // shutdown would race the cleanup it is supposed to leave clean.
            double quietStart = Math.Max(30.0, Math.Min(total * 0.08, 300.0));
            double quietEnd = Math.Max(30.0, Math.Min(total * 0.08, 300.0));

            FaultSchedule schedule = new FaultSchedule
            {
                CampaignId = campaignId,
                ScenarioId = scenarioId,
                Mode = mode,
                Seed = seed,
                RequestedDurationSeconds = total,
                QuietStartSeconds = quietStart,
                QuietEndSeconds = quietEnd,
                MinRecoveryIntervalSeconds = 45.0,
                MaxFaults = Math.Max(kinds.Count, 1),
                GeneratedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
            };

            double windowStart = quietStart;
            double windowEnd = Math.Max(quietStart + 1.0, total - quietEnd);
            double span = windowEnd - windowStart;

            DeterministicRandom random = new DeterministicRandom(unchecked((ulong)seed) ^ 0x9E3779B97F4A7C15UL);

            // One fault per kind, each in its own slice of the window. Slicing
            // rather than sampling freely is what guarantees the minimum
            // recovery interval without a rejection loop whose iteration count
            // would itself depend on the seed.
            List<string> ordered = new List<string>(kinds);
            Shuffle(ordered, random);

            int count = ordered.Count;
            double slice = count > 0 ? span / count : span;

            for (int i = 0; i < count; i++)
            {
                double sliceStart = windowStart + (i * slice);
                double usable = Math.Max(0.0, slice - schedule.MinRecoveryIntervalSeconds);
                double offset = sliceStart + (random.NextDouble() * usable);

                string kind = ordered[i];
                FaultEvent planned = new FaultEvent
                {
                    Id = campaignId + "-f" + (i + 1).ToString("D2", CultureInfo.InvariantCulture),
                    OffsetSeconds = Math.Round(offset, 3),
                    ScenarioId = scenarioId,
                    Kind = kind,
                    Target = FaultKinds.NeedsContainerControl(kind) ? containerName : "scenario-database",
                    ExpectedRecovery = DescribeExpectedRecovery(kind)
                };

                planned.Parameters.Add(new KeyValuePair<string, string>(
                    "downtimeSeconds",
                    (kind == FaultKinds.ContainerRestart ? 0 : 5).ToString(CultureInfo.InvariantCulture)));

                schedule.Events.Add(planned);
            }

            schedule.Events.Sort((a, b) => a.OffsetSeconds.CompareTo(b.OffsetSeconds));
            schedule.Hash = ComputeHash(schedule);
            return schedule;
        }

        private static string DescribeExpectedRecovery(string kind)
        {
            switch (kind)
            {
                case FaultKinds.ConnectWhileServerDown:
                    return "the open attempt fails with a provider connection error and no session is left behind; " +
                           "ordinary work succeeds once the server is back";
                case FaultKinds.TransportLossDuringCommand:
                    return "the in-flight command fails with a provider transport error, the connection is disposed, " +
                           "and a fresh command succeeds after recovery";
                case FaultKinds.TransportLossDuringTransaction:
                    return "the transaction does not commit, the session disposes without throwing, " +
                           "and the database shows no partial effect after recovery";
                case FaultKinds.TransportLossDuringStream:
                    return "the stream reports exactly one failed terminal outcome and releases its reader and connection";
                case FaultKinds.StalePooledConnection:
                    return "a pooled handle from before the interruption fails or is discarded, and the scenario's own " +
                           "bounded retry reaches a working connection";
                case FaultKinds.ContainerRestart:
                    return "the server returns, the scenario database is reachable again, and ordinary commands succeed";
                case FaultKinds.SchemaRecreation:
                    return "the schema is recreated deterministically and ordinary commands succeed against it";
                default:
                    return "unspecified";
            }
        }

        private static void Shuffle(List<string> items, DeterministicRandom random)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = (int)(random.NextDouble() * (i + 1));
                if (j > i) j = i;

                string swap = items[i];
                items[i] = items[j];
                items[j] = swap;
            }
        }

        /// <summary>
        /// The canonical text the hash covers. The generation timestamp is
        /// deliberately absent: it is provenance, and including it would make
        /// two runs of the same seed disagree by construction.
        /// </summary>
        public string Normalize()
        {
            StringBuilder text = new StringBuilder();
            text.Append("schema=").Append(SchemaVersion).Append('\n');
            text.Append("generator=").Append(GeneratorVersion).Append('\n');
            text.Append("scenario=").Append(ScenarioId).Append('\n');
            text.Append("mode=").Append(Mode).Append('\n');
            text.Append("seed=").Append(Seed.ToString(CultureInfo.InvariantCulture)).Append('\n');
            text.Append("duration=").Append(RequestedDurationSeconds.ToString("F3", CultureInfo.InvariantCulture)).Append('\n');
            text.Append("quietStart=").Append(QuietStartSeconds.ToString("F3", CultureInfo.InvariantCulture)).Append('\n');
            text.Append("quietEnd=").Append(QuietEndSeconds.ToString("F3", CultureInfo.InvariantCulture)).Append('\n');
            text.Append("minRecovery=").Append(MinRecoveryIntervalSeconds.ToString("F3", CultureInfo.InvariantCulture)).Append('\n');
            text.Append("maxFaults=").Append(MaxFaults.ToString(CultureInfo.InvariantCulture)).Append('\n');

            foreach (FaultEvent planned in Events)
            {
                text.Append("event=")
                    .Append(planned.OffsetSeconds.ToString("F3", CultureInfo.InvariantCulture)).Append('|')
                    .Append(planned.Kind).Append('|')
                    .Append(planned.Target).Append('|');

                foreach (KeyValuePair<string, string> parameter in planned.Parameters)
                    text.Append(parameter.Key).Append('=').Append(parameter.Value).Append(';');

                text.Append('\n');
            }

            return text.ToString();
        }

        private static string ComputeHash(FaultSchedule schedule)
        {
            // FNV-1a, written out so the value does not depend on a BCL hash
            // whose implementation is randomized per process on modern .NET.
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;

            ulong hash = offset;
            foreach (char c in schedule.Normalize())
            {
                hash ^= c;
                hash *= prime;
            }

            return "fnv1a64:" + hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        public string ToJson()
        {
            JsonWriter json = new JsonWriter();
            json.Object(null, () =>
            {
                json.Prop("schemaVersion", SchemaVersion);
                json.Prop("generatorVersion", GeneratorVersion);
                json.Prop("campaignId", CampaignId);
                json.Prop("scenarioId", ScenarioId);
                json.Prop("mode", Mode);
                json.Prop("seed", Seed);
                json.Prop("requestedDurationSeconds", RequestedDurationSeconds);
                json.Prop("quietStartSeconds", QuietStartSeconds);
                json.Prop("quietEndSeconds", QuietEndSeconds);
                json.Prop("minRecoveryIntervalSeconds", MinRecoveryIntervalSeconds);
                json.Prop("maxFaults", MaxFaults);
                json.Prop("generatedUtc", GeneratedUtc);
                json.Prop("hash", Hash);

                json.Array("events", () =>
                {
                    foreach (FaultEvent planned in Events)
                    {
                        json.Object(null, () =>
                        {
                            json.Prop("id", planned.Id);
                            json.Prop("offsetSeconds", planned.OffsetSeconds);
                            json.Prop("scenarioId", planned.ScenarioId);
                            json.Prop("kind", planned.Kind);
                            json.Prop("target", planned.Target);
                            json.Prop("expectedRecovery", planned.ExpectedRecovery);
                            json.Object("parameters", () =>
                            {
                                foreach (KeyValuePair<string, string> parameter in planned.Parameters)
                                    json.Prop(parameter.Key, parameter.Value);
                            });
                        });
                    }
                });
            });

            return json.ToString();
        }

        /// <summary>
        /// Loads a schedule produced elsewhere, so an orchestrator can own fault
        /// generation for a whole campaign. Only this scenario's events are
        /// kept; the file may describe several.
        /// </summary>
        public static FaultSchedule Load(string path, string scenarioId)
        {
            Dictionary<string, object?> map = JsonParser.AsObject(
                JsonParser.Parse(File.ReadAllText(path)),
                "the fault schedule");

            FaultSchedule schedule = new FaultSchedule
            {
                CampaignId = JsonParser.OptionalString(map, "campaignId") ?? string.Empty,
                ScenarioId = scenarioId,
                Mode = JsonParser.OptionalString(map, "mode") ?? "supplied",
                Seed = (int)JsonParser.RequireInt(map, "seed"),
                GeneratedUtc = JsonParser.OptionalString(map, "generatedUtc") ?? string.Empty,
                Hash = JsonParser.OptionalString(map, "hash") ?? string.Empty
            };

            List<object?> events = JsonParser.AsArray(
                map.TryGetValue("events", out object? raw) ? raw : new List<object?>(),
                "events");

            foreach (object? item in events)
            {
                Dictionary<string, object?> entry = JsonParser.AsObject(item, "a schedule event");
                string owner = JsonParser.OptionalString(entry, "scenarioId") ?? scenarioId;
                if (!string.Equals(owner, scenarioId, StringComparison.Ordinal)) continue;

                FaultEvent planned = new FaultEvent
                {
                    Id = JsonParser.OptionalString(entry, "id") ?? string.Empty,
                    OffsetSeconds = JsonParser.RequireInt(entry, "offsetSeconds"),
                    ScenarioId = owner,
                    Kind = JsonParser.RequireString(entry, "kind"),
                    Target = JsonParser.OptionalString(entry, "target") ?? string.Empty,
                    ExpectedRecovery = JsonParser.OptionalString(entry, "expectedRecovery") ?? string.Empty
                };

                schedule.Events.Add(planned);
            }

            schedule.Events.Sort((a, b) => a.OffsetSeconds.CompareTo(b.OffsetSeconds));
            return schedule;
        }
    }

    /// <summary>
    /// A tiny SplitMix64 generator.
    /// <para/>
    /// <c>System.Random</c> could not be used here: its algorithm differs
    /// between .NET Framework and modern .NET, so the same seed would produce
    /// two different schedules on the two targets this scenario has to compare.
    /// </summary>
    internal sealed class DeterministicRandom
    {
        private ulong _state;

        public DeterministicRandom(ulong seed)
        {
            _state = seed;
        }

        public ulong NextUInt64()
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                ulong z = _state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>A value in [0, 1), built from 53 bits so it is exactly representable.</summary>
        public double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);
    }
}
