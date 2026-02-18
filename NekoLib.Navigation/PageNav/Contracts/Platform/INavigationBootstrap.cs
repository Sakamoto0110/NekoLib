using NekoLib.Navigation.Runtime.Core;

namespace NekoLib.Navigation.Contracts.Platform
{
    public interface INavigationBootstrap
    {
        NavigationContext Build();
    }
}
