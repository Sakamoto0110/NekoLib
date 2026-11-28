using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using NekoLib.Navigation.Contracts.Guards;

namespace NekoLib.Navigation.Runtime.Session
{
    /// <summary>
    /// The navigation module's built-in mutable session. Instantiated by every
    /// <see cref="NekoLib.Navigation.Runtime.Core.NavigationContext"/> and exposed
    /// at the consumer through <c>NavigationService.Session</c>.
    /// <para>
    /// Implements <see cref="IUserContext"/> so guards (e.g.
    /// <c>[RequireRole]</c>, <c>[RequireAuthenticated]</c>) see live state without
    /// the consumer ever implementing an auth contract. The bootstrap's idle
    /// timeout and optional maximum session lifetime sign the session out
    /// automatically when configured on <c>PageNavBootstrap</c>.
    /// </para>
    /// </summary>
    public sealed class NavigationSession : IUserContext
    {
        private static readonly string[] Empty = new string[0];
        private readonly object _stateSync = new object();
        private int _generation;
        private long _signedInTimestamp;

        internal event Action? Changed;
        internal event Action? SignedOut;
        internal int Generation => Volatile.Read(ref _generation);

        /// <inheritdoc />
        public bool IsAuthenticated { get; private set; }
        /// <inheritdoc />
        public IReadOnlyCollection<string> Roles { get; private set; } = Empty;
        /// <inheritdoc />
        public IReadOnlyCollection<string> Permissions { get; private set; } = Empty;

        /// <summary>Marks the session authenticated with the given roles and clears permissions.</summary>
        /// <param name="roles">Role names copied and validated immediately.</param>
        public void SignIn(params string[] roles)
        {
            var copiedRoles = GuardContractValidation.CopyNames(roles, nameof(roles), "role");

            StartSession(
                copiedRoles.Length > 0 ? Array.AsReadOnly(copiedRoles) : Empty,
                Empty);
        }

        /// <summary>Marks the session authenticated with explicit roles and permissions.</summary>
        /// <param name="roles">Role names copied and validated immediately.</param>
        /// <param name="permissions">Permission names copied and validated immediately.</param>
        public void SignIn(IEnumerable<string> roles, IEnumerable<string> permissions)
        {
            var copiedRoles = GuardContractValidation.CopyNames(roles, nameof(roles), "role");
            var copiedPermissions = GuardContractValidation.CopyNames(
                permissions,
                nameof(permissions),
                "permission");

            StartSession(Array.AsReadOnly(copiedRoles), Array.AsReadOnly(copiedPermissions));
        }

        private void StartSession(IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissions)
        {
            lock (_stateSync)
            {
                Roles = roles;
                Permissions = permissions;
                _signedInTimestamp = Stopwatch.GetTimestamp();
                IsAuthenticated = true;
                Interlocked.Increment(ref _generation);
            }

            RaiseChanged();
        }

        internal (int Generation, long SignedInTimestamp)? GetAuthenticatedLifetime()
        {
            lock (_stateSync)
                return IsAuthenticated ? (_generation, _signedInTimestamp) : null;
        }

        /// <summary>Clears authentication state and closes any open navigation prompts.</summary>
        public void SignOut() => SignOutCore(expectedGeneration: null);

        internal bool TrySignOut(int expectedGeneration) => SignOutCore(expectedGeneration);

        private bool SignOutCore(int? expectedGeneration)
        {
            lock (_stateSync)
            {
                if (expectedGeneration.HasValue && _generation != expectedGeneration.Value)
                    return false;

                IsAuthenticated = false;
                Roles = Empty;
                Permissions = Empty;
                _signedInTimestamp = 0;
                Interlocked.Increment(ref _generation);
            }

            try { SignedOut?.Invoke(); }
            finally { RaiseChanged(); }
            return true;
        }

        private void RaiseChanged()
        {
            var subscribers = Changed;
            if (subscribers == null)
                return;

            foreach (Action subscriber in subscribers.GetInvocationList())
            {
                try { subscriber(); }
                catch
                {
                    // Session diagnostics/observers must never alter auth state.
                }
            }
        }
    }
}
