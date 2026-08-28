using System;
namespace NekoLib.Pipes
{
    /// <summary>
    /// Configures a <see cref="PipeServer"/> and its optional event hub. The
    /// server validates and captures every value at construction.
    /// </summary>
    public sealed class PipeServerOptions
    {
        /// <summary>Initializes options with the documented compatibility defaults.</summary>
        public PipeServerOptions()
        {
        }

        /// <summary>Gets or sets the nonblank RPC pipe base name. The default is <c>nekolib.pipe</c>.</summary>
        public string PipeName { get; set; } = "nekolib.pipe";

        /// <summary>Gets or sets the positive maximum number of concurrent RPC clients. The default is 16.</summary>
        public int MaxClients { get; set; } = 16;

        /// <summary>
        /// Gets or sets the positive idle timeout while an established client is
        /// waiting to send its next request. Handler execution and response writing
        /// are outside this budget. The default is five minutes.
        /// </summary>
        public TimeSpan ClientIdleTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets whether <see cref="PipeServer.Start"/> also creates and
        /// starts the <c>.events</c> hub. The default is <c>true</c>.
        /// </summary>
        public bool EnableEvents { get; set; } = true;

        /// <summary>Gets or sets the positive maximum number of event subscribers. The default is 16.</summary>
        public int MaxEventSubscribers { get; set; } = 16;

        /// <summary>Gets or sets the positive maximum queued events per subscriber. The default is 64.</summary>
        public int EventSubscriberQueueCapacity { get; set; } = 64;

        /// <summary>Gets or sets the supported behavior when one subscriber's event queue is full.</summary>
        public PipeEventQueueOverflowPolicy EventQueueOverflowPolicy { get; set; }
            = PipeEventQueueOverflowPolicy.DropNewest;

        /// <summary>
        /// Gets or sets the operating-system access boundary for the RPC and event servers.
        /// The compatibility default uses platform security and must not be
        /// treated as authorization.
        /// </summary>
        public PipeAccessPolicy AccessPolicy { get; set; } = PipeAccessPolicy.PlatformDefault;

        /// <summary>
        /// Gets or sets the positive maximum serialized RPC request or response
        /// frame size in bytes. Events retain their fixed 1 MiB limit. The default
        /// is 1 MiB.
        /// </summary>
        public int MaxMessageBytes { get; set; } = PipeFraming.DefaultMaxBytes;

        /// <summary>
        /// Gets or sets the optional synchronous observational metrics sink shared
        /// by the RPC server and its event hub. The sink is not owned or disposed.
        /// Null creates an instance-owned <see cref="SimplePipeMetrics"/> collector.
        /// </summary>
        public IPipeMetrics? Metrics { get; set; }
    }
}

 


