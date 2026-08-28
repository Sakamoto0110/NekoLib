using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Toolkit.Abstractions;
using NekoLib.Navigation.Wpf.Toolkit;

namespace NekoLib.Navigation.Wpf.Hosting
{
    /// <summary>
    /// Managed host that layers content pages and transient surfaces (toasts, dialogs,
    /// prompts, the loading mask) inside a single Panel. Pages stay at the bottom of
    /// the Z-order; every other child is kept above them so surfaces remain visible.
    /// Mirrors WinFormsLayeredPageHostBase.
    /// </summary>
    public class WpfLayeredPageHostBase : IPageHost, IViewHost, INavigationToolkit
    {
        // Z-index buckets: pages live below, overlays live above. Concrete values
        // are arbitrary as long as overlays > pages.
        private const int PageZIndex = 0;
        private const int OverlayZIndex = 1000;

        /// <summary>Gets the native panel that owns every page and overlay element.</summary>
        protected Panel Root { get; }
        private readonly WpfNavigationToolkit _toolkit;

        /// <summary>Initializes a layered host over one WPF panel.</summary>
        /// <param name="root">Native panel that owns the attached elements.</param>
        public WpfLayeredPageHostBase(Panel root)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            _toolkit = new WpfNavigationToolkit(root);
        }

        // ---------------------------------------------------------------------
        // INavigationToolkit
        //
        // NAV-010: mirrors WinFormsLayeredPageHostBase. PageNavBootstrap registers
        // the host with a `host as INavigationToolkit` probe, so an adapter that
        // does not implement it simply leaves the toolkit unregistered.
        // ---------------------------------------------------------------------

        /// <inheritdoc />
        public INavigationSurface Surface => _toolkit.Surface;

        /// <inheritdoc />
        public void FocusSurface() => _toolkit.FocusSurface();

        // ---------------------------------------------------------------------
        // IPageHost (content pages)
        // ---------------------------------------------------------------------

        /// <inheritdoc />
        public virtual void Attach(IPageView page)
        {
            if (page?.NativeView is not UIElement element)
                throw new InvalidOperationException("Page NativeView must be a WPF UIElement.");

            var added = !Root.Children.Contains(element);
            if (added)
                Root.Children.Add(element);

            if (element is FrameworkElement fe)
            {
                fe.HorizontalAlignment = HorizontalAlignment.Stretch;
                fe.VerticalAlignment = VerticalAlignment.Stretch;
            }

            element.Visibility = Visibility.Visible;
            Panel.SetZIndex(element, PageZIndex);

            if (added && page is IHostAttachable attachable)
                attachable.OnAttach(this);
        }

        /// <inheritdoc />
        public virtual void Detach(IPageView page)
        {
            if (page?.NativeView is not UIElement element || !Root.Children.Contains(element))
                return;

            Root.Children.Remove(element);

            if (!Root.Children.Contains(element) && page is IHostAttachable attachable)
                attachable.OnDetach();
        }

        /// <inheritdoc />
        public virtual void BringToFront(IPageView page)
        {
            if (page?.NativeView is not UIElement element)
                return;

            // Pages are explicitly the bottom layer — promoting one to the top would
            // hide live overlays. NAV-009(c): this used to re-assert the same constant
            // for every page, so two simultaneously attached pages could not be
            // ordered at all, while WinForms genuinely reorders them. Order within the
            // page band instead, staying strictly below OverlayZIndex.
            var top = Root.Children
                .OfType<UIElement>()
                .Select(Panel.GetZIndex)
                .Where(z => z < OverlayZIndex)
                .DefaultIfEmpty(PageZIndex)
                .Max();

            Panel.SetZIndex(element, Math.Min(top + 1, OverlayZIndex - 1));
        }

        // ---------------------------------------------------------------------
        // IViewHost (transient surfaces)
        // ---------------------------------------------------------------------

        /// <inheritdoc />
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

        /// <inheritdoc />
        public virtual void RemoveView(object view)
        {
            if (view is UIElement element && Root.Children.Contains(element))
                Root.Children.Remove(element);
        }

        /// <inheritdoc />
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

        /// <summary>
        /// Places keyboard focus <em>inside</em> the surface.
        /// <para>
        /// Every view base this host is asked to focus derives from
        /// <see cref="UserControl"/>, which overrides <see cref="UIElement.Focusable"/>
        /// to <c>false</c>. Calling <see cref="UIElement.Focus"/> on the surface is
        /// therefore a guaranteed no-op, which left dialogs and prompts without
        /// keyboard focus and left popovers unable to observe focus loss. Move focus
        /// to the first focusable element within the surface instead, and fall back
        /// to the surface itself only when it can genuinely take focus.
        /// </para>
        /// </summary>
        public virtual void Focus(object view)
        {
            if (view is not UIElement element)
                return;

            // The service focuses a surface immediately after adding it, before WPF
            // has laid it out. Nothing inside an unrendered element can take keyboard
            // focus, so also retry once the surface is loaded. The retry is skipped
            // when focus already reached the surface: Loaded runs at a higher
            // dispatcher priority than Input, so a view that focuses its own control
            // from OnShownAsync still wins.
            if (element is FrameworkElement frameworkElement && !frameworkElement.IsLoaded)
            {
                RoutedEventHandler? onLoaded = null;
                onLoaded = (_, __) =>
                {
                    frameworkElement.Loaded -= onLoaded;

                    if (!frameworkElement.IsKeyboardFocusWithin)
                        FocusInto(element);
                };

                frameworkElement.Loaded += onLoaded;
            }

            FocusInto(element);
        }

        private static void FocusInto(UIElement element)
        {
            // Honours TabIndex and the surface's own tab order, but only resolves
            // once the element participates in a live, rendered visual tree.
            if (element is FrameworkElement frameworkElement &&
                frameworkElement.MoveFocus(
                    new TraversalRequest(FocusNavigationDirection.First)))
            {
                return;
            }

            // Before layout — and whenever traversal declines — resolve the first
            // focusable descendant directly so the surface still receives focus.
            var target = FindFirstFocusable(element);
            if (target != null)
            {
                target.Focus();
                return;
            }

            // A surface with no focusable content at all: focus it directly when the
            // subclass opted in, otherwise leave focus untouched rather than stealing
            // it to an unrelated element.
            if (element.Focusable)
                element.Focus();
        }

        private static UIElement? FindFirstFocusable(DependencyObject root)
        {
            var queue = new Queue<DependencyObject>();
            EnqueueChildren(root, queue);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                if (current is UIElement candidate &&
                    candidate.Focusable &&
                    candidate.IsEnabled &&
                    candidate.Visibility == Visibility.Visible)
                {
                    return candidate;
                }

                EnqueueChildren(current, queue);
            }

            return null;
        }

        // The logical tree is authoritative for author-supplied content and is
        // populated before layout; the visual tree covers templated content once the
        // surface has been rendered.
        private static void EnqueueChildren(DependencyObject node, Queue<DependencyObject> queue)
        {
            foreach (var child in LogicalTreeHelper.GetChildren(node))
            {
                if (child is DependencyObject logicalChild)
                    queue.Enqueue(logicalChild);
            }

            if (node is not Visual && node is not System.Windows.Media.Media3D.Visual3D)
                return;

            var count = VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < count; i++)
                queue.Enqueue(VisualTreeHelper.GetChild(node, i));
        }
    }
}
