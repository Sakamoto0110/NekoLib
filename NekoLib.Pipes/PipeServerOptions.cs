using System;
namespace NekoLib.Pipes
{
    public sealed class PipeServerOptions
    {
        public string PipeName { get; set; } = "nekolib.pipe";
        public int MaxClients { get; set; } = 16;
        public TimeSpan ClientIdleTimeout { get; set; } = TimeSpan.FromMinutes(5);

        public bool EnableEvents { get; set; } = true;
        public int MaxEventSubscribers { get; set; } = 16;

        public IPipeMetrics? Metrics { get; set; }
    }
}

 


