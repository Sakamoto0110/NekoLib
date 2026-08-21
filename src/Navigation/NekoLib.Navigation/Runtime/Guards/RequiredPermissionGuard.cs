using NekoLib.Navigation.Contracts.Guards;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Runtime.Guards
{
    public sealed class RequirePermissionGuard : IGuard
    {
        private readonly string _permission;
        private readonly Type? _redirect;

        public RequirePermissionGuard(string permission, Type? redirect = null)
        {
            _permission = GuardContractValidation.RequireName(
                permission,
                nameof(permission),
                "required permission");
            if (redirect != null)
                GuardResult.Redirect(redirect);
            _redirect = redirect;
        }

        public Task<GuardResult> EvaluateAsync(GuardContext context)
        {
            if (context?.User?.Permissions == null)
                return Task.FromResult(
                    GuardResult.Deny("User context not available."));
            if (context.User.Permissions.Contains(_permission))
                return Task.FromResult(GuardResult.Allow());

            if (_redirect != null)
                return Task.FromResult(
                    GuardResult.Redirect(_redirect, $"Missing permission: {_permission}"));

            return Task.FromResult(
                GuardResult.Deny($"Missing permission: {_permission}"));
        }
    }
}


