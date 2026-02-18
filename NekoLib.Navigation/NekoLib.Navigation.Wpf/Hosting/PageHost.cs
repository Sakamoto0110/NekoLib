using PageNav.Contracts.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PageNav.Wpf
{
    public class PageHost : IPageHost
    {
        private readonly Panel _panel;

        public PageHost(Panel panel)
        {
            _panel = panel;
        }

        public void AddView(object view)
        {
            if(view is UIElement elem)
                _panel.Children.Add(elem);
        }

        public void RemoveView(object view)
        {
            if(view is UIElement elem)
                _panel.Children.Remove(elem);
        }

        public void BringToFront(object view)
        {
            // WPF z-index
            if(view is UIElement elem)
                Panel.SetZIndex(elem, 9999);
        }

        public void Focus(object view)
        {
            if(view is UIElement elem)
                elem.Focus();
        }

        public void Attach(IPageView page)
        {
            if(!(page?.NativeView is UIElement control)) throw new InvalidOperationException("IPageView.NativeView must be a UIElement Control.");

          

            if(control is FrameworkElement fe)
            {
                fe.HorizontalAlignment = HorizontalAlignment.Stretch;
                fe.VerticalAlignment = VerticalAlignment.Stretch;
            }

            if(!_panel.Children.Contains(control))
                _panel.Children.Add(control);

            page.OnAttach(this);
        }
        public void Detach(IPageView page)
        {
            if(!(page?.NativeView is UIElement control))
                throw new InvalidOperationException("IPageView.NativeView must be a UIElement Control.");

            _panel.Children.Remove(control);
            page.OnDetach();
        }

        public void BringToFront(IPageView page)
        {
            if(!(page?.NativeView is UIElement control))
                throw new InvalidOperationException("IPageView.NativeView must be a UIElement Control.");
            if(_panel.Children.Contains(control))
            {
                _panel.Children.Remove(control);
                _panel.Children.Add(control);
            }
        }
    }
}
