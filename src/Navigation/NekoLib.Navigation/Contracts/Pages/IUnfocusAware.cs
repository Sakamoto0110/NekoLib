using System.Threading.Tasks;

namespace NekoLib.Navigation.Contracts.Pages
{
    /// <summary>
    /// Optional hook implemented by views that want to react when focus leaves
    /// them. The service raises <see cref="OnUnfocusAsync"/> via the platform's
    /// focus observer; the view decides what to do. Popovers typically resolve
    /// their own completion callback to dismiss, but the contract itself is
    /// neutral about that behavior.
    /// </summary>
    public interface IUnfocusAware
    {
        Task OnUnfocusAsync();
    }
}
