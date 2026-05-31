using NekoLib.Navigation.Contracts.Guards;
using NekoLib.Navigation.Runtime.Guards;

namespace NekoLib.Navigation.Metadata.Attributes
{
    public sealed class RequireAllPermissionsAttribute : GuardAttribute
    {
        private readonly string[] _permissions;

        public RequireAllPermissionsAttribute(params string[] permissions)
        {
            _permissions = permissions;
        }

        public override IGuard CreateGuard()
            => new RequireAllPermissionsGuard(_permissions);
    }


}