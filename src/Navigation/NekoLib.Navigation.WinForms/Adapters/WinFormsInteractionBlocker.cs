using NekoLib.Navigation.Contracts.Platform;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace NekoLib.Navigation.WinForms.Adapters
{
    /// <summary>
    /// Blocks user interaction by disabling every control beneath a root control.
    /// Only controls that were enabled when blocking began are restored on unblock, so
    /// controls that were already disabled keep their original state.
    /// </summary>
    public sealed class WinFormsInteractionBlocker : IInteractionBlocker
    {
        private readonly Control _root;
        private readonly List<Control> _disabled = new List<Control>();
        private bool _blocked;

        public static explicit operator Control(WinFormsInteractionBlocker host) => host._root;

        public WinFormsInteractionBlocker(Control root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        public void Block()
        {
            if (_blocked)
                return;

            _blocked = true;
            _disabled.Clear();

            // Disable the entire subtree (not just the direct children) so inputs nested
            // inside panels / group boxes are also blocked (D-2). Remember only the
            // controls we actually toggled so Unblock can restore the prior state.
            DisableSubtree(_root);
        }

        public void Unblock()
        {
            if (!_blocked)
                return;

            foreach (var control in _disabled)
            {
                try { control.Enabled = true; } catch { }
            }

            _disabled.Clear();
            _blocked = false;
        }

        private void DisableSubtree(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                if (child.Enabled)
                {
                    child.Enabled = false;
                    _disabled.Add(child);
                }

                DisableSubtree(child);
            }
        }
    }
}
