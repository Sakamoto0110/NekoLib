using NekoLib.Navigation.Contracts.Guards;
using NekoLib.Navigation.Runtime.Guards;

namespace NekoLib.Navigation.Metadata.Attributes
{
    /// <summary>Requires every configured permission before entering the attributed page.</summary>
    public sealed class RequireAllPermissionsAttribute : GuardAttribute
    {
        private readonly string[] _permissions;

        /// <summary>Initializes the attribute with the complete required permission set.</summary>
        /// <param name="permissions">Permission names copied and validated immediately; an empty set allows access.</param>
        public RequireAllPermissionsAttribute(params string[] permissions)
        {
            _permissions = GuardContractValidation.CopyNames(
                permissions,
                nameof(permissions),
                "required permission");
        }

        /// <inheritdoc />
        public override IGuard CreateGuard()
            => ApplyRedirect(new RequireAllPermissionsGuard(_permissions));
    }


}
