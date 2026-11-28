using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;

namespace NekoLib.Watchdog
{
    /// <summary>Outcome of seeking acknowledgement for one deadline-bound stop request.</summary>
    public enum WatchdogStopResult
    {
        /// <summary>The caller could not verify the request's disposition.</summary>
        Unknown,
        /// <summary>The supervisor claimed the request before its deadline and owns shutdown.</summary>
        Accepted,
        /// <summary>The request expired without claiming shutdown. It cannot execute later.</summary>
        Expired,
        /// <summary>The supervisor declined the request or does not support this command.</summary>
        Rejected
    }

    internal sealed class WatchdogStopRequest
    {
        internal static readonly TimeSpan Budget = TimeSpan.FromSeconds(60);
        internal static readonly TimeSpan ReceiptBudget = TimeSpan.FromSeconds(3);
        private readonly TaskCompletionSource<WatchdogStopResult> _decision =
            new TaskCompletionSource<WatchdogStopResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<bool> ResponseAttempted =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        internal string Token { get; }
        internal long Deadline { get; }
        internal Task<WatchdogStopResult> Decision => _decision.Task;

        private WatchdogStopRequest(string token, long deadline)
        {
            Token = token;
            Deadline = deadline;
        }

        // The local client and supervisor share Windows' monotonic performance counter.
        internal static string NewToken(TimeSpan budget) =>
            Guid.NewGuid().ToString("N") + "/" +
            (Stopwatch.GetTimestamp() + (long)(budget.TotalSeconds * Stopwatch.Frequency))
                .ToString(CultureInfo.InvariantCulture);

        internal static WatchdogStopRequest? Parse(string? token)
        {
            if (token == null || token.Length < 34 || token.Length > 52 || token[32] != '/' ||
                !Guid.TryParseExact(token.Substring(0, 32), "N", out _) ||
                !long.TryParse(token.Substring(33), NumberStyles.None, CultureInfo.InvariantCulture, out var deadline) ||
                deadline > Stopwatch.GetTimestamp() + (long)(Budget.TotalSeconds * Stopwatch.Frequency))
                return null;
            return new WatchdogStopRequest(token, deadline);
        }

        // All decision writers use the runtime's receipt lock. Check at the claim
        // point even if the timeout timer, pipe handler or executor itself ran late.
        internal void ExpireIfDue()
        {
            if (Stopwatch.GetTimestamp() >= Deadline)
                _decision.TrySetResult(WatchdogStopResult.Expired);
        }

        internal bool Decide(WatchdogStopResult result)
        {
            ExpireIfDue();
            return _decision.TrySetResult(result);
        }
    }
}
