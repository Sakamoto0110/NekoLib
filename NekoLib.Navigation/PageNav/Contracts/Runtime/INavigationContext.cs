/// <summary>
/// TODO: Document this type.
/// Describe responsibility, lifecycle expectations,
/// threading guarantees, and ownership rules.
/// </summary>
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Plataform;

namespace NekoLib.Navigation.Contracts.Runtime
{


    public interface INavigationContext  
    {
        IPageHost Host { get; }
        

        IPageOverlay Overlay { get; }
        IInteractionBlocker Blocker { get; }
 
         
    }

}
