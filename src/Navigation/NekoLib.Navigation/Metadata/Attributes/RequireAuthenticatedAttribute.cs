using NekoLib.Navigation.Contracts.Guards;
using NekoLib.Navigation.Runtime.Guards;

namespace NekoLib.Navigation.Metadata.Attributes
{
    public sealed class RequireAuthenticatedAttribute : GuardAttribute
    {
        public override IGuard CreateGuard()
            => new RequireAuthenticatedGuard();
    }



}