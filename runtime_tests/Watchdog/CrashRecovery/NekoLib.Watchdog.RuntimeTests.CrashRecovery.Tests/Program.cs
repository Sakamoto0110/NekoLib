#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using NekoLib.RuntimeTests.Harness;
using NekoLib.RuntimeTests.Harness.Faults;
using NekoLib.Watchdog.RuntimeTests.CrashRecovery.Shared;

namespace NekoLib.Watchdog.RuntimeTests.CrashRecovery.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            Suite suite = new Suite();
            suite.Run("child plan round-trips durably", ChildPlanRoundTrips);
            suite.Run("schedule is stable and seed-sensitive", ScheduleIsDeterministic);
            suite.Run("all generated fault kinds are recognized", GeneratedFaultKindsAreKnown);
            suite.Run("pipe identity matches the public Watchdog algorithm vector", PipeIdentityMatchesVector);
            suite.Run("package provenance binds deployed Host bytes", PackageProvenanceMatchesPayload);
            suite.Run("package provenance rejects different deployed bytes", PackageProvenanceRejectsMismatch);
            return suite.Report();
        }

        private static void ChildPlanRoundTrips(Assert assert)
        {
            using (Workspace workspace = new Workspace())
            {
                string path = workspace.Path("plan.tsv");
                ChildPlan expected = new ChildPlan
                {
                    CampaignId = "campaign",
                    ScheduleHash = "fnv1a64:0123456789abcdef",
                    OriginTimestamp = 123456789,
                    TimestampFrequency = 10000000
                };
                expected.Events.Add(new ChildPlanEvent
                {
                    Id = "campaign-f01",
                    Kind = FaultKinds.UnhandledCrash,
                    OffsetSeconds = 42.125,
                    Repetitions = 1
                });

                expected.SaveDurably(path);
                ChildPlan actual = ChildPlan.Load(path);

                assert.Equal(expected.CampaignId, actual.CampaignId, "campaign ID");
                assert.Equal(expected.ScheduleHash, actual.ScheduleHash, "schedule hash");
                assert.Equal(expected.OriginTimestamp, actual.OriginTimestamp, "origin timestamp");
                assert.Equal(expected.TimestampFrequency, actual.TimestampFrequency, "timestamp frequency");
                assert.Equal(1, actual.Events.Count, "event count");
                assert.Equal("42.125", actual.Events[0].OffsetSeconds.ToString("F3", CultureInfo.InvariantCulture),
                    "event offset");
            }
        }

        private static void ScheduleIsDeterministic(Assert assert)
        {
            FaultSchedule first = ScheduleFactory.Build(Options(ScenarioMode.RecoveryRehearsal, 20260810));
            FaultSchedule second = ScheduleFactory.Build(Options(ScenarioMode.RecoveryRehearsal, 20260810));
            FaultSchedule different = ScheduleFactory.Build(Options(ScenarioMode.RecoveryRehearsal, 99));

            assert.Equal(first.Hash, second.Hash, "same-seed hash");
            assert.That(!string.Equals(first.Hash, different.Hash, StringComparison.Ordinal),
                "a different seed must change the normalized schedule");
        }

        private static void GeneratedFaultKindsAreKnown(Assert assert)
        {
            foreach (ScenarioMode mode in new[]
            {
                ScenarioMode.Smoke, ScenarioMode.RecoveryRehearsal, ScenarioMode.Soak
            })
            {
                FaultSchedule schedule = ScheduleFactory.Build(Options(mode, 20260810));
                assert.That(schedule.Events.Count > 0, mode + " generated no events");
                foreach (FaultEvent planned in schedule.Events)
                    assert.That(FaultKinds.IsKnown(planned.Kind), "unknown generated kind " + planned.Kind);
            }

            assert.Equal(12, FaultKinds.FastCrashCount, "fast-loop terminal count");
        }

        private static void PipeIdentityMatchesVector(Assert assert)
        {
            assert.Equal(
                "NekoLib.Watchdog.263F98E5B94303E4",
                WatchdogProtocol.PipeName(@"C:\Apps\Neko.exe"),
                "pipe identity");
        }

        private static void PackageProvenanceMatchesPayload(Assert assert)
        {
            using (Workspace workspace = new Workspace())
            {
                byte[] hostBytes = Encoding.UTF8.GetBytes("exact-host-payload");
                string application = workspace.Directory("application");
                File.WriteAllBytes(Path.Combine(application,
                    "NekoLib.Watchdog.RuntimeTests.CrashRecovery.Child.exe"), new byte[] { 1 });
                string hostRoot = workspace.Directory("application", "NekoLib.Watchdog.Host");
                File.WriteAllBytes(Path.Combine(hostRoot, "NekoLib.Watchdog.Host.exe"), hostBytes);
                string package = WritePackage(workspace, hostBytes);

                ScenarioOptions options = PackageOptions(application, package);
                DeploymentLayoutEvidence evidence = DeploymentLayoutEvidence.Resolve(options);

                assert.That(evidence.SupportsPackageClaim, "the package layout was not recorded");
                assert.Equal("1.2.3-test.4", evidence.PackageVersion, "package version");
                assert.Equal("tools/net481/NekoLib.Watchdog.Host.exe", evidence.PackagePayloadEntry,
                    "matched payload entry");
                assert.Equal(64, evidence.PackageSha256.Length, "package SHA-256 length");
            }
        }

        private static void PackageProvenanceRejectsMismatch(Assert assert)
        {
            using (Workspace workspace = new Workspace())
            {
                string application = workspace.Directory("application");
                File.WriteAllBytes(Path.Combine(application,
                    "NekoLib.Watchdog.RuntimeTests.CrashRecovery.Child.exe"), new byte[] { 1 });
                string hostRoot = workspace.Directory("application", "NekoLib.Watchdog.Host");
                File.WriteAllBytes(Path.Combine(hostRoot, "NekoLib.Watchdog.Host.exe"),
                    Encoding.UTF8.GetBytes("different-host"));
                string package = WritePackage(workspace, Encoding.UTF8.GetBytes("package-host"));

                bool rejected = false;
                try { DeploymentLayoutEvidence.Resolve(PackageOptions(application, package)); }
                catch (InvalidDataException) { rejected = true; }
                assert.That(rejected, "different deployed Host bytes were accepted");
            }
        }

        private static ScenarioOptions Options(ScenarioMode mode, int seed) => new ScenarioOptions
        {
            Mode = mode,
            Seed = seed,
            CampaignId = "e3wdog-tests",
            SmokeDuration = TimeSpan.FromMinutes(15),
            RehearsalDuration = TimeSpan.FromMinutes(60),
            SoakDuration = TimeSpan.FromHours(4)
        };

        private static ScenarioOptions PackageOptions(string application, string package) => new ScenarioOptions
        {
            Layout = DeploymentLayout.DisposablePackage,
            ApplicationRoot = application,
            PackageFile = package,
            PackageVersion = "1.2.3-test.4"
        };

        private static string WritePackage(Workspace workspace, byte[] hostBytes)
        {
            string path = workspace.Path("NekoLib.Watchdog.Host.1.2.3-test.4.nupkg");
            using (FileStream stream = File.Create(path))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create, false))
            {
                WriteEntry(archive, "NekoLib.Watchdog.Host.nuspec",
                    "<?xml version=\"1.0\"?><package><metadata><id>NekoLib.Watchdog.Host</id>" +
                    "<version>1.2.3-test.4</version></metadata></package>");
                ZipArchiveEntry payload = archive.CreateEntry("tools/net481/NekoLib.Watchdog.Host.exe");
                using (Stream payloadStream = payload.Open())
                    payloadStream.Write(hostBytes, 0, hostBytes.Length);
            }
            return path;
        }

        private static void WriteEntry(ZipArchive archive, string name, string content)
        {
            ZipArchiveEntry entry = archive.CreateEntry(name);
            using (Stream stream = entry.Open())
            using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
                writer.Write(content);
        }
    }

    internal sealed class Workspace : IDisposable
    {
        private readonly string _root = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "NekoLib-E3WDOG-Tests", Guid.NewGuid().ToString("N"));

        public Workspace() { System.IO.Directory.CreateDirectory(_root); }

        public string Path(params string[] parts) =>
            parts.Aggregate(_root, System.IO.Path.Combine);

        public string Directory(params string[] parts)
        {
            string path = Path(parts);
            System.IO.Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose()
        {
            try { System.IO.Directory.Delete(_root, true); } catch { }
        }
    }

    internal sealed class Assert
    {
        public void That(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        public void Equal(long expected, long actual, string what)
        {
            if (expected != actual)
                throw new InvalidOperationException(what + ": expected " + expected + ", got " + actual);
        }

        public void Equal(string expected, string actual, string what)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
                throw new InvalidOperationException(what + ": expected '" + expected + "', got '" + actual + "'");
        }
    }

    internal sealed class Suite
    {
        private readonly List<string> _failures = new List<string>();
        private int _passed;

        public void Run(string name, Action<Assert> body)
        {
            try
            {
                body(new Assert());
                _passed++;
                Console.WriteLine("ok  " + name);
            }
            catch (Exception ex)
            {
                _failures.Add(name + ": " + ex.GetType().Name + ": " + ex.Message);
                Console.WriteLine("!!  " + name + "  " + ex.Message);
            }
        }

        public int Report()
        {
            Console.WriteLine();
            Console.WriteLine(_passed + " passed, " + _failures.Count + " failed");
            foreach (string failure in _failures) Console.Error.WriteLine(failure);
            return _failures.Count == 0 ? 0 : 1;
        }
    }
}
