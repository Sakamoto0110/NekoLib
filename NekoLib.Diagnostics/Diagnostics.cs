using NekoLib.Diagnostics.Contracts;
using System;
using System.Collections.Generic;

namespace NekoLib.Diagnostics
{
    public sealed class Diagnostics : IDiagnosticsContext
    {
        public ILogger Logger { get; }
        public ITelemetrySink Telemetry { get; }

        public static readonly IDiagnosticsContext Null =
            new Diagnostics(null,null);

        public Diagnostics(ILogger logger, ITelemetrySink telemetry)
        {
            Logger = logger;
            Telemetry = telemetry;
        }
    }

}
