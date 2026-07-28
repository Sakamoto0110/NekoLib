namespace NekoLib.Navigation.Contracts.Pages
{
    /// <summary>
    /// Optional native visibility hook invoked on the UI thread when a page becomes
    /// the active destination or leaves the active position. Runtime visibility
    /// accounting is independent of this hook, so custom pages need not implement
    /// it merely to appear in diagnostics.
    /// </summary>
    public interface IPageVisibility
    {
        /// <summary>Show the page after it has been attached and brought to front.</summary>
        void ShowPage();

        /// <summary>Hide the active page before its leave lifecycle hook runs.</summary>
        void HidePage();
    }


 

}
