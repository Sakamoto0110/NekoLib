using System.Collections.Generic;

namespace NekoLib.Navigation.Contracts.Guards
{
    public interface IUserContext
    {
        bool IsAuthenticated { get; }
        IReadOnlyCollection<string> Roles { get; }
        IReadOnlyCollection<string> Permissions { get; }
    }
}