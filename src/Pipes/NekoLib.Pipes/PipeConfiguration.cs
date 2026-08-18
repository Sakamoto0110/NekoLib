using System;

namespace NekoLib.Pipes
{
    internal static class PipeConfiguration
    {
        public static string RequirePipeName(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Pipe name must not be blank.", parameterName);

            return value;
        }

        public static int RequirePositive(int value, string parameterName)
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(parameterName, value, "Value must be positive.");

            return value;
        }

        public static TimeSpan RequirePositiveTimeout(TimeSpan value, string parameterName)
        {
            if (value <= TimeSpan.Zero || value.TotalMilliseconds > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Timeout must be positive and no greater than Int32.MaxValue milliseconds.");
            }

            return value;
        }

        public static TimeSpan RequireNonNegativeDelay(TimeSpan value, string parameterName)
        {
            if (value < TimeSpan.Zero || value.TotalMilliseconds > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Delay must be non-negative and no greater than Int32.MaxValue milliseconds.");
            }

            return value;
        }

        public static int ToTimeoutMilliseconds(TimeSpan value)
            => Math.Max(1, checked((int)Math.Ceiling(value.TotalMilliseconds)));
    }
}
