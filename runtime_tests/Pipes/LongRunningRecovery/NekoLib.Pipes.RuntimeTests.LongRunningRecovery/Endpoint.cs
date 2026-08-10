#nullable enable
using System;
using System.Globalization;
using System.IO;
using System.Text;
using NekoLib.Pipes;

namespace NekoLib.Pipes.RuntimeTests.LongRunningRecovery
{
    /// <summary>
    /// The pipe names one run owns, and nothing else's.
    /// <para/>
    /// Every name carries the campaign id, so two runs on one machine can never
    /// collide and a leftover endpoint is always attributable to the run that
    /// created it. That matters more here than in most scenarios: a named pipe
    /// is a machine-wide resource, and "the pipe could be rebound afterwards" is
    /// one of the things this scenario has to assert.
    /// </summary>
    internal static class Endpoint
    {
        public static string ForCampaign(string campaignId)
        {
            // Pipe names may not contain a backslash and are best kept short;
            // the campaign id is already unique per run.
            StringBuilder name = new StringBuilder("nekolib.e3pipe.");
            foreach (char c in campaignId)
                if (char.IsLetterOrDigit(c)) name.Append(c);

            return name.ToString();
        }

        /// <summary>The hub's pipe, which <c>PipeEventHub</c> derives by suffix.</summary>
        public static string EventsFor(string basePipeName) => basePipeName + ".events";

        /// <summary>The Win32 path a raw peer opens, bypassing the client API.</summary>
        public static string RawPath(string pipeName) => @"\\.\pipe\" + pipeName;

        /// <summary>
        /// True when a listener is accepting on this pipe.
        /// <para/>
        /// <c>File.Exists</c> against the pipe filesystem is the cheapest honest
        /// probe: it answers whether the name is currently bound without
        /// connecting, which is what "the endpoint was released" and "the
        /// endpoint can be rebound" both need to ask.
        /// </summary>
        public static bool IsBound(string pipeName) => File.Exists(RawPath(pipeName));
    }

    /// <summary>
    /// Reads a response payload back as text.
    /// <para/>
    /// This shim exists because <c>PipeMessage.Data</c> is the one part of the
    /// Pipes surface that differs by target: <c>JsonElement?</c> on modern .NET
    /// and <c>JToken?</c> on <c>net481</c>, because the module serializes with
    /// <c>System.Text.Json</c> on one and <c>Newtonsoft.Json</c> on the other.
    /// Sending is target-agnostic - <c>SendAsync</c> takes <c>object?</c> - so
    /// only reading needs conditioning, and confining it to one method keeps
    /// every check target-neutral.
    /// </summary>
    internal static class Payload
    {
        /// <summary>An echo payload: a marker string of a requested size.</summary>
        public static string Make(string marker, int totalBytes)
        {
            if (totalBytes <= marker.Length) return marker;

            StringBuilder text = new StringBuilder(totalBytes);
            text.Append(marker);
            while (text.Length < totalBytes) text.Append('x');
            return text.ToString(0, totalBytes);
        }

        /// <summary>The payload's text, or null when the message carried none.</summary>
        public static string? Text(PipeMessage message)
        {
            if (message == null || message.Data == null) return null;

#if NET9_0_OR_GREATER
            System.Text.Json.JsonElement element = message.Data.Value;
            return element.ValueKind == System.Text.Json.JsonValueKind.String
                ? element.GetString()
                : element.ToString();
#else
            // ToObject rather than Value<string>(): the latter is the indexing
            // overload and needs a key, which a scalar JValue does not have.
            return message.Data.Type == Newtonsoft.Json.Linq.JTokenType.String
                ? message.Data.ToObject<string>()
                : message.Data.ToString();
#endif
        }

        public static int Length(PipeMessage message)
        {
            string? text = Text(message);
            return text == null ? 0 : text.Length;
        }

        public static string Describe(PipeMessage message)
        {
            if (message == null) return "<null message>";

            string ok = message.Ok ? "ok" : "error";
            string error = message.Error == null
                ? string.Empty
                : " " + message.Error.Code + ": " + Flatten(message.Error.Message);

            return ok + " name=" + message.Name + " bytes=" +
                   Length(message).ToString(CultureInfo.InvariantCulture) + error;
        }

        public static string Flatten(string? text) =>
            (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
    }

    /// <summary>The operations the scenario's server maps.</summary>
    internal static class Ops
    {
        /// <summary>Returns the payload it was given.</summary>
        public const string Echo = "echo";

        /// <summary>Waits for a requested number of milliseconds, then echoes.</summary>
        public const string Slow = "slow";

        /// <summary>Always throws, to exercise the handler-failure contract.</summary>
        public const string Boom = "boom";

        /// <summary>Returns a response of a requested size, to cross the outbound limit.</summary>
        public const string Fat = "fat";

        /// <summary>Publishes N events through the hub.</summary>
        public const string Publish = "publish";

        /// <summary>Returns the server's metrics snapshot as text.</summary>
        public const string Stats = "stats";

        /// <summary>Asks the server child to end itself cleanly.</summary>
        public const string Shutdown = "shutdown";

        /// <summary>A name deliberately never mapped, for the not-found contract.</summary>
        public const string Missing = "no-such-operation";
    }
}
