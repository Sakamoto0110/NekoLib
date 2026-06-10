using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NekoLib.Navigation.Contracts.Pages;

namespace NekoLib.Navigation.Wpf.Hosting
{
    /// <summary>
    /// Managed host that layers content pages and transient surfaces (toasts, dialogs,
    /// prompts, the loading mask) inside a single Panel. Pages stay at the bottom of
    /// the Z-order; every other child is kept above them so surfaces remain visible.
    /// Mirrors WinFormsLayeredPageHostBase.
    /// </summary>
    public class WpfLayeredPageHostBase : IPageHost, IViewHost
    {
        // Z-index buckets: pages live below, overlays live above. Concrete values
        // are arbitrary as long as overlays > pages.
        private const int PageZIndex = 0;
        private const int OverlayZIndex = 1000;

        protected Panel Root { get; }

        public WpfLayeredPageHostBase(Panel root)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
        }

        // ---------------------------------------------------------------------
        // IPageHost (content pages)
        // ---------------------------------------------------------------------

        public virtual void Attach(IPageView page)
        {
            if (page?.NativeView is not UIElement element)
                throw new InvalidOperationException("Page NativeView must be a WPF UIElement.");

            if (!Root.Children.Contains(element))
                Root.Children.Add(element);

            if (element is FrameworkElement fe)
            {
                fe.HorizontalAlignment = HorizontalAlignment.Stretch;
                fe.VerticalAlignment = VerticalAlignment.Stretch;
            }

            element.Visibility = Visibility.Visible;
            Panel.SetZIndex(element, PageZIndex);

            if (page is IHostAttachable attachable)
                attachable.OnAttach(this);
        }

        public virtual void Detach(IPageView page)
        {
            if (page?.NativeView is UIElement element && Root.Children.Contains(element))
                Root.Children.Remove(element);

            if (page is IHostAttachable attachable)
                attachable.OnDetach();
        }

        public virtual void BringToFront(IPageView page)
        {
            // Pages are explicitly the bottom layer — promoting a page to the top
            // would hide live overlays. Re-asserts the page layer instead.
            if (page?.NativeView is UIElement element)
                Panel.SetZIndex(element, PageZIndex);
        }

        // ---------------------------------------------------------------------
        // IViewHost (transient surfaces)
        // ---------------------------------------------------------------------

        public virtual void AddView(object view)
        {
            if (view is not UIElement element) return;

            if (!Root.Children.Contains(element))
                Root.Children.Add(element);

            // Do NOT force alignment here. Each overlay base sets its own intent in
            // its ctor (dialog/prompt = Center, toast = bottom-right, popover = the
            // designer placement); forcing Stretch afterwards would clobber it. The
            // loading mask sets no alignment and fills naturally — Stretch is already
            // the FrameworkElement default.
            element.Visibility = Visibility.Visible;
            Panel.SetZIndex(element, OverlayZIndex);
        }

        public virtual void RemoveView(object view)
        {
            if (view is UIElement element && Root.Children.Contains(element))
                Root.Children.Remove(element);
        }

        public virtual void BringToFront(object view)
        {
            if (view is UIElement element)
            {
                // Find the current highest overlay z-index and go one above it.
                var max = Root.Children
                    .OfType<UIElement>()
                    .Select(Panel.GetZIndex)
                    .DefaultIfEmpty(OverlayZIndex)
                    .Max();

                Panel.SetZIndex(element, max + 1);
            }
        }

        public virtual void Focus(object view)
        {
            if (view is UIElement element && element.Focusable)
                element.Focus();
        }
    }
}
