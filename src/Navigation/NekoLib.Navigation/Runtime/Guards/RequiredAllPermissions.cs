using NekoLib.Navigation.Contracts.Guards;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Runtime.Guards {
    public sealed class RequireAllPermissionsGuard : IGuard
    {
        private readonly string[] _required;

        public RequireAllPermissionsGuard(params string[] requiredPermissions)
        {
            _required = requiredPermissions ?? Array.Empty<string>();
        }

        public Task<GuardResult> EvaluateAsync(GuardContext context)
        {
            if (_required.Length == 0)
                return Task.FromResult(GuardResult.Allow());

            var userPerms = context.User?.Permissions;

            if (userPerms == null || userPerms.Count == 0)
                return Task.FromResult(
                    GuardResult.Deny("User has no permissions."));

            foreach (var required in _required)
            {
                if (!userPerms.Contains(required))
                {
                    return Task.FromResult(
                        GuardResult.Deny($"Missing permission: {required}"));
                }
            }

            return Task.FromResult(GuardResult.Allow());
        }
    }
}
