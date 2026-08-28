using NekoLib.Navigation.Contracts.Guards;
using NekoLib.Navigation.Runtime.Guards;

using System;

namespace NekoLib.Navigation.Metadata.Attributes
{
    /// <summary>Requires one named role before entering the attributed page.</summary>
    public sealed class RequireRoleAttribute : GuardAttribute
    {
        /// <summary>Gets the required role name.</summary>
        public string Role { get; }
         

        /// <summary>Initializes a deny-only role requirement.</summary>
        /// <param name="role">Non-empty role name.</param>
        public RequireRoleAttribute(string role)
        {
            Role = !string.IsNullOrWhiteSpace(role)
                ? role
                : throw new ArgumentException("A required role cannot be null, empty, or whitespace.", nameof(role));
        }

        /// <inheritdoc />
        public override IGuard CreateGuard()
            => ApplyRedirect(new RequireRoleGuard(Role));
    }


}
