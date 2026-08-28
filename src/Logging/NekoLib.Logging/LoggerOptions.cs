using NekoLib.Core.Logging;

namespace NekoLib.Logging
{
    /// <summary>
    /// Configures filtering, bounded recent retention, and sink ownership for a
    /// <see cref="Logger"/>. Values are captured at construction.
    /// </summary>
    public sealed class LoggerOptions
    {
        /// <summary>Gets or sets the minimum accepted severity. The default is <see cref="LogLevel.Info"/>.</summary>
        public LogLevel MinimumLevel { get; set; } = LogLevel.Info;
        /// <summary>Gets or sets the recent-entry capacity. The default is 1024 and the value must be at least 1.</summary>
        public int RecentEntryCapacity { get; set; } = 1024;
        /// <summary>Gets or sets whether disposing the logger also disposes disposable sinks. The default is <c>true</c>.</summary>
        public bool DisposeSinks { get; set; } = true;
    }
}
