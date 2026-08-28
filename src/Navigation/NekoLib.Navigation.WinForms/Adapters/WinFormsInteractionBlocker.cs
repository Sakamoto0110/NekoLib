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
    public sealed class WinFormsInteractionBlocker :
        IPageAwareInteractionBlocker
    {
        private readonly Control _root;
        private readonly Dictionary<Control, bool> _originalStates =
            new Dictionary<Control, bool>();
        private readonly List<Control> _modalStack = new List<Control>();
        private int _blockDepth;

        /// <summary>Initializes a blocker for one WinForms navigation root.</summary>
        /// <param name="root">Root whose child controls are tracked.</param>
        public WinFormsInteractionBlocker(Control root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        /// <inheritdoc />
        public void Block()
        {
            _blockDepth++;
            if (_blockDepth != 1)
                return;

            _originalStates.Clear();
            _modalStack.Clear();

            // Disabling each direct child disables its effective subtree too.
            // Remember only controls we actually toggle so descendants do not
            // accidentally persist an inherited disabled state as a local one.
            try
            {
                DisableSubtree(_root);
            }
            catch
            {
                RestoreDisabledControls();
                _blockDepth = 0;
                throw;
            }
        }

        /// <inheritdoc />
        public void Unblock()
        {
            if (_blockDepth == 0)
                return;

            _blockDepth--;
            if (_blockDepth != 0)
                return;

            RestoreDisabledControls();
        }

        /// <inheritdoc />
        public void OnViewAdded(object view, bool isModalSurface)
        {
            if (_blockDepth == 0 || !(view is Control control))
                return;

            CaptureState(control);

            if (!isModalSurface)
            {
                _modalStack.Remove(control);
                Disable(control);
                return;
            }

            if (_modalStack.Count > 0)
                Disable(_modalStack[_modalStack.Count - 1]);

            _modalStack.Remove(control);
            _modalStack.Add(control);
            Restore(control);
        }

        /// <inheritdoc />
        public void OnViewRemoved(object view)
        {
            if (!(view is Control control))
                return;

            var wasTop =
                _modalStack.Count > 0 &&
                ReferenceEquals(_modalStack[_modalStack.Count - 1], control);

            _modalStack.Remove(control);
            Restore(control);
            _originalStates.Remove(control);

            if (_blockDepth > 0 && wasTop && _modalStack.Count > 0)
                Restore(_modalStack[_modalStack.Count - 1]);
        }

        private void RestoreDisabledControls()
        {
            foreach (var pair in _originalStates)
            {
                try { pair.Key.Enabled = pair.Value; } catch { }
            }

            _originalStates.Clear();
            _modalStack.Clear();
        }

        private void DisableSubtree(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                CaptureState(child);
                Disable(child);
            }
        }

        private void CaptureState(Control control)
        {
            if (!_originalStates.ContainsKey(control))
                _originalStates.Add(control, control.Enabled);
        }

        private static void Disable(Control control)
        {
            if (control.Enabled)
                control.Enabled = false;
        }

        private void Restore(Control control)
        {
            if (_originalStates.TryGetValue(control, out var enabled))
                control.Enabled = enabled;
        }
    }
}
