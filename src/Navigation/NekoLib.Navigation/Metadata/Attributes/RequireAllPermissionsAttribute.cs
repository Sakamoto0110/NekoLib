using NekoLib.Navigation.Contracts.Guards;
using NekoLib.Navigation.Runtime.Guards;

namespace NekoLib.Navigation.Metadata.Attributes
{
    public sealed class RequireAllPermissionsAttribute : GuardAttribute
    {
        private readonly string[] _permissions;

        public RequireAllPermissionsAttribute(params string[] permissions)
        {
            _permissions = GuardContractValidation.CopyNames(
                permissions,
                nameof(permissions),
                "required permission");
        }

        public override IGuard CreateGuard()
            => ApplyRedirect(new RequireAllPermissionsGuard(_permissions));
    }


}
