using NekoLib.Core.Logging;

namespace NekoLib.Logging
{
    public sealed class LoggerOptions
    {
        public LogLevel MinimumLevel { get; set; } = LogLevel.Info;
        public int RecentEntryCapacity { get; set; } = 1024;
        public bool DisposeSinks { get; set; } = true;
    }
}
