using System.Collections.Generic;

namespace NekoLib.Navigation.Contracts.Guards
{
    /// <summary>
    /// Read-only authentication state consumed by navigation guards. The default
    /// implementation is <c>NavigationSession</c>, exposed as
    /// <c>NavigationService.Session</c>.
    /// </summary>
    public interface IUserContext
    {
        /// <summary>True when the current navigation session is signed in.</summary>
        bool IsAuthenticated { get; }

        /// <summary>Role names available to role-based guards.</summary>
        IReadOnlyCollection<string> Roles { get; }

        /// <summary>Permission names available to permission-based guards.</summary>
        IReadOnlyCollection<string> Permissions { get; }
    }
}
