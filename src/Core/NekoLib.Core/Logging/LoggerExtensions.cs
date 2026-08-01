using System;

namespace NekoLib.Core.Logging
{
    public static class LoggerExtensions
    {
        public static void Trace(this ILogger logger, string message, string? category = null)
            => Require(logger).Log(LogLevel.Trace, message, category: category);

        public static void Debug(this ILogger logger, string message, string? category = null)
            => Require(logger).Log(LogLevel.Debug, message, category: category);

        public static void Info(this ILogger logger, string message, string? category = null)
            => Require(logger).Log(LogLevel.Info, message, category: category);

        public static void Warn(this ILogger logger, string message, string? category = null)
            => Require(logger).Log(LogLevel.Warn, message, category: category);

        public static void Error(
            this ILogger logger,
            string message,
            Exception? exception = null,
            string? category = null)
            => Require(logger).Log(LogLevel.Error, message, exception, category);

        public static void Fatal(
            this ILogger logger,
            string message,
            Exception? exception = null,
            string? category = null)
            => Require(logger).Log(LogLevel.Fatal, message, exception, category);

        private static ILogger Require(ILogger logger)
            => logger ?? throw new ArgumentNullException(nameof(logger));
    }
}
