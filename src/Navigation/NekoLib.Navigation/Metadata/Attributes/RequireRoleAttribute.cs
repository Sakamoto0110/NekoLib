using NekoLib.Navigation.Contracts.Guards;
using NekoLib.Navigation.Runtime.Guards;

namespace NekoLib.Navigation.Metadata.Attributes
{
    public sealed class RequireRoleAttribute : GuardAttribute
    {
        public string Role { get; }
         

        public RequireRoleAttribute(string role)
        {
            Role = role;
        }

        public override IGuard CreateGuard()
        {
            return new RequireRoleGuard(Role, RedirectTo);
        }
    }


}