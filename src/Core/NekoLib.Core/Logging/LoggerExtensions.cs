using System;

namespace NekoLib.Core.Logging
{
    /// <summary>Provides severity-specific conveniences over <see cref="ILogger.Log"/>.</summary>
    public static class LoggerExtensions
    {
        /// <summary>Emits a <see cref="LogLevel.Trace"/> message.</summary>
        /// <param name="logger">Logger that receives the message.</param>
        /// <param name="message">Message text.</param>
        /// <param name="category">Optional application-defined category.</param>
        /// <exception cref="ArgumentNullException"><paramref name="logger"/> is null.</exception>
        public static void Trace(this ILogger logger, string message, string? category = null)
            => Require(logger).Log(LogLevel.Trace, message, category: category);

        /// <summary>Emits a <see cref="LogLevel.Debug"/> message.</summary>
        /// <param name="logger">Logger that receives the message.</param>
        /// <param name="message">Message text.</param>
        /// <param name="category">Optional application-defined category.</param>
        /// <exception cref="ArgumentNullException"><paramref name="logger"/> is null.</exception>
        public static void Debug(this ILogger logger, string message, string? category = null)
            => Require(logger).Log(LogLevel.Debug, message, category: category);

        /// <summary>Emits a <see cref="LogLevel.Info"/> message.</summary>
        /// <param name="logger">Logger that receives the message.</param>
        /// <param name="message">Message text.</param>
        /// <param name="category">Optional application-defined category.</param>
        /// <exception cref="ArgumentNullException"><paramref name="logger"/> is null.</exception>
        public static void Info(this ILogger logger, string message, string? category = null)
            => Require(logger).Log(LogLevel.Info, message, category: category);

        /// <summary>Emits a <see cref="LogLevel.Warn"/> message.</summary>
        /// <param name="logger">Logger that receives the message.</param>
        /// <param name="message">Message text.</param>
        /// <param name="category">Optional application-defined category.</param>
        /// <exception cref="ArgumentNullException"><paramref name="logger"/> is null.</exception>
        public static void Warn(this ILogger logger, string message, string? category = null)
            => Require(logger).Log(LogLevel.Warn, message, category: category);

        /// <summary>Emits a <see cref="LogLevel.Error"/> message.</summary>
        /// <param name="logger">Logger that receives the message.</param>
        /// <param name="message">Message text.</param>
        /// <param name="exception">Optional exception evidence.</param>
        /// <param name="category">Optional application-defined category.</param>
        /// <exception cref="ArgumentNullException"><paramref name="logger"/> is null.</exception>
        public static void Error(
            this ILogger logger,
            string message,
            Exception? exception = null,
            string? category = null)
            => Require(logger).Log(LogLevel.Error, message, exception, category);

        /// <summary>Emits a <see cref="LogLevel.Fatal"/> message.</summary>
        /// <param name="logger">Logger that receives the message.</param>
        /// <param name="message">Message text.</param>
        /// <param name="exception">Optional exception evidence.</param>
        /// <param name="category">Optional application-defined category.</param>
        /// <exception cref="ArgumentNullException"><paramref name="logger"/> is null.</exception>
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
