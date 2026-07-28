using System;
using System.Collections.Generic;
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
    /// timeout calls <see cref="SignOut"/> automatically when configured via
    /// <c>PageNavBootstrap.UseIdleTimeout(int)</c>.
    /// </para>
    /// </summary>
    public sealed class NavigationSession : IUserContext
    {
        private static readonly string[] Empty = new string[0];

        internal event Action? Changed;

        public bool IsAuthenticated { get; private set; }
        public IReadOnlyCollection<string> Roles { get; private set; } = Empty;
        public IReadOnlyCollection<string> Permissions { get; private set; } = Empty;

        /// <summary>Mark the session authenticated with the given roles.</summary>
        public void SignIn(params string[] roles)
        {
            IsAuthenticated = true;
            Roles = roles != null && roles.Length > 0
                ? (IReadOnlyCollection<string>)Array.AsReadOnly(
                    (string[])roles.Clone())
                : Empty;
            Permissions = Empty;
            RaiseChanged();
        }

        /// <summary>Mark the session authenticated with explicit roles + permissions.</summary>
        public void SignIn(IEnumerable<string> roles, IEnumerable<string> permissions)
        {
            IsAuthenticated = true;
            Roles = roles != null ? new List<string>(roles).AsReadOnly() : (IReadOnlyCollection<string>)Empty;
            Permissions = permissions != null ? new List<string>(permissions).AsReadOnly() : (IReadOnlyCollection<string>)Empty;
            RaiseChanged();
        }

        /// <summary>Clear authentication state.</summary>
        public void SignOut()
        {
            IsAuthenticated = false;
            Roles = Empty;
            Permissions = Empty;
            RaiseChanged();
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
