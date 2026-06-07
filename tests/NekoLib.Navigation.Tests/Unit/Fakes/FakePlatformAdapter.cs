using System;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Contracts.Runtime;

namespace NekoLib.Navigation.Tests.Unit.Fakes
{
    /// <summary>
    /// Stub adapter that exists only to satisfy <see cref="NavigationContext"/>'s
    /// non-null constraint on <see cref="IPlatformAdapter"/>. The runtime does not
    /// dispatch through these methods during the operations under test — it pulls
    /// the dispatcher and services out of the registered <c>ServiceLocator</c>.
    /// If a test exercises a code path that does call into the adapter it will
    /// throw clearly and we'll add the needed implementation then.
    /// </summary>
    public sealed class FakePlatformAdapter : IPlatformAdapter
    {
        public bool CanHandle(object host) => true;
        public IPageHost CreateHost(object host) => throw new NotImplementedException();
        public IEventDispatcherAdapter CreateEventDispatcher(object host) => throw new NotImplementedException();
        public IEventSubscriptionAdapter CreateEventSubscriber(object host) => throw new NotImplementedException();
        public IInteractionBlocker CreateInteractionBlocker(object host) => throw new NotImplementedException();
        public ITimerAdapter CreateTimerAdapter() => throw new NotImplementedException();
        public Type GetDefaultLoadingMaskType() => null;
        public IInteractionObserverService CreateInteractionObserverAdapter(object host) => throw new NotImplementedException();
    }
}
