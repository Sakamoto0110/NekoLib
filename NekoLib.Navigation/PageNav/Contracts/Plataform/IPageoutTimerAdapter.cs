using System;

namespace NekoLib.Navigation.Contracts.Plataform
{
    public interface IPageTimeoutAdapter : IDisposable
    {
        /// <summary>
        /// Starts the timeout countdown.
        /// </summary>
        void Start(int timeoutMilliseconds);

        /// <summary>
        /// Stops the timeout countdown.
        /// </summary>
        void Stop();

        /// <summary>
        /// Fired when the timeout elapses.
        /// </summary>
        event Action TimeoutElapsed;
    }

}
