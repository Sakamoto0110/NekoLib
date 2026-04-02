using System.Collections.Generic;

namespace NekoLib.Navigation.Contracts.Guards
{
    public sealed class DefaultUserContext : IUserContext
    {
        private static readonly string[] Empty = new string[0];

        public bool IsAuthenticated => false;

        public string UserId => null;

        public IReadOnlyCollection<string> Roles => Empty;

        public IReadOnlyCollection<string> Permissions => Empty;
    }
}