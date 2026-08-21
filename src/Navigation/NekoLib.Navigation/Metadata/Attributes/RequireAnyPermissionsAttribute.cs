using NekoLib.Navigation.Contracts.Guards;
using NekoLib.Navigation.Runtime.Guards;

namespace NekoLib.Navigation.Metadata.Attributes
{
    public sealed class RequireAnyPermissionsAttribute : GuardAttribute
    {
        private readonly string[] _permissions;

        public RequireAnyPermissionsAttribute(params string[] permissions)
        {
            _permissions = GuardContractValidation.CopyNames(
                permissions,
                nameof(permissions),
                "required permission");
        }

        public override IGuard CreateGuard()
            => ApplyRedirect(new RequireAnyPermissionGuard(_permissions));
    }


}
