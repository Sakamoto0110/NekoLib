namespace NekoLib.Pipes
{
    /// <summary>
    /// Selects what an event hub does when one subscriber's bounded queue is full.
    /// </summary>
    public enum PipeEventQueueOverflowPolicy
    {
        /// <summary>Drop the newest event for that subscriber and keep it connected.</summary>
        DropNewest = 0,

        /// <summary>Disconnect the slow subscriber and fail its queued deliveries.</summary>
        DisconnectSubscriber = 1
    }
}
