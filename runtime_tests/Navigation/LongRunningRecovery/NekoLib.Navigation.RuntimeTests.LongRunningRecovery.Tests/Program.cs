#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NekoLib.RuntimeTests.Harness;
using NekoLib.RuntimeTests.Harness.Faults;

namespace NekoLib.Navigation.RuntimeTests.LongRunningRecovery.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            Suite suite = new Suite();
            suite.Run("schedule is stable and seed-sensitive", ScheduleIsStable);
            suite.Run("schedule is independent of platform label", ScheduleIsPlatformIndependent);
            suite.Run("recovery plan covers the complete fault vocabulary", RecoveryCoversVocabulary);
            suite.Run("smoke plan contains no recovery faults", SmokeContainsNoFaults);
            suite.Run("persisted plan is exact UTF-8 JSON", PersistedPlanIsExact);
            suite.Run("scenario options preserve workload boundaries", OptionsPreserveBoundaries);
            suite.Run("orchestrated instance id owns its schedule events", InstanceIdOwnsScheduleEvents);
            suite.Run("unsafe orchestrated instance id is rejected", UnsafeInstanceIdIsRejected);
            suite.Run("page fault oracle accepts the documented factory wrapper", PageFaultOracleAcceptsFactoryWrapper);
            return suite.Report();
        }

        private static void ScheduleIsStable(Assert assert)
        {
            FaultSchedule first = ScenarioPlan.Build(Options("winforms", 20260810));
            FaultSchedule second = ScenarioPlan.Build(Options("winforms", 20260810));
            FaultSchedule different = ScenarioPlan.Build(Options("winforms", 99));

            assert.Equal(first.Hash, second.Hash, "same-seed hash");
            assert.That(!string.Equals(first.Hash, different.Hash, StringComparison.Ordinal),
                "a different seed must change the normalized schedule");
        }

        private static void ScheduleIsPlatformIndependent(Assert assert)
        {
            FaultSchedule winForms = ScenarioPlan.Build(Options("winforms", 20260810));
            FaultSchedule wpf = ScenarioPlan.Build(Options("wpf", 20260810));
            assert.Equal(winForms.Hash, wpf.Hash, "cross-platform hash");
            assert.Equal(EventSignature(winForms), EventSignature(wpf), "cross-platform events");
        }

        private static void RecoveryCoversVocabulary(Assert assert)
        {
            FaultSchedule schedule = ScenarioPlan.Build(Options("winforms", 20260810));
            string[] generated = schedule.Events.Select(item => item.Kind)
                .OrderBy(item => item, StringComparer.Ordinal).ToArray();
            string[] expected = FaultKinds.All.OrderBy(item => item, StringComparer.Ordinal).ToArray();

            assert.Equal(expected.Length, generated.Length, "fault count");
            assert.Equal(string.Join("|", expected), string.Join("|", generated), "fault kinds");
            assert.That(schedule.Events.All(item => !string.IsNullOrWhiteSpace(item.Target)),
                "every fault must name a target");
            assert.That(schedule.Events.All(item => !string.IsNullOrWhiteSpace(item.ExpectedRecovery)),
                "every fault must describe recovery");
        }

        private static void SmokeContainsNoFaults(Assert assert)
        {
            ScenarioOptions options = Options("winforms", 20260810);
            options.Mode = ScenarioMode.Smoke;
            FaultSchedule schedule = ScenarioPlan.Build(options);
            assert.Equal(0, schedule.Events.Count, "smoke fault count");
        }

        private static void PersistedPlanIsExact(Assert assert)
        {
            string root = Path.Combine(Path.GetTempPath(), "NekoLib-E3NAV-Tests", Guid.NewGuid().ToString("N"));
            try
            {
                string path = Path.Combine(root, "nested", "schedule.json");
                FaultSchedule schedule = ScenarioPlan.Build(Options("winforms", 20260810));
                ScenarioPlan.Persist(path, schedule);

                byte[] bytes = File.ReadAllBytes(path);
                assert.That(bytes.Length > 3, "persisted plan is empty");
                assert.That(!(bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf),
                    "persisted plan contains a UTF-8 BOM");
                assert.Equal(schedule.ToJson(), Encoding.UTF8.GetString(bytes), "persisted JSON");
            }
            finally
            {
                try { Directory.Delete(root, true); } catch { }
            }
        }

        private static void OptionsPreserveBoundaries(Assert assert)
        {
            ScenarioOptions options = new ScenarioOptions("winforms");
            bool parsed = options.TryParse(new[]
            {
                "--recovery-rehearsal",
                "--campaign-id", "e3nav-contracts",
                "--worker-id", "contracts",
                "--scenario-id", "E3-NAV-winforms-net481",
                "--seed", "42",
                "--rehearsal-duration", "45m",
                "--idle-timeout-ms", "150000",
                "--switches-per-cycle", "512"
            }, out string diagnostic);

            assert.That(parsed, diagnostic);
            assert.Equal(42, options.Seed, "seed");
            assert.Equal(150000, options.IdleTimeoutMilliseconds, "idle timeout");
            assert.Equal(512, options.SwitchesPerCycle, "switches per cycle");
            assert.Equal("e3nav-contracts", options.CampaignId, "campaign ID");
            assert.Equal("E3-NAV-winforms-net481", options.ScenarioId, "scenario ID");
        }

        private static void InstanceIdOwnsScheduleEvents(Assert assert)
        {
            ScenarioOptions options = new ScenarioOptions("winforms");
            bool parsed = options.TryParse(new[]
            {
                "--recovery-rehearsal",
                "--scenario-id", "E3-NAV-winforms-net9.0"
            }, out string diagnostic);

            assert.That(parsed, diagnostic);
            FaultSchedule schedule = ScenarioPlan.Build(options);
            assert.Equal("E3-NAV-winforms-net9.0", schedule.ScenarioId, "schedule scenario ID");
            assert.That(schedule.Events.All(item => item.ScenarioId == options.ScenarioId),
                "every event must belong to the orchestrator entry");
        }

        private static void UnsafeInstanceIdIsRejected(Assert assert)
        {
            ScenarioOptions options = new ScenarioOptions("winforms");
            bool parsed = options.TryParse(new[]
            {
                "--smoke",
                "--scenario-id", "..\\escaped"
            }, out string diagnostic);

            assert.That(!parsed, "an unsafe scenario ID was accepted");
            assert.That(diagnostic.IndexOf("safe directory name", StringComparison.Ordinal) >= 0,
                "the rejection did not explain the path boundary");
        }

        private static void PageFaultOracleAcceptsFactoryWrapper(Assert assert)
        {
            Exception injected = new ScenarioInjectedException("injected");
            Exception wrapped = new InvalidOperationException(
                "Factory failed to create the page.",
                new InvalidOperationException("Default constructor failed.", injected));

            assert.That(NavigationRecovery.ContainsScenarioInjectedException(injected),
                "a direct scenario exception was rejected");
            assert.That(NavigationRecovery.ContainsScenarioInjectedException(wrapped),
                "the documented factory wrapper was rejected");
            assert.That(!NavigationRecovery.ContainsScenarioInjectedException(
                    new InvalidOperationException("unrelated")),
                "an unrelated exception was accepted");
        }

        private static ScenarioOptions Options(string platform, int seed) => new ScenarioOptions(platform)
        {
            Mode = ScenarioMode.RecoveryRehearsal,
            Seed = seed,
            CampaignId = "e3nav-contracts",
            RehearsalDuration = TimeSpan.FromMinutes(45),
            SmokeDuration = TimeSpan.FromMinutes(20),
            SoakDuration = TimeSpan.FromHours(4)
        };

        private static string EventSignature(FaultSchedule schedule) => string.Join("|",
            schedule.Events.Select(item =>
                item.Id + ":" + item.OffsetSeconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                ":" + item.Kind + ":" + item.Target));
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
