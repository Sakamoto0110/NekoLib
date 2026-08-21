using NekoLib.Navigation.Contracts.Guards;
using NekoLib.Navigation.Runtime.Guards;

using System;

namespace NekoLib.Navigation.Metadata.Attributes
{
    public sealed class RequireRoleAttribute : GuardAttribute
    {
        public string Role { get; }
         

        public RequireRoleAttribute(string role)
        {
            Role = !string.IsNullOrWhiteSpace(role)
                ? role
                : throw new ArgumentException("A required role cannot be null, empty, or whitespace.", nameof(role));
        }

        public override IGuard CreateGuard()
            => ApplyRedirect(new RequireRoleGuard(Role));
    }


}
