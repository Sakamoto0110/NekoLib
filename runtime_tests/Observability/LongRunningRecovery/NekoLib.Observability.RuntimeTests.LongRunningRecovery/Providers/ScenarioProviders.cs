#nullable enable
using System;
using System.Globalization;
using System.Threading;

namespace NekoLib.Observability.RuntimeTests.LongRunningRecovery.Providers
{
    /// <summary>
    /// The four shapes a state provider can have, as the suite lists them:
    /// returns normally, returns null, throws, and exceeds the snapshot budget.
    /// <para/>
    /// All four are scenario-owned callbacks handed to the public
    /// <c>RegisterStateProvider</c>. Inspection is not modified to produce them,
    /// and nothing here registers an action.
    /// </summary>
    internal sealed class ScenarioStateProvider
    {
        private long _calls;
        private int _armed = 1;

        private ScenarioStateProvider(string key, Kind behaviour, TimeSpan delay)
        {
            Key = key;
            Behaviour = behaviour;
            Delay = delay;
        }

        internal enum Kind
        {
            Healthy,
            Null,
            Throws,
            Slow
        }

        public string Key { get; }
        public Kind Behaviour { get; }
        public TimeSpan Delay { get; }

        public long Calls => Interlocked.Read(ref _calls);

        /// <summary>
        /// Disarming makes a misbehaving provider healthy again, which is what
        /// recovery from an injected provider fault looks like.
        /// </summary>
        public bool Armed
        {
            get { return Volatile.Read(ref _armed) != 0; }
            set { Volatile.Write(ref _armed, value ? 1 : 0); }
        }

        public static ScenarioStateProvider Healthy(string key) =>
            new ScenarioStateProvider(key, Kind.Healthy, TimeSpan.Zero);

        public static ScenarioStateProvider ReturnsNull(string key) =>
            new ScenarioStateProvider(key, Kind.Null, TimeSpan.Zero);

        public static ScenarioStateProvider Throws(string key) =>
            new ScenarioStateProvider(key, Kind.Throws, TimeSpan.Zero);

        /// <summary>
        /// Exceeds the snapshot budget by sleeping. The delay is kept short on
        /// purpose: <c>CaptureSnapshot</c> abandons the wait but not the thread
        /// running the provider, so a long sleep registered for the whole run
        /// would show up as thread growth in this scenario's own leak checks.
        /// </summary>
        public static ScenarioStateProvider Slow(string key, TimeSpan delay) =>
            new ScenarioStateProvider(key, Kind.Slow, delay);

        /// <summary>The callback handed to <c>RegisterStateProvider</c>.</summary>
        public object Snapshot()
        {
            long call = Interlocked.Increment(ref _calls);

            if (!Armed)
                return "healthy after recovery, call " + call.ToString(CultureInfo.InvariantCulture);

            switch (Behaviour)
            {
                case Kind.Null:
                    return null!;

                case Kind.Throws:
                    throw new InvalidOperationException(
                        "scenario provider '" + Key + "' failed on call " +
                        call.ToString(CultureInfo.InvariantCulture));

                case Kind.Slow:
                    Thread.Sleep(Delay);
                    return "late, call " + call.ToString(CultureInfo.InvariantCulture);

                default:
                    return "healthy, call " + call.ToString(CultureInfo.InvariantCulture);
            }
        }
    }

    /// <summary>The markers Inspection substitutes when a provider misbehaves.</summary>
    internal static class ProviderMarkers
    {
        public const string Null = "<null>";
        public const string TimedOut = "<snapshot timed out>";
        public const string ThrewPrefix = "<snapshot threw:";

        public static bool IsThrown(object? value)
        {
            string? text = value as string;
            return text != null && text.StartsWith(ThrewPrefix, StringComparison.Ordinal);
        }
    }
}
