/// <summary>
/// TODO: Document this type.
/// Describe responsibility, lifecycle expectations,
/// threading guarantees, and ownership rules.
/// </summary>
namespace NekoLib.Navigation.Contracts.Platform
{

 

    public interface IInteractionBlocker  
    {
        void Block();
        void Unblock();
    }
}
