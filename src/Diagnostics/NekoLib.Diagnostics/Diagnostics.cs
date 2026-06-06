using NekoLib.Diagnostics.Contracts;
using NekoLib.Diagnostics.Sinks;
using System;
using System.Collections.Generic;

namespace NekoLib.Diagnostics
{
    public sealed class Diagnostics : IDiagnosticsContext
    {
        public ILogger Logger { get; }
        public ITelemetrySink Telemetry { get; }

        public static readonly IDiagnosticsContext Null =
            new Diagnostics(NullLogger.Instance, NullTelemetrySink.Instance);

        public Diagnostics(ILogger logger, ITelemetrySink telemetry)
        {
            Logger = logger ?? NullLogger.Instance;
            Telemetry = telemetry ?? NullTelemetrySink.Instance;
        }
    }

}
