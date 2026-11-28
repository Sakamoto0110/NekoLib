namespace NekoLib.Navigation.Contracts.Pages
{
    /// <summary>
    /// Allows a prompt to carry an unsubmitted draft through navigation to Idle
    /// within the same signed-in session.
    /// </summary>
    public interface IPromptDraftState
    {
        /// <summary>
        /// Copies the input to keep after the view is disposed. Return
        /// <see langword="null"/> to skip draft restoration.
        /// </summary>
        object? CaptureDraft();

        /// <summary>
        /// Applies the saved input to the next prompt of the same type opened
        /// from the original page after its normal setup completes.
        /// </summary>
        void RestoreDraft(object draft);
    }
}
