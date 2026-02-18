using PageNav.Contracts.Plataform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PageNav.Wpf.Adapters
{
    public class InteractionBlocker : IInteractionBlocker
    {
        public void Block(object view)
        {
            if(view is UIElement e)
                e.IsEnabled = false;
        }

        public void Unblock(object view)
        {
            if(view is UIElement e)
                e.IsEnabled = true;
        }
    }
}
