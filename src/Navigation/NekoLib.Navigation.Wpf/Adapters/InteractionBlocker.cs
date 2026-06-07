using NekoLib.Navigation.Contracts.Platform;
using System;
using System.Windows;

namespace NekoLib.Navigation.Wpf.Adapters
{
    /// <summary>
    /// Blocks user interaction by disabling the host element. WPF propagates a disabled
    /// state through the entire visual subtree, so the whole host surface is blocked.
    /// The prior enabled state is restored on unblock.
    /// </summary>
    public sealed class InteractionBlocker : IInteractionBlocker
    {
        private readonly UIElement _root;
        private bool _blocked;
        private bool _previousIsEnabled;

        public InteractionBlocker(UIElement root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        public void Block()
        {
            if (_blocked)
                return;

            _previousIsEnabled = _root.IsEnabled;
            _root.IsEnabled = false;
            _blocked = true;
        }

        public void Unblock()
        {
            if (!_blocked)
                return;

            _root.IsEnabled = _previousIsEnabled;
            _blocked = false;
        }
    }
}
