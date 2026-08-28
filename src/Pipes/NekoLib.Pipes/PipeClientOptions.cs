using System;
namespace NekoLib.Pipes
{
    /// <summary>
    /// Configures a <see cref="PipeClient"/>. The client validates and captures
    /// these values at construction, so later mutations do not affect it.
    /// </summary>
    public sealed class PipeClientOptions
    {
        /// <summary>Initializes options with the documented compatibility defaults.</summary>
        public PipeClientOptions()
        {
        }

        /// <summary>Gets or sets the nonblank local RPC pipe name. The default is <c>nekolib.pipe</c>.</summary>
        public string PipeName { get; set; } = "nekolib.pipe";

        /// <summary>
        /// Gets or sets the connection-establishment timeout. The value must be
        /// positive and no greater than <see cref="int.MaxValue"/> milliseconds.
        /// The default is three seconds.
        /// </summary>
        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(3);

        /// <summary>
        /// Gets or sets the timeout for request writing plus response reading,
        /// starting after connection. The value has the same positive
        /// <see cref="int.MaxValue"/>-millisecond bound as <see cref="ConnectTimeout"/>.
        /// The default is five seconds.
        /// </summary>
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets or sets the positive maximum serialized request or response frame
        /// size in bytes. The default is 1 MiB.
        /// </summary>
        public int MaxMessageBytes { get; set; } = PipeFraming.DefaultMaxBytes;

        /// <summary>
        /// Gets or sets the optional synchronous observational metrics sink.
        /// Transport-owned callbacks are failure-isolated; the sink is not owned
        /// or disposed by the client. Null selects no-op metrics.
        /// </summary>
        public IPipeMetrics? Metrics { get; set; }
    }

}


