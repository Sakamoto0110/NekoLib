using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.WinForms.Adapters;
using NekoLib.Navigation.WinForms.Hosting;
using NekoLib.Navigation.Wpf.Adapters;
using NekoLib.Navigation.Wpf.Hosting;
using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using Xunit;
using WinFormsPageView = NekoLib.Navigation.WinForms.Hosting.PageView;
using WpfPageView = NekoLib.Navigation.Wpf.Hosting.PageView;

namespace NekoLib.Navigation.Tests.Unit
{
    public class PlatformPageLifecycleTests
    {
        [Fact]
        public void WinFormsPage_VisibilityAndHostCallbacks_ReflectActualAttachment()
        {
            RunSta(() =>
            {
                using (var root = new System.Windows.Forms.Panel())
                using (var page = new WinFormsAttachablePage())
                {
                    var visibility = (IPageVisibility)page;
                    visibility.HidePage();
                    Assert.False(page.Visible);
                    visibility.ShowPage();
                    Assert.True(page.Visible);

                    var host = new WinFormsLayeredPageHostBase(root);
                    host.Attach(page);
                    host.Attach(page);
                    Assert.Equal(1, page.AttachCount);

                    host.Detach(page);
                    host.Detach(page);
                    Assert.Equal(1, page.DetachCount);
                }
            });
        }

        [Fact]
        public void WpfPage_VisibilityAndHostCallbacks_ReflectActualAttachment()
        {
            RunSta(() =>
            {
                var root = new System.Windows.Controls.Grid();
                var page = new WpfAttachablePage();
                var visibility = (IPageVisibility)page;

                visibility.HidePage();
                Assert.Equal(Visibility.Collapsed, page.Visibility);
                visibility.ShowPage();
                Assert.Equal(Visibility.Visible, page.Visibility);

                var host = new WpfLayeredPageHostBase(root);
                host.Attach(page);
                host.Attach(page);
                Assert.Equal(1, page.AttachCount);

                host.Detach(page);
                host.Detach(page);
                Assert.Equal(1, page.DetachCount);
                page.Dispose();
            });
        }

        [Fact]
        public void WinFormsInteractionBlocker_NestedBlocks_RestoreOnlyAtDepthZero()
        {
            RunSta(() =>
            {
                using (var root = new System.Windows.Forms.Panel())
                using (var child = new System.Windows.Forms.Panel())
                using (var nested = new System.Windows.Forms.Button())
                {
                    child.Controls.Add(nested);
                    root.Controls.Add(child);
                    var blocker = new WinFormsInteractionBlocker(root);

                    blocker.Block();
                    blocker.Block();
                    blocker.Unblock();

                    Assert.False(child.Enabled);

                    blocker.Unblock();
                    Assert.True(child.Enabled);
                    Assert.True(nested.Enabled);
                }
            });
        }

        [Fact]
        public void WpfInteractionBlocker_NestedBlocks_RestoreOnlyAtDepthZero()
        {
            RunSta(() =>
            {
                var root = new System.Windows.Controls.Grid();
                var child = new System.Windows.Controls.Button();
                root.Children.Add(child);
                var blocker = new WpfInteractionBlocker(root);

                blocker.Block();
                blocker.Block();
                blocker.Unblock();

                Assert.False(child.IsEnabled);

                blocker.Unblock();
                Assert.True(child.IsEnabled);
            });
        }

        [Fact]
        public void WinFormsInteractionBlocker_LateViewsAndModalStack_RestoreStates()
        {
            RunSta(() =>
            {
                using (var root = new System.Windows.Forms.Panel())
                using (var background = new System.Windows.Forms.Button())
                using (var latePage = new System.Windows.Forms.Panel())
                using (var firstModal = new System.Windows.Forms.Panel())
                using (var secondModal = new System.Windows.Forms.Panel())
                {
                    root.Controls.Add(background);
                    var blocker = new WinFormsInteractionBlocker(root);

                    blocker.Block();
                    Assert.False(background.Enabled);

                    root.Controls.Add(latePage);
                    blocker.OnViewAdded(latePage, isModalSurface: false);
                    Assert.False(latePage.Enabled);

                    root.Controls.Remove(latePage);
                    blocker.OnViewRemoved(latePage);
                    Assert.True(latePage.Enabled);

                    root.Controls.Add(latePage);
                    blocker.OnViewAdded(latePage, isModalSurface: false);
                    Assert.False(latePage.Enabled);

                    root.Controls.Add(firstModal);
                    blocker.OnViewAdded(firstModal, isModalSurface: true);
                    Assert.True(firstModal.Enabled);

                    root.Controls.Add(secondModal);
                    blocker.OnViewAdded(secondModal, isModalSurface: true);
                    Assert.False(firstModal.Enabled);
                    Assert.True(secondModal.Enabled);

                    root.Controls.Remove(secondModal);
                    blocker.OnViewRemoved(secondModal);
                    Assert.True(secondModal.Enabled);
                    Assert.True(firstModal.Enabled);

                    blocker.OnViewRemoved(firstModal);
                    blocker.Unblock();

                    Assert.True(background.Enabled);
                    Assert.True(latePage.Enabled);
                }
            });
        }

        [Fact]
        public void WpfInteractionBlocker_LateViewsAndModalStack_RestoreStates()
        {
            RunSta(() =>
            {
                var root = new System.Windows.Controls.Grid();
                var background = new System.Windows.Controls.Button();
                var latePage = new System.Windows.Controls.Grid();
                var firstModal = new System.Windows.Controls.Grid();
                var secondModal = new System.Windows.Controls.Grid();
                root.Children.Add(background);
                var blocker = new WpfInteractionBlocker(root);

                blocker.Block();
                Assert.False(background.IsEnabled);

                root.Children.Add(latePage);
                blocker.OnViewAdded(latePage, isModalSurface: false);
                Assert.False(latePage.IsEnabled);

                root.Children.Remove(latePage);
                blocker.OnViewRemoved(latePage);
                Assert.True(latePage.IsEnabled);

                root.Children.Add(latePage);
                blocker.OnViewAdded(latePage, isModalSurface: false);
                Assert.False(latePage.IsEnabled);

                root.Children.Add(firstModal);
                blocker.OnViewAdded(firstModal, isModalSurface: true);
                Assert.True(firstModal.IsEnabled);

                root.Children.Add(secondModal);
                blocker.OnViewAdded(secondModal, isModalSurface: true);
                Assert.False(firstModal.IsEnabled);
                Assert.True(secondModal.IsEnabled);

                root.Children.Remove(secondModal);
                blocker.OnViewRemoved(secondModal);
                Assert.True(secondModal.IsEnabled);
                Assert.True(firstModal.IsEnabled);

                blocker.OnViewRemoved(firstModal);
                blocker.Unblock();

                Assert.True(background.IsEnabled);
                Assert.True(latePage.IsEnabled);
            });
        }

        [Fact]
        public void WinFormsHost_CustomIPageView_RemainsInPageLayer()
        {
            RunSta(() =>
            {
                using (var root = new System.Windows.Forms.Panel())
                using (var pageControl = new System.Windows.Forms.Panel())
                using (var overlay = new System.Windows.Forms.Panel())
                {
                    var page = new DirectWinFormsPage(pageControl);
                    var host = new WinFormsLayeredPageHostBase(root);

                    host.Attach(page);
                    host.AddView(overlay);
                    host.BringToFront(page);

                    Assert.True(
                        root.Controls.GetChildIndex(overlay) <
                        root.Controls.GetChildIndex(pageControl));
                }
            });
        }

        [Fact]
        public void WinFormsHost_BringPageToFront_PreservesSurfaceZOrder()
        {
            RunSta(() =>
            {
                using (var root = new System.Windows.Forms.Panel())
                using (var pageControl = new System.Windows.Forms.Panel())
                using (var lowerSurface = new System.Windows.Forms.Panel())
                using (var topSurface = new System.Windows.Forms.Panel())
                {
                    var page = new DirectWinFormsPage(pageControl);
                    var host = new WinFormsLayeredPageHostBase(root);

                    host.Attach(page);
                    host.AddView(lowerSurface);
                    host.AddView(topSurface);
                    host.BringToFront(page);

                    Assert.True(
                        root.Controls.GetChildIndex(topSurface) <
                        root.Controls.GetChildIndex(lowerSurface));
                    Assert.True(
                        root.Controls.GetChildIndex(lowerSurface) <
                        root.Controls.GetChildIndex(pageControl));
                }
            });
        }

        // NAV-003: WPF UserControl overrides Focusable to false, so the old
        // `if (element.Focusable) element.Focus()` host implementation could never
        // focus any shipped view base. Focus must land INSIDE the surface.
        [Fact]
        public void WpfHost_FocusSurface_MovesFocusToFirstFocusableDescendant()
        {
            RunSta(() =>
            {
                var root = new System.Windows.Controls.Grid();
                var host = new WpfLayeredPageHostBase(root);

                var surface = new System.Windows.Controls.UserControl();
                var stack = new System.Windows.Controls.StackPanel();
                var field = new System.Windows.Controls.TextBox();
                var confirm = new System.Windows.Controls.Button();
                stack.Children.Add(field);
                stack.Children.Add(confirm);
                surface.Content = stack;

                Assert.False(surface.Focusable);

                host.AddView(surface);
                host.Focus(surface);

                Assert.True(field.IsFocused);
                Assert.False(confirm.IsFocused);
                Assert.Same(field, System.Windows.Input.FocusManager.GetFocusedElement(root));
            });
        }

        [Fact]
        public void WpfHost_FocusSurface_SkipsDisabledAndCollapsedCandidates()
        {
            RunSta(() =>
            {
                var root = new System.Windows.Controls.Grid();
                var host = new WpfLayeredPageHostBase(root);

                var surface = new System.Windows.Controls.UserControl();
                var stack = new System.Windows.Controls.StackPanel();
                var disabled = new System.Windows.Controls.TextBox { IsEnabled = false };
                var collapsed = new System.Windows.Controls.TextBox
                {
                    Visibility = Visibility.Collapsed
                };
                var usable = new System.Windows.Controls.Button();
                stack.Children.Add(disabled);
                stack.Children.Add(collapsed);
                stack.Children.Add(usable);
                surface.Content = stack;

                host.AddView(surface);
                host.Focus(surface);

                Assert.True(usable.IsFocused);
                Assert.False(disabled.IsFocused);
                Assert.False(collapsed.IsFocused);
            });
        }

        [Fact]
        public void WpfHost_FocusSurfaceWithoutFocusableContent_LeavesFocusUntouched()
        {
            RunSta(() =>
            {
                var root = new System.Windows.Controls.Grid();
                var host = new WpfLayeredPageHostBase(root);

                var surface = new System.Windows.Controls.UserControl
                {
                    Content = new System.Windows.Controls.TextBlock { Text = "no controls" }
                };

                host.AddView(surface);
                host.Focus(surface);

                // Nothing to focus, and nothing unrelated stolen.
                Assert.False(surface.IsFocused);
                Assert.Null(System.Windows.Input.FocusManager.GetFocusedElement(root));
            });
        }

        [Fact]
        public void WpfHost_FocusSurfaceThatIsItselfFocusable_FocusesTheSurface()
        {
            RunSta(() =>
            {
                var root = new System.Windows.Controls.Grid();
                var host = new WpfLayeredPageHostBase(root);

                var surface = new System.Windows.Controls.UserControl
                {
                    Focusable = true,
                    Content = new System.Windows.Controls.TextBlock { Text = "no controls" }
                };

                host.AddView(surface);
                host.Focus(surface);

                Assert.True(surface.IsFocused);
                Assert.Same(surface, System.Windows.Input.FocusManager.GetFocusedElement(root));
            });
        }

        // NAV-004: IViewHost.Focus forwards focus to the surface's first selectable
        // child, so the container never holds focus and its non-bubbling LostFocus
        // never fired. Leave is the subtree-scoped event that actually reports the
        // user moving focus out of the surface.
        [Fact]
        public void WinFormsFocusObserver_FocusMovingWithinSurface_DoesNotReportUnfocus()
        {
            RunFocusScenario((surface, outside, field, confirm, observer) =>
            {
                var unfocusCount = 0;
                using (observer.Track(surface, () => unfocusCount++))
                {
                    field.Focus();
                    System.Windows.Forms.Application.DoEvents();
                    Assert.Equal(0, unfocusCount);

                    confirm.Focus();
                    System.Windows.Forms.Application.DoEvents();
                    Assert.Equal(0, unfocusCount);
                }
            });
        }

        [Fact]
        public void WinFormsFocusObserver_FocusLeavingSurface_ReportsUnfocus()
        {
            RunFocusScenario((surface, outside, field, confirm, observer) =>
            {
                // Mirror the service order: the host focuses the surface first, then
                // the observer is attached.
                surface.Focus();
                System.Windows.Forms.Application.DoEvents();
                Assert.True(surface.ContainsFocus);

                var unfocusCount = 0;
                using (observer.Track(surface, () => unfocusCount++))
                {
                    outside.Focus();
                    System.Windows.Forms.Application.DoEvents();
                    Assert.Equal(1, unfocusCount);
                }
            });
        }

        [Fact]
        public void WinFormsFocusObserver_DisposedSubscription_StopsReportingUnfocus()
        {
            RunFocusScenario((surface, outside, field, confirm, observer) =>
            {
                var unfocusCount = 0;
                var subscription = observer.Track(surface, () => unfocusCount++);

                field.Focus();
                System.Windows.Forms.Application.DoEvents();
                subscription.Dispose();

                outside.Focus();
                System.Windows.Forms.Application.DoEvents();
                Assert.Equal(0, unfocusCount);
            });
        }

        /// <summary>
        /// Real focus transitions need a shown form, so the scenario uses one parked
        /// far off-screen: it never becomes visible to the user but still delivers
        /// genuine WinForms focus events.
        /// </summary>
        private static void RunFocusScenario(
            Action<System.Windows.Forms.UserControl,
                   System.Windows.Forms.Button,
                   System.Windows.Forms.TextBox,
                   System.Windows.Forms.Button,
                   WinFormsFocusObserverAdapter> body)
        {
            RunSta(() =>
            {
                using (var form = new System.Windows.Forms.Form
                {
                    ShowInTaskbar = false,
                    StartPosition = System.Windows.Forms.FormStartPosition.Manual,
                    Location = new System.Drawing.Point(-32000, -32000),
                    Size = new System.Drawing.Size(320, 240)
                })
                {
                    var root = new System.Windows.Forms.Panel
                    {
                        Dock = System.Windows.Forms.DockStyle.Fill
                    };
                    form.Controls.Add(root);

                    var outside = new System.Windows.Forms.Button
                    {
                        Text = "outside",
                        Location = new System.Drawing.Point(10, 150)
                    };
                    root.Controls.Add(outside);

                    var surface = new System.Windows.Forms.UserControl
                    {
                        Size = new System.Drawing.Size(200, 100)
                    };
                    var field = new System.Windows.Forms.TextBox
                    {
                        Location = new System.Drawing.Point(10, 10),
                        Width = 120
                    };
                    var confirm = new System.Windows.Forms.Button
                    {
                        Text = "OK",
                        Location = new System.Drawing.Point(10, 50)
                    };
                    surface.Controls.Add(field);
                    surface.Controls.Add(confirm);
                    root.Controls.Add(surface);

                    form.Show();
                    System.Windows.Forms.Application.DoEvents();

                    try
                    {
                        body(surface, outside, field, confirm, new WinFormsFocusObserverAdapter());
                    }
                    finally
                    {
                        form.Close();
                    }
                }
            });
        }

        private static void RunSta(Action action)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { failure = ex; }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure != null)
                ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private sealed class WinFormsAttachablePage : WinFormsPageView, IHostAttachable
        {
            public int AttachCount { get; private set; }
            public int DetachCount { get; private set; }

            public void OnAttach(IPageHost host) => AttachCount++;
            public void OnDetach() => DetachCount++;
        }

        private sealed class WpfAttachablePage : WpfPageView, IHostAttachable
        {
            public int AttachCount { get; private set; }
            public int DetachCount { get; private set; }

            public void OnAttach(IPageHost host) => AttachCount++;
            public void OnDetach() => DetachCount++;
        }

        private sealed class DirectWinFormsPage : IPageView
        {
            private readonly System.Windows.Forms.Control _control;

            public DirectWinFormsPage(
                System.Windows.Forms.Control control)
            {
                _control = control;
            }

            public string Name => nameof(DirectWinFormsPage);
            public object NativeView => _control;
            public bool IsDisposed { get; private set; }
            public void Dispose() => IsDisposed = true;
        }
    }
}
