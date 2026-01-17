using NekoLib.Navigation.Contracts.Plataform;
using System;

namespace NekoLib.Navigation.Infrastructure
{

    /// <summary>
    /// Central registry for platform adapters.
    /// Must be configured during bootstrap only.
    /// </summary>


    public static class PlatformRegistry
    {
        private static IPlatformAdapter _current;

        public static void Register(IPlatformAdapter adapter)
        {
            if (_current != null)
                throw new InvalidOperationException(
                    "PlatformAdapter already registered.");

            _current = adapter
                ?? throw new ArgumentNullException(nameof(adapter));
        }

        public static IPlatformAdapter Current
            => _current ?? throw new InvalidOperationException(
                "PlatformAdapter not registered. Call PlatformRegistry.Register() during bootstrap.");
    }
}