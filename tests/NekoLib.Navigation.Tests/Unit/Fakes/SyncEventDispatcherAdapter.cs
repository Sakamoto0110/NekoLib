using System;
using NekoLib.Navigation.Contracts.Platform;

namespace NekoLib.Navigation.Tests.Unit.Fakes
{
    /// <summary>
    /// Inline IEventDispatcherAdapter: both <see cref="Invoke"/> and
    /// <see cref="BeginInvoke"/> run the action on the calling thread immediately.
    /// Tests run on the test runner thread, so we treat that as the "UI" thread —
    /// the runtime's marshal calls become no-ops and the navigation stays
    /// deterministic and synchronous.
    /// </summary>
    public sealed class SyncEventDispatcherAdapter : IEventDispatcherAdapter
    {
        public void Invoke(Action action) => action?.Invoke();
        public void BeginInvoke(Action action) => action?.Invoke();
    }
}
