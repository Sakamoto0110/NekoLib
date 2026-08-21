using NekoLib.Navigation.Contracts.Guards;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Runtime.Guards {
    public sealed class RequireRoleGuard : IGuard
    {
        private readonly string _role;
        private readonly Type? _redirect;

        public RequireRoleGuard(string role, Type? redirect = null)
        {
            _role = GuardContractValidation.RequireName(role, nameof(role), "required role");
            if (redirect != null)
                GuardResult.Redirect(redirect);
            _redirect = redirect;
        }

        public Task<GuardResult> EvaluateAsync(GuardContext context)
        {
            if (context?.User?.Roles == null)
                return Task.FromResult(
                    GuardResult.Deny("User context not available."));
            if (context.User.Roles.Contains(_role))
                return Task.FromResult(GuardResult.Allow());

            if (_redirect != null)
                return Task.FromResult(
                    GuardResult.Redirect(_redirect, $"Missing role: {_role}"));

            return Task.FromResult(
                GuardResult.Deny($"Missing role: {_role}"));
        }
    }

}
