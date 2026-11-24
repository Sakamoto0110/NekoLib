namespace NekoLib.Navigation.Contracts.Runtime
{
    /// <summary>Chooses which native events renew navigation idle activity.</summary>
    public enum InteractionObservationMode
    {
        /// <summary>Observe mouse, key and text input events, excluding control value changes and clicks.</summary>
        InputOnly = 0,
        /// <summary>Also observe button clicks and text-control changes, including programmatic changes.</summary>
        InputAndControlChanges = 1
    }
}
