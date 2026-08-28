using NekoLib.Navigation.Contracts.Runtime;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace NekoLib.Navigation.WinForms.Adapters
{
    /// <summary>
    /// Observes mouse, keyboard, control-tree, and form activity beneath one
    /// WinForms root and reports it through the idle interaction contract.
    /// </summary>
    public sealed class WinFormsInteractionObserver :
     IInteractionObserverService,
     IDisposable
    {
        private readonly Control _root;

        // Tracks ONLY controls we have hooked
        private readonly HashSet<Control> _hooked = new();

        /// <inheritdoc />
        public event Action? InteractionDetected;

        event Action? IInteractionObserverService.InteractionDetected
        {
            add { if (value != null) InteractionDetected += value; }
            remove { if (value != null) InteractionDetected -= value; }
        }

        /// <summary>Initializes and recursively hooks one WinForms root.</summary>
        /// <param name="root">Root control whose current and future descendants are observed.</param>
        public WinFormsInteractionObserver(Control root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));

            // Hook the existing subtree. HookSingle also subscribes to each
            // container's ControlAdded/ControlRemoved, so future controls added
            // anywhere beneath root (not just direct children) get hooked too.
            Hook(_root);
        }

        // ------------------------------------------------------------
        // Hooking logic
        // ------------------------------------------------------------

        private void Hook(Control control)
        {
            HookSingle(control);

            foreach (Control child in control.Controls)
                Hook(child);
        }

        private void HookSingle(Control control)
        {
            // If we already hooked this control, stop.
            if (!_hooked.Add(control))
                return;

            // Observe structural changes so deep/late controls are tracked too.
            control.ControlAdded += OnControlAdded;
            control.ControlRemoved += OnControlRemoved;

            control.MouseDown += OnInteraction;
            control.KeyDown += OnInteraction;

            if (control is ButtonBase btn)
                btn.Click += OnInteraction;

            if (control is TextBoxBase tb)
                tb.TextChanged += OnInteraction;
        }

        // ------------------------------------------------------------
        // Dynamic controls
        // ------------------------------------------------------------

        private void OnControlAdded(object sender, ControlEventArgs e)
        {
            // A control (and its current subtree) was added beneath a hooked
            // container. Hook it recursively.
            Hook(e.Control);
        }

        private void OnControlRemoved(object sender, ControlEventArgs e)
        {
            // A control (and its subtree) was removed; release our handlers so
            // we do not leak references to detached controls.
            UnhookTree(e.Control);
        }

        // ------------------------------------------------------------
        // Interaction callback
        // ------------------------------------------------------------

        private void OnInteraction(object sender, EventArgs e)
        {
            InteractionDetected?.Invoke();
        }

        // ------------------------------------------------------------
        // Cleanup
        // ------------------------------------------------------------

        /// <summary>Unhooks the root, its descendants, and control-tree change handlers.</summary>
        public void Dispose()
        {
            UnhookTree(_root);
            _hooked.Clear();
        }

        private void UnhookTree(Control control)
        {
            UnhookSingle(control);

            foreach (Control child in control.Controls)
                UnhookTree(child);
        }

        private void UnhookSingle(Control control)
        {
            // Only detach if WE hooked it.
            if (!_hooked.Remove(control))
                return;

            control.ControlAdded -= OnControlAdded;
            control.ControlRemoved -= OnControlRemoved;

            control.MouseDown -= OnInteraction;
            control.KeyDown -= OnInteraction;

            if (control is ButtonBase btn)
                btn.Click -= OnInteraction;

            if (control is TextBoxBase tb)
                tb.TextChanged -= OnInteraction;
        }
    }
}
