using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Toolkit.Models;
using NekoLib.Navigation.WinForms.Toolkit;
using NekoLib.Navigation.Wpf.Hosting;
using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using Xunit;
using WpfPageView = NekoLib.Navigation.Wpf.Hosting.PageView;

namespace NekoLib.Navigation.Tests.Unit
{
    /// <summary>
    /// NAV-009. The surface DPI read must have no side effect and must survive
    /// teardown (a), the WPF surface bases must let a subclass extend disposal (b),
    /// and the WPF host must be able to order two attached pages (c).
    /// </summary>
    public class SurfaceToolkitAndErgonomicsTests
    {
        // NAV-009(a): Scale used to call Control.CreateGraphics(), which realizes the
        // host's window handle as a side effect.
        [Fact]
        public void Scale_UnrealizedHost_DoesNotCreateTheHandle()
        {
            RunSta(() =>
            {
                using (var host = new System.Windows.Forms.Panel())
                {
                    Assert.False(host.IsHandleCreated);

                    var scale = new WinFormsNavigationSurface(host).Scale;

                    Assert.False(host.IsHandleCreated);
                    Assert.True(scale > 0f);
                }
            });
        }

        // NAV-009(a): and it used to throw ObjectDisposedException once the host was
        // disposed, so an anchor consumer could not read it during teardown.
        [Fact]
        public void Scale_DisposedHost_StillReportsInsteadOfThrowing()
        {
            RunSta(() =>
            {
                var host = new System.Windows.Forms.Panel();
                var surface = new WinFormsNavigationSurface(host);
                var before = surface.Scale;

                host.Dispose();

                Assert.Equal(before, surface.Scale);
            });
        }

        [Fact]
        public void ResolveAnchor_UnrealizedHost_DoesNotCreateTheHandle()
        {
            RunSta(() =>
            {
                using (var host = new System.Windows.Forms.Panel { Width = 400, Height = 200 })
                {
                    var surface = new WinFormsNavigationSurface(host);

                    var bottomRight = surface.ResolveAnchor(SurfaceAnchor.BottomRight);

                    Assert.False(host.IsHandleCreated);
                    Assert.Equal(host.ClientRectangle.Width, bottomRight.X);
                    Assert.Equal(host.ClientRectangle.Height, bottomRight.Y);
                }
            });
        }

        // NAV-009(b): the WPF surface bases exposed a non-virtual Dispose(), so a
        // subclass could not extend disposal the way the WinForms Dispose(bool)
        // pattern allows.
        [Fact]
        public void WpfSurfaceBases_Dispose_CanBeExtendedBySubclasses()
        {
            RunSta(() =>
            {
                var dialog = new ExtendedDialog();
                dialog.Dispose();

                Assert.True(dialog.ExtraDisposalRan);
                Assert.True(dialog.IsDisposed);

                var toast = new ExtendedToast();
                toast.Dispose();

                Assert.True(toast.ExtraDisposalRan);
                Assert.True(toast.IsDisposed);
            });
        }

        // NAV-009(c): BringToFront(IPageView) assigned the same constant to every
        // page, so two simultaneously attached pages could not be ordered.
        [Fact]
        public void WpfHost_BringPageToFront_OrdersPagesBelowTheOverlayBand()
        {
            RunSta(() =>
            {
                var root = new System.Windows.Controls.Grid();
                var host = new WpfLayeredPageHostBase(root);

                var first = new ZOrderProbePage();
                var second = new ZOrderProbePage();
                host.Attach(first);
                host.Attach(second);

                host.BringToFront(second);
                host.BringToFront(first);

                var firstZ = System.Windows.Controls.Panel.GetZIndex(first);
                var secondZ = System.Windows.Controls.Panel.GetZIndex(second);

                Assert.True(
                    firstZ > secondZ,
                    $"The page brought to front must sit above the other one (first={firstZ}, second={secondZ}).");

                var overlay = new System.Windows.Controls.Button();
                host.AddView(overlay);
                Assert.True(
                    System.Windows.Controls.Panel.GetZIndex(overlay) > firstZ,
                    "Pages must stay strictly below the overlay band.");

                first.Dispose();
                second.Dispose();
            });
        }

        private sealed class ZOrderProbePage : WpfPageView
        {
        }

        private sealed class ExtendedDialog : DialogViewBase
        {
            public bool ExtraDisposalRan { get; private set; }

            public override void Dispose()
            {
                ExtraDisposalRan = true;
                base.Dispose();
            }
        }

        private sealed class ExtendedToast : ToastViewBase
        {
            public bool ExtraDisposalRan { get; private set; }

            public override void Dispose()
            {
                ExtraDisposalRan = true;
                base.Dispose();
            }
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
    }
}
