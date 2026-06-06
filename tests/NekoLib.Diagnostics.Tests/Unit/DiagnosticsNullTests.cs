using System;
using NekoLib.Diagnostics.Contracts;
using Xunit;

namespace NekoLib.Diagnostics.Tests.Unit
{
    public sealed class DiagnosticsNullTests
    {
        [Fact]
        public void NullContext_ProvidesSafeLoggerAndTelemetry()
        {
            var ctx = global::NekoLib.Diagnostics.Diagnostics.Null;

            Assert.NotNull(ctx.Logger);
            Assert.NotNull(ctx.Telemetry);

            ctx.Logger.Info("ignored");
            ctx.Logger.Error("ignored", new InvalidOperationException("ignored"));
            ctx.Telemetry.Track(new TelemetryEvent(DateTime.UtcNow, "ignored"));
        }
    }
}
