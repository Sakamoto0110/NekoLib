using System;
using Xunit;

namespace NekoLib.Watchdog.Tests.Unit
{
    public sealed class UiHangMonitorTests
    {
        [Fact]
        public void SustainedFailure_RequiresTheFullConsecutiveTimeout()
        {
            var monitor = NewMonitor();
            Assert.False(monitor.Check(123, 0, true));
            Assert.False(monitor.Check(123, 750, true));
            Assert.True(monitor.Check(123, 1000, true));
        }

        [Fact]
        public void StartupGrace_DoesNotCountTowardFailureDuration()
        {
            var monitor = NewMonitor(grace: 1000);
            Assert.False(monitor.Check(123, 0, true));
            Assert.False(monitor.Check(123, 750, true));
            Assert.False(monitor.Check(123, 1000, true));
            Assert.False(monitor.Check(123, 1750, true));
            Assert.True(monitor.Check(123, 2000, true));
        }

        [Theory]
        [InlineData((int)UiWindowState.Responding)]
        [InlineData((int)UiWindowState.NoWindow)]
        [InlineData((int)UiWindowState.Unavailable)]
        public void NonFailureObservation_ClearsAnEarlierFailureInterval(int state)
        {
            var observation = new UiWindowSample(UiWindowState.Unresponsive, 42);
            var monitor = NewMonitor(probe: pid => observation);
            Assert.False(monitor.Check(123, 0, true));
            Assert.False(monitor.Check(123, 750, true));
            observation = new UiWindowSample((UiWindowState)state, 42, 5);
            Assert.False(monitor.Check(123, 1000, true));
            observation = new UiWindowSample(UiWindowState.Unresponsive, 42);
            Assert.False(monitor.Check(123, 1250, true));
            Assert.False(monitor.Check(123, 2000, true));
            Assert.True(monitor.Check(123, 2250, true));
        }

        [Fact]
        public void ReplacedWindow_DoesNotInheritThePreviousWindowFailure()
        {
            long window = 42;
            var monitor = NewMonitor(probe: pid => new UiWindowSample(UiWindowState.Unresponsive, window));
            Assert.False(monitor.Check(123, 0, true));
            window = 43;
            Assert.False(monitor.Check(123, 1000, true));
            Assert.False(monitor.Check(123, 1750, true));
            Assert.True(monitor.Check(123, 2000, true));
        }

        [Fact]
        public void ReplacementPid_GetsItsOwnGraceAndFailureInterval()
        {
            var monitor = NewMonitor(grace: 1000);
            Assert.False(monitor.Check(123, 0, true));
            Assert.False(monitor.Check(123, 1000, true));
            Assert.False(monitor.Check(456, 2000, true));
            Assert.False(monitor.Check(456, 3000, true));
            Assert.False(monitor.Check(456, 3750, true));
            Assert.True(monitor.Check(456, 4000, true));
        }

        [Fact]
        public void Pause_ClearsFailuresAndDoesNotProbeTheTarget()
        {
            int probes = 0;
            var monitor = NewMonitor(probe: pid => { probes++; return new UiWindowSample(UiWindowState.Unresponsive, 42); });
            Assert.False(monitor.Check(123, 0, true));
            Assert.False(monitor.Check(123, 750, false));
            Assert.False(monitor.Check(123, 3000, false));
            Assert.Equal(1, probes);
            Assert.Contains("\"state\":\"paused\"", WatchdogSupervisorLaunch.Serialize(monitor.Snapshot(3000)));
            monitor.Reset(123, 3000);
            Assert.False(monitor.Check(123, 3000, true));
            Assert.True(monitor.Check(123, 4000, true));
        }

        [Fact]
        public void DisabledPolicy_NeverCallsTheNativeProbe()
        {
            var monitor = new UiHangMonitor(false, 1000, 0, pid => throw new InvalidOperationException("must not run"));
            Assert.False(monitor.Check(123, 0, true));
            Assert.False(monitor.Check(123, 10000, true));
            Assert.Contains("\"state\":\"disabled\"", WatchdogSupervisorLaunch.Serialize(monitor.Snapshot(10000)));
        }

        [Fact]
        public void NativeProbeFailure_IsUnavailableRatherThanARecoveryDecision()
        {
            var monitor = NewMonitor(probe: pid => throw new InvalidOperationException("native error"));
            Assert.False(monitor.Check(123, 0, true));
            Assert.False(monitor.Check(123, 10000, true));
            Assert.Contains("\"state\":\"unavailable\"", WatchdogSupervisorLaunch.Serialize(monitor.Snapshot(10000)));
        }

        [Fact]
        public void ResumeReset_InvalidatesAnEarlierUncommittedTimeoutDecision()
        {
            var monitor = NewMonitor();
            Assert.False(monitor.Check(123, 0, true));
            Assert.True(monitor.Check(123, 1000, true, out var decisionRevision));
            monitor.Reset(123, 1000);
            Assert.NotEqual(decisionRevision, monitor.Revision);
            Assert.False(monitor.Check(123, 1000, true));
        }

        private static UiHangMonitor NewMonitor(int grace = 0, Func<int, UiWindowSample> probe = null)
            => new UiHangMonitor(true, 1000, grace, probe ?? (pid => new UiWindowSample(UiWindowState.Unresponsive, 42)));
    }
}
