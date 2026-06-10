using System;
using System.Collections.Generic;
using NekoLib.Navigation.Contracts.Platform;

namespace NekoLib.Navigation.Tests.Unit.Fakes
{
    /// <summary>
    /// In-memory focus observer. Tests call <see cref="TriggerUnfocus"/> to
    /// simulate the user clicking away; <see cref="SubscriptionCount"/> reports
    /// active subscriptions so tests can pin teardown behavior.
    /// </summary>
    public sealed class FakeFocusObserverAdapter : IFocusObserverAdapter
    {
        private readonly Dictionary<object, Action> _subscriptions = new();

        public int SubscriptionCount
        {
            get { lock (_subscriptions) return _subscriptions.Count; }
        }

        public IDisposable Track(object nativeView, Action onUnfocus)
        {
            lock (_subscriptions) _subscriptions[nativeView] = onUnfocus;
            return new Subscription(this, nativeView);
        }

        /// <summary>Simulate "user clicked away" on the given native view.</summary>
        public void TriggerUnfocus(object nativeView)
        {
            Action cb;
            lock (_subscriptions)
            {
                if (!_subscriptions.TryGetValue(nativeView, out cb))
                    return;
            }
            cb();
        }

        private void Remove(object nativeView)
        {
            lock (_subscriptions) _subscriptions.Remove(nativeView);
        }

        private sealed class Subscription : IDisposable
        {
            private FakeFocusObserverAdapter _owner;
            private object _nativeView;

            public Subscription(FakeFocusObserverAdapter owner, object nativeView)
            {
                _owner = owner;
                _nativeView = nativeView;
            }

            public void Dispose()
            {
                _owner?.Remove(_nativeView);
                _owner = null;
                _nativeView = null;
            }
        }
    }
}
