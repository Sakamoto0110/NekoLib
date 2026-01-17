using NekoLib.Navigation.Runtime.Core;

namespace NekoLib.Navigation.Contracts.Plataform
{
    public interface INavigationBootstrap
    {
        NavigationContext Build();
    }
}
