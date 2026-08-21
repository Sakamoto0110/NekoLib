using NekoLib.Navigation.Contracts.Guards;
using NekoLib.Navigation.Runtime.Guards;
using System;

namespace NekoLib.Navigation.Metadata.Attributes

{
    public sealed class RequirePermissionAttribute : GuardAttribute
    {
        public string Permission { get; }

        public RequirePermissionAttribute(string permission)
        {
            Permission = !string.IsNullOrWhiteSpace(permission)
                ? permission
                : throw new ArgumentException(
                    "A required permission cannot be null, empty, or whitespace.", nameof(permission));
        }

        public RequirePermissionAttribute(string permission, Type redirect)
            : this(permission)
        {
            RedirectTo = redirect ?? throw new ArgumentNullException(nameof(redirect));
        }

        public override IGuard CreateGuard()
            => ApplyRedirect(new RequirePermissionGuard(Permission));
    }
}
