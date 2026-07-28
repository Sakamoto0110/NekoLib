#nullable enable
using System;
using System.Runtime.ExceptionServices;

namespace NekoLib.Navigation.Runtime.Services
{
    /// <summary>
    /// Runs teardown steps best-effort while preserving the first failure for
    /// the public caller/runtime after every owned resource has been reclaimed.
    /// </summary>
    internal static class SurfaceCleanup
    {
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
