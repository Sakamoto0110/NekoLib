namespace NekoLib.Navigation.Contracts.Platform
{
    /// <summary>
    /// Platform service that temporarily prevents user interaction with the
    /// navigation host while a modal surface is active.
    /// </summary>
    public interface IInteractionBlocker  
    {
        /// <summary>Disable background interaction.</summary>
        void Block();

        /// <summary>Restore interaction state changed by <see cref="Block"/>.</summary>
        void Unblock();
    }
}
