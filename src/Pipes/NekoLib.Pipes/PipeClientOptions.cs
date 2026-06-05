using System;
namespace NekoLib.Pipes
{
    public sealed class PipeClientOptions
    {
        public string PipeName { get; set; } = "nekolib.pipe";

        /// <summary>
        /// Connection timeout.
        /// </summary>
        public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(3);

        /// <summary>
        /// Request timeout (send + response).
        /// </summary>
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>Maximum framed request/response size. Default 1 MiB.</summary>
        public int MaxMessageBytes { get; set; } = PipeFraming.DefaultMaxBytes;

        /// <summary>
        /// Optional metrics hook.
        /// </summary>
        public IPipeMetrics? Metrics { get; set; }
    }

}


