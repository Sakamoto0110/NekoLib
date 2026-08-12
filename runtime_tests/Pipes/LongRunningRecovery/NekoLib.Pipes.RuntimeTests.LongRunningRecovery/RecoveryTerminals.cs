#nullable enable
using System;
using System.Globalization;
using System.IO;
using NekoLib.Pipes;

namespace NekoLib.Pipes.RuntimeTests.LongRunningRecovery
{
    /// <summary>
    /// How a request that was in flight when its server died actually ended.
    /// <para/>
    /// The distinction this type exists to make cost a whole ten-minute
    /// rehearsal. The first version of <c>kill-server-process</c> captured only
    /// the exception and treated "no exception" as "the request survived". It
    /// does not: <c>PipeClient.SendAsync</c> reads the response frame with
    /// <c>TryReadAsync</c> and, when the pipe closes first, substitutes a
    /// <c>PipeMessage</c> carrying <c>Ok = false</c> and
    /// <c>Error.Code = "connection_closed"</c> rather than throwing. That is a
    /// perfectly good terminal, and the oracle was calling it a survival.
    /// <para/>
    /// So both halves are preserved and classified together, and the only
    /// outcome that still fails is a genuinely successful response — which would
    /// be a question about <c>NekoLib.Pipes</c> rather than about this scenario.
    /// </summary>
    internal enum KillTerminal
    {
        /// <summary>Neither a response nor an exception; the observation is broken.</summary>
        Nothing = 0,

        /// <summary>A transport or cancellation exception. Expected.</summary>
        Exception = 1,

        /// <summary>A response reporting <c>connection_closed</c>. Expected.</summary>
        ConnectionClosed = 2,

        /// <summary>A failed response carrying some other code. Not the documented terminal.</summary>
        OtherError = 3,

        /// <summary>A successful response from a server that was killed. A product question.</summary>
        Success = 4
    }

    /// <summary>
    /// One in-flight call's complete outcome: the response if one came back, the
    /// exception if one was thrown, and never a judgement about either until
    /// <see cref="Classify"/> is asked.
    /// </summary>
    internal sealed class TerminalObservation
    {
        public PipeMessage? Message;
        public Exception? Failure;

        public static TerminalObservation FromMessage(PipeMessage message) =>
            new TerminalObservation { Message = message };

        public static TerminalObservation FromException(Exception failure) =>
            new TerminalObservation { Failure = failure };

        /// <summary>
        /// The error code the response carried, or an empty string when there
        /// was no response or no error on it.
        /// </summary>
        public string ErrorCode =>
            Message == null || Message.Error == null || Message.Error.Code == null
                ? string.Empty
                : Message.Error.Code;

        public KillTerminal Classify()
        {
            if (Failure != null)
                return IsTransportOrCancellation(Failure) ? KillTerminal.Exception : KillTerminal.OtherError;

            if (Message == null) return KillTerminal.Nothing;
            if (Message.Ok) return KillTerminal.Success;

            return string.Equals(ErrorCode, ConnectionClosedCode, StringComparison.Ordinal)
                ? KillTerminal.ConnectionClosed
                : KillTerminal.OtherError;
        }

        public bool IsExpected()
        {
            KillTerminal terminal = Classify();
            return terminal == KillTerminal.Exception || terminal == KillTerminal.ConnectionClosed;
        }

        /// <summary>
        /// Everything the artifact should carry about this outcome, so a later
        /// reader never has to reconstruct it from a verdict. A failing case in
        /// particular has to be recorded whole: that is the evidence a product
        /// finding would rest on.
        /// </summary>
        public string Describe()
        {
            if (Failure != null)
                return Failure.GetType().Name + ": " + Flatten(Failure.Message);

            if (Message == null) return "no response and no exception";

            string text = "response ok=" +
                          Message.Ok.ToString(CultureInfo.InvariantCulture).ToLowerInvariant();

            if (ErrorCode.Length > 0) text += " code=" + ErrorCode;

            if (Message.Error != null && Message.Error.Message != null && Message.Error.Message.Length > 0)
                text += " message=" + Flatten(Message.Error.Message);

            string? payload = Payload.Text(Message);
            if (payload != null && payload.Length > 0)
                text += " payload=" + Flatten(payload.Length > 120 ? payload.Substring(0, 120) + "..." : payload);

            return text;
        }

        /// <summary>
        /// The code <c>PipeClient</c> substitutes when the pipe closes before a
        /// response frame arrives.
        /// </summary>
        public const string ConnectionClosedCode = "connection_closed";

        /// <summary>
        /// Whether an exception is the kind a broken transport or a withdrawn
        /// caller legitimately produces.
        /// <para/>
        /// Deliberately a list rather than "anything that is not a CheckFailure":
        /// a <c>NullReferenceException</c> out of the client would be a finding,
        /// not a terminal, and a classifier that accepted everything would hide
        /// exactly that. <c>InvalidOperationException</c> is excluded for the
        /// same reason — <c>PipeClient</c> raises it for an invalid response
        /// type, which is a correlation defect rather than a dead peer.
        /// <para/>
        /// <c>EndOfStreamException</c> needs no entry of its own: it derives
        /// from <see cref="IOException"/>.
        /// </summary>
        public static bool IsTransportOrCancellation(Exception failure)
        {
            return failure is OperationCanceledException ||
                   failure is TimeoutException ||
                   failure is IOException ||
                   failure is ObjectDisposedException;
        }

        private static string Flatten(string text) =>
            (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
    }
}
