using NekoLib.Navigation.Contracts.Platform;
using System;

namespace NekoLib.Navigation.Infrastructure
{

    /// <summary>
    /// Process-wide registry for the active platform adapter.
    /// <para>
    /// Configured once per navigation session via <c>PageNavBootstrap.Use&lt;T&gt;</c>.
    /// Call <see cref="Reset"/> (e.g. from <c>NavigationService.Shutdown</c>) before
    /// registering a new adapter so that multi-session / test-isolation scenarios work
    /// without the second <see cref="Register"/> call throwing (S-1).
    /// </para>
    /// </summary>
    public static class PlatformRegistry
    {
        private static IPlatformAdapter _current;

        public static void Register(IPlatformAdapter adapter)
        {
            if (_current != null)
                throw new InvalidOperationException(
                    "PlatformAdapter already registered. Call PlatformRegistry.Reset() before registering a new adapter.");

            _current = adapter
                ?? throw new ArgumentNullException(nameof(adapter));
        }

        /// <summary>
        /// Clears the registered adapter so a new one can be registered in the next
        /// navigation session. Call this from your shutdown / test-teardown path.
        /// </summary>
        public static void Reset()
        {
            _current = null;
        }

        public static IPlatformAdapter Current
            => _current ?? throw new InvalidOperationException(
                "PlatformAdapter not registered. Call PlatformRegistry.Register() during bootstrap.");
    }
}