using NekoLib.Navigation.Contracts.Guards;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Runtime.Guards {
    /// <summary>Allows navigation only when the user has every configured permission.</summary>
    public sealed class RequireAllPermissionsGuard : IGuard
    {
        private readonly string[] _required;

        /// <summary>Initializes the guard with a defensive copy of required names.</summary>
        /// <param name="requiredPermissions">Permission names; an empty set allows navigation.</param>
        public RequireAllPermissionsGuard(params string[] requiredPermissions)
        {
            _required = GuardContractValidation.CopyNames(
                requiredPermissions,
                nameof(requiredPermissions),
                "required permission");
        }

        /// <inheritdoc />
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
