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
