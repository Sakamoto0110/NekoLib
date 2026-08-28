using NekoLib.Navigation.Contracts.Guards;
using NekoLib.Navigation.Runtime.Guards;
using System;

namespace NekoLib.Navigation.Metadata.Attributes

{
    /// <summary>Requires one named permission before entering the attributed page.</summary>
    public sealed class RequirePermissionAttribute : GuardAttribute
    {
        /// <summary>Gets the required permission name.</summary>
        public string Permission { get; }

        /// <summary>Initializes a deny-only permission requirement.</summary>
        /// <param name="permission">Non-empty permission name.</param>
        public RequirePermissionAttribute(string permission)
        {
            Permission = !string.IsNullOrWhiteSpace(permission)
                ? permission
                : throw new ArgumentException(
                    "A required permission cannot be null, empty, or whitespace.", nameof(permission));
        }

        /// <summary>Initializes a permission requirement with a concrete redirect page.</summary>
        /// <param name="permission">Non-empty permission name.</param>
        /// <param name="redirect">Concrete <see cref="Contracts.Pages.IPageView"/> type used when permission is missing.</param>
        public RequirePermissionAttribute(string permission, Type redirect)
            : this(permission)
        {
            RedirectTo = redirect ?? throw new ArgumentNullException(nameof(redirect));
        }

        /// <inheritdoc />
        public override IGuard CreateGuard()
            => ApplyRedirect(new RequirePermissionGuard(Permission));
    }
}
