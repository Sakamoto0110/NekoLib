using System;
using System.Windows;
using NekoLib.Navigation.Contracts.Platform;

namespace NekoLib.Navigation.Wpf.Adapters
{
    /// <summary>
    /// Blocks input by toggling <see cref="UIElement.IsEnabled"/> on the root host.
    /// WPF's disabled state propagates down the visual tree, so a single toggle is
    /// enough — no need to walk the subtree like WinForms does.
    /// </summary>
    public sealed class WpfInteractionBlocker : IInteractionBlocker
    {
        private readonly UIElement _root;
        private bool _blocked;
        private bool _previousEnabled;

        public WpfInteractionBlocker(UIElement root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        public void Block()
        {
            if (_blocked) return;

            _previousEnabled = _root.IsEnabled;
            _root.IsEnabled = false;
            _blocked = true;
        }

        public void Unblock()
        {
            if (!_blocked) return;

            _root.IsEnabled = _previousEnabled;
            _blocked = false;
        }
    }
}
