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
    /// <remarks>
    /// <b>This is a focus signal, not a click-outside signal.</b> It is raised
    /// when keyboard focus leaves the view's subtree, or when the owning
    /// form/window is deactivated. Clicking an area that cannot take focus —
    /// labels, panels, a page built only from static content — moves no focus and
    /// therefore raises nothing, so a light-dismiss surface stays open. Moving
    /// focus inside the view (tabbing between its own fields) does not raise it
    /// either. A true click-outside model would need mouse capture or a hit-test
    /// scrim and is deliberately not implemented; see the overlay section of the
    /// Navigation README.
    /// </remarks>
    public interface IUnfocusAware
    {
        Task OnUnfocusAsync();
    }
}
