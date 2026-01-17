using NekoLib.Navigation.Contracts.Plataform;
using System;
using System.Windows.Forms;

namespace NekoLib.Navigation.Adapters
{
    /// <summary>Blocks user interaction by disabling controls under a root control./summary>
    public sealed class WinFormsInteractionBlocker : IInteractionBlocker
    {
        private readonly Control _root;
        public static explicit operator Control(WinFormsInteractionBlocker host) => host._root;


        public WinFormsInteractionBlocker(Control root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        public void Block() => SetChildrenEnabled(_root, false);
        public void Unblock() => SetChildrenEnabled(_root, true);

        private static void SetChildrenEnabled(Control c, bool enabled)
        {
            // If you want to keep the root enabled but disable children, don’t toggle c.Enabled here.
            foreach (Control child in c.Controls)
                child.Enabled = enabled;
        }
    }
}
