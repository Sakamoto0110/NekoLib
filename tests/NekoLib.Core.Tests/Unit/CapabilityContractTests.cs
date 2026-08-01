using NekoLib.Core.Inspection;
using NekoLib.Core.Logging;
using NekoLib.Core.Telemetry;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NekoLib.Core.Tests.Unit
{
    public sealed class CapabilityContractTests
    {
        [Fact]
        public void LogEntry_Construction_PreservesStructuredValues()
        {
            var timestamp = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
            var exception = new InvalidOperationException("boom");

            var entry = new LogEntry(
                timestamp,
                LogLevel.Error,
                "failed",
                exception,
                "Navigation");

            Assert.Equal(timestamp, entry.TimestampUtc);
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.Equal("Navigation", entry.Category);
            Assert.Equal("failed", entry.Message);
            Assert.Same(exception, entry.Exception);
        }

        [Fact]
        public void LogEntry_BoxedAsObject_UsesStructuredFormatting()
        {
            object entry = new LogEntry(
                new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                LogLevel.Info,
                "ready",
                category: "Application");

            var formatted = entry.ToString();

            Assert.Contains("Info", formatted);
            Assert.Contains("[Application]", formatted);
            Assert.Contains("ready", formatted);
        }

        [Fact]
        public void TelemetryOperation_Type_IsIndependentFromLogEntry()
        {
            Assert.False(typeof(LogEntry).IsAssignableFrom(typeof(TelemetryOperation)));
        }

        [Fact]
        public void TelemetryOperation_Construction_PreservesOperationModel()
        {
            var checkpoint = new TelemetryCheckpoint("authenticated", TimeSpan.FromMilliseconds(4));
            var operation = new TelemetryOperation(
                DateTime.UtcNow,
                "Navigation",
                "page_switch",
                "operation",
                "parent",
                TelemetryOutcome.Succeeded,
                TimeSpan.FromMilliseconds(10),
                new[] { checkpoint },
                new Dictionary<string, object> { ["target"] = "Catalog" },
                new Dictionary<string, double> { ["page_switch.total_ms"] = 10 });

            Assert.Equal("Navigation", operation.Module);
            Assert.Equal("operation", operation.OperationId);
            Assert.Equal("parent", operation.ParentOperationId);
            Assert.Same(checkpoint, operation.Checkpoints.Single());
        }

        [Fact]
        public void InspectionSnapshotSource_Surface_DoesNotExposeActions()
        {
            var methodNames = typeof(IInspectionSnapshotSource)
                .GetMethods()
                .Select(method => method.Name)
                .ToArray();

            Assert.Equal(new[] { "CaptureSnapshot" }, methodNames);
        }

        [Fact]
        public void NullImplementations_DefaultOperations_AreSafe()
        {
            NullLogger.Instance.Info("ignored");
            var telemetry = NullTelemetry.Instance.StartOperation("Test", "ignored");
            telemetry.Checkpoint("checkpoint");
            telemetry.Complete(TelemetryOutcome.Succeeded);
            NullInspection.Instance.Record("Test", "ignored", () =>
                throw new InvalidOperationException("must not execute"));

            Assert.Empty(NullLogger.Instance.GetRecentEntries(1));
            Assert.Empty(NullTelemetry.Instance.GetRecentOperations(1));
            Assert.False(NullInspection.Instance.IsEnabled);
        }
    }
}
