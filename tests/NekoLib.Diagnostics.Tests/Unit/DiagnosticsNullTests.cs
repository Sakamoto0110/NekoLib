using System;
using NekoLib.Core.Diagnostics;
using Xunit;

namespace NekoLib.Diagnostics.Tests.Unit
{
    public sealed class DiagnosticsNullTests
    {
        [Fact]
        public void NullContext_ProvidesSafeLoggerAndTelemetry()
        {
            var ctx = NekoLib.Logger.Diagnostics.Null;

            Assert.NotNull(ctx.Logger);
            Assert.NotNull(ctx.Telemetry);

            ctx.Logger.Info("ignored");
            ctx.Logger.Error("ignored", new InvalidOperationException("ignored"));
            ctx.Telemetry.Track(new TelemetryEvent(DateTime.UtcNow, "ignored"));
        }
    }
}
