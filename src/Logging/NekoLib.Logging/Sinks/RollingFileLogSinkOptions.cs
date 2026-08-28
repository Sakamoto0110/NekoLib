using System.Text;

namespace NekoLib.Logging.Sinks
{
    /// <summary>
    /// Configures one <see cref="RollingFileLogSink"/>. Values are captured and
    /// the path is normalized when the sink is constructed.
    /// </summary>
    public sealed class RollingFileLogSinkOptions
    {
        /// <summary>Gets or sets the required live log path. Relative paths use the construction-time working directory.</summary>
        public string FilePath { get; set; } = string.Empty;
        /// <summary>Gets or sets the pre-write rotation threshold in encoded bytes. The default is 4 MiB and the minimum is 1024.</summary>
        public long MaximumFileBytes { get; set; } = 4 * 1024 * 1024;
        /// <summary>Gets or sets the number of archives retained in addition to the live file. The default is 4 and the minimum is 1.</summary>
        public int RetainedFileCount { get; set; } = 4;
        /// <summary>Gets or sets the line encoding. The default is UTF-8 without a byte-order mark and the value cannot be <c>null</c>.</summary>
        public Encoding Encoding { get; set; } = new UTF8Encoding(false);
    }
}
