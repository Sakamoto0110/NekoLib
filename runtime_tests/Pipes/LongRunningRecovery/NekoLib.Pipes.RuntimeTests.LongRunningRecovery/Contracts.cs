#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NekoLib.Pipes;
using NekoLib.RuntimeTests.Harness;

namespace NekoLib.Pipes.RuntimeTests.LongRunningRecovery
{
    /// <summary>
    /// Isolated contracts for the recovery terminal classification.
    /// <para/>
    /// They open no pipe, start no process and write no run artifacts, so they
    /// can be run anywhere in a second and cannot be mistaken for runtime
    /// evidence. They exist because the classification is the part of
    /// <c>kill-server-process</c> that was wrong: the first oracle accepted only
    /// an exception, and a <c>connection_closed</c> response - the terminal
    /// <c>PipeClient</c> actually produces - was read as the request surviving.
    /// A rule that subtle should be assertable without a ten-minute rehearsal.
    /// </summary>
    internal static class Contracts
    {
        public static int Run()
        {
            List<string> failures = new List<string>();
            int total = 0;

            Assert(failures, ref total, "a connection_closed response is an expected terminal",
                TerminalObservation.FromMessage(Failed(TerminalObservation.ConnectionClosedCode)).Classify()
                == KillTerminal.ConnectionClosed);

            Assert(failures, ref total, "a connection_closed response passes the expected-terminal gate",
                TerminalObservation.FromMessage(Failed(TerminalObservation.ConnectionClosedCode)).IsExpected());

            Assert(failures, ref total, "a successful response is classified as a product question",
                TerminalObservation.FromMessage(Ok()).Classify() == KillTerminal.Success);

            Assert(failures, ref total, "a successful response never passes the expected-terminal gate",
                !TerminalObservation.FromMessage(Ok()).IsExpected());

            Assert(failures, ref total, "a failed response carrying another code is not the documented terminal",
                TerminalObservation.FromMessage(Failed("handler_failed")).Classify() == KillTerminal.OtherError);

            Assert(failures, ref total, "another error code does not pass the expected-terminal gate",
                !TerminalObservation.FromMessage(Failed("handler_failed")).IsExpected());

            Assert(failures, ref total, "a cancellation is an expected terminal",
                TerminalObservation.FromException(new OperationCanceledException()).Classify()
                == KillTerminal.Exception);

            Assert(failures, ref total, "a timeout is an expected terminal",
                TerminalObservation.FromException(new TimeoutException()).Classify() == KillTerminal.Exception);

            Assert(failures, ref total, "a broken transport is an expected terminal",
                TerminalObservation.FromException(new IOException("pipe broken")).Classify()
                == KillTerminal.Exception);

            Assert(failures, ref total, "an end-of-stream is an expected terminal through IOException",
                TerminalObservation.FromException(new EndOfStreamException()).Classify() == KillTerminal.Exception);

            Assert(failures, ref total, "a disposed object is an expected terminal",
                TerminalObservation.FromException(new ObjectDisposedException("pipe")).Classify()
                == KillTerminal.Exception);

            // The exclusions matter as much as the inclusions: a classifier that
            // accepted every exception would turn a real defect in the client
            // into a passing recovery check.
            Assert(failures, ref total, "a null reference is not a transport terminal",
                TerminalObservation.FromException(new NullReferenceException()).Classify()
                == KillTerminal.OtherError);

            Assert(failures, ref total, "an invalid response type is not a transport terminal",
                TerminalObservation.FromException(new InvalidOperationException("Invalid pipe response type."))
                    .Classify() == KillTerminal.OtherError);

            Assert(failures, ref total, "no response and no exception is a broken observation",
                new TerminalObservation().Classify() == KillTerminal.Nothing);

            Assert(failures, ref total, "a broken observation never passes the expected-terminal gate",
                !new TerminalObservation().IsExpected());

            Assert(failures, ref total, "a failing outcome is described whole, not merely named",
                TerminalObservation.FromMessage(Ok()).Describe().IndexOf("ok=true", StringComparison.Ordinal) >= 0);

            Assert(failures, ref total, "a connection_closed description carries its code",
                TerminalObservation.FromMessage(Failed(TerminalObservation.ConnectionClosedCode))
                    .Describe().IndexOf(TerminalObservation.ConnectionClosedCode, StringComparison.Ordinal) >= 0);

            Assert(failures, ref total, "an exception description carries its type",
                TerminalObservation.FromException(new TimeoutException("gone"))
                    .Describe().IndexOf("TimeoutException", StringComparison.Ordinal) >= 0);

            Console.Out.WriteLine(
                "E3-PIPE contracts: " +
                (total - failures.Count).ToString(CultureInfo.InvariantCulture) + "/" +
                total.ToString(CultureInfo.InvariantCulture) + " passed");

            foreach (string failure in failures)
                Console.Error.WriteLine("!!  " + failure);

            return failures.Count == 0 ? ExitCodes.Success : ExitCodes.CheckFailed;
        }

        private static void Assert(List<string> failures, ref int total, string claim, bool condition)
        {
            total++;
            if (!condition) failures.Add(claim);
        }

        private static PipeMessage Ok()
        {
            return new PipeMessage
            {
                Id = Guid.NewGuid(),
                Type = "res",
                Name = Ops.Slow,
                Ok = true
            };
        }

        private static PipeMessage Failed(string code)
        {
            return new PipeMessage
            {
                Id = Guid.NewGuid(),
                Type = "res",
                Name = Ops.Slow,
                Ok = false,
                Error = new PipeError
                {
                    Code = code,
                    Message = "produced by an isolated contract, not by a pipe"
                }
            };
        }
    }
}
