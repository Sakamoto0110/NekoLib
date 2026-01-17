using NekoLib.Navigation.Contracts.Runtime;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace NekoLib.Navigation.Adapters
{
     public sealed class WinFormsInteractionObserver :
      IInteractionObserverService,
      IDisposable
    {
        private readonly Control _root;

        // Tracks ONLY controls we have hooked
        private readonly HashSet<Control> _hooked = new();

        public event Action InteractionDetected;

        public WinFormsInteractionObserver(Control root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));

            // Hook existing controls
            Hook(_root);

            // Observe future controls
            _root.ControlAdded += OnRootControlAdded;
        }

        // ------------------------------------------------------------
        // Hooking logic
        // ------------------------------------------------------------

        private void Hook(Control control)
        {
            HookSingle(control);

            foreach(Control child in control.Controls)
                Hook(child);
        }

        private void HookSingle(Control control)
        {
            // 1️⃣ If we already hooked this control, STOP
            if(!_hooked.Add(control))
                return;

            // 2️⃣ Attach OUR handlers (only now we add to cache)
            control.MouseDown += OnInteraction;
            control.KeyDown += OnInteraction;

            if(control is ButtonBase btn)
                btn.Click += OnInteraction;

            if(control is TextBoxBase tb)
                tb.TextChanged += OnInteraction;
        }

        // ------------------------------------------------------------
        // Dynamic controls
        // ------------------------------------------------------------

        private void OnRootControlAdded(object sender, ControlEventArgs e)
        {
            // New control added anywhere under root
            Hook(e.Control);
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

        public void Dispose()
        {
            _root.ControlAdded -= OnRootControlAdded;

            UnhookTree(_root);
            _hooked.Clear();
        }

        private void UnhookTree(Control control)
        {
            UnhookSingle(control);

            foreach(Control child in control.Controls)
                UnhookTree(child);
        }

        private void UnhookSingle(Control control)
        {
            // 1️⃣ Only detach if WE hooked it
            if(!_hooked.Remove(control))
                return;

            control.MouseDown -= OnInteraction;
            control.KeyDown -= OnInteraction;

            if(control is ButtonBase btn)
                btn.Click -= OnInteraction;

            if(control is TextBoxBase tb)
                tb.TextChanged -= OnInteraction;
        }
    }


}
