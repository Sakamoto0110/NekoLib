using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using NekoLib.Navigation.Contracts.Platform;

namespace NekoLib.Navigation.Wpf.Adapters
{
    /// <summary>
    /// Blocks background interaction while a modal surface (dialog / prompt) is open.
    /// Disables the host Panel's CURRENT children — not the host itself — so the
    /// modal overlay, which the service adds to the same Panel right AFTER
    /// <see cref="Block"/>, stays interactive. Disabling the host root instead would
    /// propagate the disabled state to that later-added overlay too (WPF coerces every
    /// descendant's effective IsEnabled), leaving the dialog's own controls dead.
    /// Only children we actually disabled are restored on <see cref="Unblock"/>.
    /// </summary>
    public sealed class WpfInteractionBlocker : IInteractionBlocker
    {
        private readonly UIElement _root;
        private readonly List<UIElement> _disabled = new List<UIElement>();
        private bool _blocked;

        public WpfInteractionBlocker(UIElement root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        public void Block()
        {
            if (_blocked) return;

            _blocked = true;
            _disabled.Clear();

            // Snapshot + disable the children present now (the page and any earlier
            // overlay). WPF propagates IsEnabled down each child's subtree, so one
            // toggle per direct child is enough — no recursion needed.
            if (_root is Panel panel)
            {
                foreach (UIElement child in panel.Children)
                {
                    if (child.IsEnabled)
                    {
                        child.IsEnabled = false;
                        _disabled.Add(child);
                    }
                }
            }
            else if (_root.IsEnabled)
            {
                // Non-Panel host (shouldn't happen via WpfPlatformAdapter): best effort.
                _root.IsEnabled = false;
                _disabled.Add(_root);
            }
        }

        public void Unblock()
        {
            if (!_blocked) return;

            foreach (var element in _disabled)
            {
                try { element.IsEnabled = true; } catch { }
            }

            _disabled.Clear();
            _blocked = false;
        }
    }
}
