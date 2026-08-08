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

        /// <summary>Maximum queued events per subscriber. Default 64.</summary>
        public int EventSubscriberQueueCapacity { get; set; } = 64;

        /// <summary>Behavior when one subscriber's event queue is full.</summary>
        public PipeEventQueueOverflowPolicy EventQueueOverflowPolicy { get; set; }
            = PipeEventQueueOverflowPolicy.DropNewest;

        /// <summary>
        /// Operating-system access boundary for the RPC and event servers.
        /// The compatibility default uses platform security and must not be
        /// treated as authorization.
        /// </summary>
        public PipeAccessPolicy AccessPolicy { get; set; } = PipeAccessPolicy.PlatformDefault;

        /// <summary>Maximum framed request/response size. Default 1 MiB.</summary>
        public int MaxMessageBytes { get; set; } = PipeFraming.DefaultMaxBytes;

        public IPipeMetrics? Metrics { get; set; }
    }
}

 


