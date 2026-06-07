using NekoLib.Navigation.Contracts.Pages;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace NekoLib.Navigation.Wpf
{
    /// <summary>
    /// Hosts content pages and transient surfaces (toasts, dialogs, prompts, loading mask)
    /// inside a single Panel. Content pages are kept at the bottom of the z-order so the
    /// transient surfaces stay visible above them.
    /// </summary>
    public class PageHost : IPageHost, IViewHost
    {
        private const int PageZIndex = 0;
        private const int SurfaceZIndex = 100;

        private readonly Panel _panel;

        public PageHost(Panel panel)
        {
            _panel = panel ?? throw new ArgumentNullException(nameof(panel));
        }

        // ---------------------------------------------------------------------
        // IPageHost (content pages)
        // ---------------------------------------------------------------------

        public void Attach(IPageView page)
        {
            if (!(page?.NativeView is UIElement control))
                throw new InvalidOperationException("IPageView.NativeView must be a WPF UIElement.");

            StretchToHost(control);

            if (!_panel.Children.Contains(control))
                _panel.Children.Add(control);

            control.Visibility = Visibility.Visible;
            Panel.SetZIndex(control, PageZIndex);
        }

        public void Detach(IPageView page)
        {
            if (page?.NativeView is UIElement control && _panel.Children.Contains(control))
                _panel.Children.Remove(control);
        }

        public void BringToFront(IPageView page)
        {
            if (page?.NativeView is UIElement control && _panel.Children.Contains(control))
            {
                // Promote among content pages, then re-assert surfaces so they stay on top.
                Panel.SetZIndex(control, PageZIndex + 1);
                RestackSurfaces();
            }
        }

        // ---------------------------------------------------------------------
        // IViewHost (transient surfaces: toasts, dialogs, prompts, mask)
        // ---------------------------------------------------------------------

        public void AddView(object view)
        {
            if (!(view is UIElement control))
                return;

            StretchToHost(control);

            if (!_panel.Children.Contains(control))
                _panel.Children.Add(control);

            control.Visibility = Visibility.Visible;
            Panel.SetZIndex(control, SurfaceZIndex);
        }

        public void RemoveView(object view)
        {
            if (view is UIElement control && _panel.Children.Contains(control))
                _panel.Children.Remove(control);
        }

        public void BringToFront(object view)
        {
            if (view is UIElement control)
                Panel.SetZIndex(control, SurfaceZIndex);
        }

        public void Focus(object view)
        {
            if (view is UIElement control)
                control.Focus();
        }

        private static void StretchToHost(UIElement control)
        {
            if (control is FrameworkElement fe)
            {
                fe.HorizontalAlignment = HorizontalAlignment.Stretch;
                fe.VerticalAlignment = VerticalAlignment.Stretch;
            }
        }

        private void RestackSurfaces()
        {
            foreach (var child in _panel.Children.OfType<UIElement>())
            {
                if (Panel.GetZIndex(child) >= SurfaceZIndex)
                    Panel.SetZIndex(child, SurfaceZIndex);
            }
        }
    }
}
