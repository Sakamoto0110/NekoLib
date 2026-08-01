using System;
using System.Collections.Generic;
using NekoLib.Core.Diagnostics;
using Xunit;

namespace NekoLib.Core.Tests.Unit
{
    public sealed class DiagnosticsContractTests
    {
        [Fact]
        public void TelemetryEvent_Type_IsIndependentFromLogEntry()
        {
            Assert.False(
                typeof(LogEntry).IsAssignableFrom(typeof(TelemetryEvent)));
        }

        [Fact]
        public void TelemetryEvent_Construction_PreservesTelemetryValues()
        {
            var timestamp = new DateTime(
                2026,
                8,
                1,
                12,
                30,
                0,
                DateTimeKind.Utc);
            var duration = TimeSpan.FromMilliseconds(125);
            var data = new Dictionary<string, object>
            {
                ["page"] = "Catalog"
            };

            var telemetry = new TelemetryEvent(
                timestamp,
                "navigation.page_switch",
                duration,
                data);

            Assert.Equal(timestamp, telemetry.TimestampUtc);
            Assert.Equal("navigation.page_switch", telemetry.Name);
            Assert.Equal(duration, telemetry.Duration);
            Assert.Same(data, telemetry.Data);
        }

        [Fact]
        public void ToString_WhenViewedAsObject_UsesLogEntryFormatting()
        {
            var timestamp = new DateTime(
                2026,
                8,
                1,
                12,
                30,
                0,
                DateTimeKind.Utc);
            var exception = new InvalidOperationException("failed");
            var entry = new LogEntry(
                timestamp,
                LogLevel.Error,
                "navigation",
                "Page switch failed",
                exception);
            object boxed = entry;

            var text = boxed.ToString();

            Assert.Contains(timestamp.ToString("O"), text);
            Assert.Contains("Error", text);
            Assert.Contains("Page switch failed", text);
            Assert.Contains("InvalidOperationException", text);
        }
    }
}
