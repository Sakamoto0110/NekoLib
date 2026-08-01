using System.Text;

namespace NekoLib.Logging.Sinks
{
    public sealed class RollingFileLogSinkOptions
    {
        public string FilePath { get; set; } = string.Empty;
        public long MaximumFileBytes { get; set; } = 4 * 1024 * 1024;
        public int RetainedFileCount { get; set; } = 4;
        public Encoding Encoding { get; set; } = new UTF8Encoding(false);
    }
}
