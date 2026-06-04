using System;
using System.Diagnostics;
using System.Threading;

namespace NekoLib.Pipes.Tests.Unit.Fakes
{
    /// <summary>
    /// Helpers for the in-process pipe tests: a collision-free pipe name per test
    /// (so xUnit's parallel collections never share a pipe) and a small spin-wait
    /// for the inherently asynchronous connect/subscribe handshakes.
    /// </summary>
    internal static class PipeTestUtil
    {
        static PipeTestUtil()
        {
            // On net481 every blocking pipe op (server WaitForConnection, client
            // Connect) holds a thread-pool thread, and a disposed server's pending
            // accept stays blocked until GC (audit M6) — so threads accumulate
            // across tests. Raise the pool minimum so connects never wait on
            // thread-pool ramp-up. (Harmless on net9, which uses true async I/O.)
            ThreadPool.GetMinThreads(out int worker, out int io);
            ThreadPool.SetMinThreads(Math.Max(worker, 100), Math.Max(io, 100));
        }

        public static string UniqueName(string prefix = "neko.test")
            => prefix + "." + Guid.NewGuid().ToString("N");

        /// <summary>Polls <paramref name="condition"/> until true or the timeout elapses.</summary>
        public static bool WaitUntil(Func<bool> condition, int timeoutMs = 3000)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (condition())
                    return true;
                Thread.Sleep(20);
            }
            return condition();
        }

        /// <summary>TFM-agnostic read of a message payload as text (JsonElement / JToken).</summary>
        public static string DataText(PipeMessage m)
            => m == null ? "" : (m.Data?.ToString() ?? "");
    }
}
