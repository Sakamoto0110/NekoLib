using NekoLib.Core.Telemetry;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Telemetry;
using NekoLib.Telemetry;
using System.Linq;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    public sealed class NavigationTelemetryObserverTests
    {
        [Fact]
        public void RequestCompleted_WithAuthenticationTiming_RecordsNavigationMeasurements()
        {
            var hub = new NavigationEventHub();
            var telemetry = new TelemetryPipeline();
            var timing = new NavigationTimingContext();

            using (NavigationTelemetryObserver.Attach(hub, telemetry))
            {
                hub.PublishTrace(new NavigationTraceEvent(
                    NavigationTraceKind.RequestStarted,
                    "runtime-1",
                    trigger: NavigationTraceTrigger.Navigate,
                    requestId: "request-1",
                    fromPage: "Home",
                    targetPage: "Catalog"));

                hub.PublishStarted(new NavigationStartedEvent(
                    null,
                    typeof(object),
                    "Catalog",
                    NavigationArgs.Default().WithTiming(timing),
                    System.DateTime.UtcNow,
                    "runtime-1",
                    "request-1",
                    NavigationTraceTrigger.Navigate));

                timing.AuthenticationCompleted();

                hub.PublishTrace(new NavigationTraceEvent(
                    NavigationTraceKind.RequestCompleted,
                    "runtime-1",
                    outcome: NavigationTraceOutcome.Succeeded,
                    trigger: NavigationTraceTrigger.Navigate,
                    requestId: "request-1",
                    targetPage: "Catalog",
                    effectiveLoadMode: "LoadBeforeShow",
                    elapsedMilliseconds: 120));
            }

            var operation = Assert.Single(telemetry.GetRecentOperations(10));
            Assert.Equal("Navigation", operation.Module);
            Assert.Equal("page_switch", operation.Name);
            Assert.Equal("request-1", operation.OperationId);
            Assert.Equal(TelemetryOutcome.Succeeded, operation.Outcome);
            Assert.Equal(120, operation.Measurements["page_switch.total_ms"]);
            Assert.True(operation.Measurements.ContainsKey("page_switch.time_to_authenticated_ms"));
            Assert.True(operation.Measurements.ContainsKey("page_switch.post_auth_to_ready_ms"));
            Assert.Contains(operation.Checkpoints, checkpoint => checkpoint.Name == "page_switch_started");
            Assert.Contains(operation.Checkpoints, checkpoint => checkpoint.Name == "authentication_completed");
            Assert.Contains(operation.Checkpoints, checkpoint => checkpoint.Name == "page_ready");
        }

        [Fact]
        public void RequestCompleted_WhenFailed_DoesNotClaimPageReady()
        {
            var hub = new NavigationEventHub();
            var telemetry = new TelemetryPipeline();

            using (NavigationTelemetryObserver.Attach(hub, telemetry))
            {
                hub.PublishTrace(new NavigationTraceEvent(
                    NavigationTraceKind.RequestStarted,
                    "runtime-1",
                    requestId: "request-1",
                    targetPage: "Catalog"));

                hub.PublishTrace(new NavigationTraceEvent(
                    NavigationTraceKind.RequestCompleted,
                    "runtime-1",
                    outcome: NavigationTraceOutcome.Failed,
                    requestId: "request-1",
                    targetPage: "Catalog",
                    elapsedMilliseconds: 50));
            }

            var operation = Assert.Single(telemetry.GetRecentOperations(10));
            Assert.Equal(TelemetryOutcome.Failed, operation.Outcome);
            Assert.DoesNotContain(operation.Checkpoints, checkpoint => checkpoint.Name == "page_ready");
            Assert.False(operation.Measurements.ContainsKey("page_switch.time_to_authenticated_ms"));
        }
    }
}
