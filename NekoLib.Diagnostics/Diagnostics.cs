 using System;
using System.Collections.Generic;

namespace NekoLib.Diagnostics
{
    public sealed class Diagnostics : IDiagnostics
    {
        public ILogger Logger { get; }
        public ITelemetrySink Telemetry { get; }

        public static readonly IDiagnostics Null =
            new Diagnostics(null,null);

        public Diagnostics(ILogger logger, ITelemetrySink telemetry)
        {
            Logger = logger;
            Telemetry = telemetry;
        }
    }

}
