using NekoLib.Navigation.Contracts.Guards;
using NekoLib.Navigation.Runtime.Guards;

namespace NekoLib.Navigation.Metadata.Attributes
{
    /// <summary>Requires the built-in navigation session to be authenticated.</summary>
    public sealed class RequireAuthenticatedAttribute : GuardAttribute
    {
        /// <inheritdoc />
        public override IGuard CreateGuard()
            => ApplyRedirect(new RequireAuthenticatedGuard());
    }



}
