namespace NekoLib.Navigation.Metadata
{
    /// <summary>
    /// Defines how the page is presented relative to the current UI stack.
    /// 
    /// This affects stacking and input behavior.
    /// </summary>
    public enum PagePresentationMode
    {
        /// <summary>
        /// Replace the current visible page.
        /// Standard navigation.
        /// </summary>
        Replace = 0,

        /// <summary>
        /// Overlay above existing page(s).
        /// Underlying pages remain visible.
        /// </summary>
        Overlay = 1,

        /// <summary>
        /// Overlay and block input to underlying pages.
        /// Modal-style behavior.
        /// </summary>
        ModalOverlay = 2
    }
}