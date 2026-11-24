using NekoLib.Navigation.Contracts.Platform;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace NekoLib.Navigation.WinForms.Adapters
{
    /// <summary>
    /// Blocks user interaction by disabling every control beneath a root control.
    /// Holds tracked background views disabled across Enabled-binding refreshes.
    /// Restores captured state and current binding values on unblock, preserving
    /// each tracked Enabled binding and its original data-source update mode.
    /// </summary>
    public sealed class WinFormsInteractionBlocker :
        IPageAwareInteractionBlocker
    {
        private readonly Control _root;
        private readonly Dictionary<Control, bool> _originalStates =
            new Dictionary<Control, bool>();
        private readonly Dictionary<Control, EnabledBindingState> _enabledBindings =
            new Dictionary<Control, EnabledBindingState>();
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
            control.EnabledChanged -= OnEnabledChanged;
            try { Restore(control); }
            finally
            {
                ReleaseEnabledBinding(control);
                _originalStates.Remove(control);
            }

            if (_blockDepth > 0 && wasTop && _modalStack.Count > 0)
                Restore(_modalStack[_modalStack.Count - 1]);
        }

        private void RestoreDisabledControls()
        {
            foreach (var control in _originalStates.Keys)
                control.EnabledChanged -= OnEnabledChanged;

            foreach (var pair in _originalStates)
            {
                try { Restore(pair.Key); } catch { }
            }

            foreach (var state in _enabledBindings.Values)
            {
                try { state.Binding.DataSourceUpdateMode = state.UpdateMode; } catch { }
            }
            _enabledBindings.Clear();

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
            {
                _originalStates.Add(control, control.Enabled);
                control.EnabledChanged += OnEnabledChanged;
            }
            CaptureEnabledBinding(control);
        }

        private void OnEnabledChanged(object? sender, EventArgs e)
        {
            if (_blockDepth == 0 || !(sender is Control control) || !control.Enabled || !_originalStates.ContainsKey(control))
                return;
            if (_modalStack.Count > 0 && ReferenceEquals(_modalStack[_modalStack.Count - 1], control))
                return;

            _originalStates[control] = true;
            Disable(control);
        }

        private void Disable(Control control)
        {
            CaptureEnabledBinding(control);
            if (control.Enabled)
                control.Enabled = false;
        }

        private void Restore(Control control)
        {
            if (!_originalStates.TryGetValue(control, out var enabled)) return;
            CaptureEnabledBinding(control);
            control.Enabled = enabled;
            control.DataBindings[nameof(Control.Enabled)]?.ReadValue();
        }

        private void CaptureEnabledBinding(Control control)
        {
            var binding = control.DataBindings[nameof(Control.Enabled)];
            if (_enabledBindings.TryGetValue(control, out var previous))
            {
                if (ReferenceEquals(previous.Binding, binding)) return;
                ReleaseEnabledBinding(control);
            }
            if (binding == null) return;
            _enabledBindings.Add(control, new EnabledBindingState(binding));
            // Enabled is framework-owned while blocked; forcing it false must
            // not write that temporary value back into an application model.
            binding.DataSourceUpdateMode = DataSourceUpdateMode.Never;
        }

        private void ReleaseEnabledBinding(Control control)
        {
            if (!_enabledBindings.TryGetValue(control, out var state)) return;
            _enabledBindings.Remove(control);
            state.Binding.DataSourceUpdateMode = state.UpdateMode;
        }

        private sealed class EnabledBindingState
        {
            internal readonly Binding Binding;
            internal readonly DataSourceUpdateMode UpdateMode;
            internal EnabledBindingState(Binding binding)
            {
                Binding = binding;
                UpdateMode = binding.DataSourceUpdateMode;
            }
        }
    }
}
