using System;
using NekoLib.Navigation.Contracts.Platform;

namespace NekoLib.Navigation.Runtime.Services
{
    /// <summary>
    /// Coordinates overlapping modal services over one platform blocker. Platform
    /// implementations written before nested Dialog/Prompt support may be boolean;
    /// the wrapper therefore calls them only for the outermost transition.
    /// </summary>
    internal sealed class ReferenceCountedInteractionBlocker :
        IPageAwareInteractionBlocker
    {
        private readonly IInteractionBlocker _inner;
        private readonly object _sync = new object();
        private int _depth;

        public ReferenceCountedInteractionBlocker(IInteractionBlocker inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        internal int Depth
        {
            get
            {
                lock (_sync)
                    return _depth;
            }
        }

        public void Block()
        {
            lock (_sync)
            {
                if (_depth > 0)
                {
                    _depth++;
                    return;
                }

                _depth = 1;
                try
                {
                    _inner.Block();
                }
                catch
                {
                    // The platform call may reenter this wrapper. Roll back the
                    // provisional owner only when reentrancy did not change it.
                    if (_depth == 1)
                        _depth = 0;
                    throw;
                }
            }
        }

        public void Unblock()
        {
            lock (_sync)
            {
                if (_depth == 0)
                    return;

                if (_depth > 1)
                {
                    _depth--;
                    return;
                }

                _depth = 0;
                try
                {
                    _inner.Unblock();
                }
                catch
                {
                    // Preserve state established by a reentrant Block/Unblock.
                    // Restore the released owner only if depth is still untouched.
                    if (_depth == 0)
                        _depth = 1;
                    throw;
                }
            }
        }

        public void OnViewAdded(object view, bool isModalSurface)
        {
            lock (_sync)
            {
                if (_inner is IPageAwareInteractionBlocker aware)
                    aware.OnViewAdded(view, isModalSurface);
            }
        }

        public void OnViewRemoved(object view)
        {
            lock (_sync)
            {
                if (_inner is IPageAwareInteractionBlocker aware)
                    aware.OnViewRemoved(view);
            }
        }
    }
}
