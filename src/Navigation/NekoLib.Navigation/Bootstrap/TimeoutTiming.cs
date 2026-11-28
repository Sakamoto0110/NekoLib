using System;
using System.Diagnostics;

namespace NekoLib.Navigation.Bootstrap
{
    internal static class TimeoutTiming
    {
        public static TimeSpan GetElapsedTime(long startedTimestamp, long currentTimestamp)
            => TimeSpan.FromTicks((long)((currentTimestamp - startedTimestamp) *
                (TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency)));

        // The native adapters and Task.Delay on .NET Framework take an Int32
        // interval. Long deadlines are scheduled in slices and rechecked on wake.
        public static int GetTimerIntervalMilliseconds(TimeSpan remaining)
            => (int)Math.Min(int.MaxValue, Math.Ceiling(remaining.TotalMilliseconds));
    }
}
