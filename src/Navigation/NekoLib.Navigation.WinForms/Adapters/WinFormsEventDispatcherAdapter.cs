using NekoLib.Navigation.Contracts.Platform;
using System;
using System.Windows.Forms;

namespace NekoLib.Navigation.WinForms.Adapters
{
    public sealed class WinFormsEventDispatcherAdapter
     : IEventDispatcherAdapter
    {
        private readonly Control _root;

        public WinFormsEventDispatcherAdapter(Control root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        public void Invoke(Action action)
        {
            if(_root.InvokeRequired)
                _root.Invoke(action);
            else
                action();
        }

        public void BeginInvoke(Action action)
        {
            _root.BeginInvoke(action);
        }
    }

}
