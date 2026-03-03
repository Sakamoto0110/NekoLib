using NekoLib.Navigation.Contracts.Guards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Runtime.Guards {
    public sealed class RequireRoleGuard : IGuard
    {
        private readonly string _role;
        private readonly Type _redirect;

        public RequireRoleGuard(string role, Type redirect)
        {
            _role = role;
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
