#nullable enable
using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Runtime.Services
{
    /// <summary>
    /// Runs teardown steps best-effort while preserving the first failure for
    /// the public caller/runtime after every owned resource has been reclaimed.
    /// </summary>
    internal static class SurfaceCleanup
    {
        public static async Task<bool> WaitForSetupAsync(Task setup, Task result, string serviceName)
        {
            // Teardown can settle an attached view while its user-owned setup
            // is still pending. Late setup failures cannot change that result.
            if (!setup.IsCompleted && await Task.WhenAny(setup, result) != setup)
            {
                _ = ObserveLateSetupAsync(setup, serviceName);
                return false;
            }

            await setup;
            return true;
        }

        private static async Task ObserveLateSetupAsync(Task setup, string serviceName)
        {
            try { await setup.ConfigureAwait(false); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[" + serviceName + "] Setup failed after closure: " + ex);
            }
        }

        public static Exception? Run(
            Exception? firstError,
            Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                if (firstError == null)
                    firstError = ex;
            }

            return firstError;
        }

        public static void Rethrow(Exception? error)
        {
            if (error != null)
                ExceptionDispatchInfo.Capture(error).Throw();
        }
    }
}
