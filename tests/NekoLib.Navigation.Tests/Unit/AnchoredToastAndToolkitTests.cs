using NekoLib.Navigation.Bootstrap;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Runtime.Core;
using NekoLib.Navigation.Toolkit.Abstractions;
using NekoLib.Navigation.WinForms.Adapters;
using NekoLib.Navigation.WinForms.Hosting;
using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    /// <summary>
    /// NAV-010. The platform host doubles as the navigation toolkit and bootstrap
    /// registers it through a probe, and the WinForms toast base parks itself at the
    /// bottom-right anchor instead of covering the whole host.
    /// </summary>
    [Collection("NavigationServiceFacade")]
    public class AnchoredToastAndToolkitTests
    {
        // The host docks every added view to Fill; the toast base was the only
        // surface base that never undocked, so a stock toast covered the host.
        [Fact]
        public void WinFormsToast_AfterAttach_IsNotHostSized()
        {
            RunSta(() =>
            {
                using (var root = new System.Windows.Forms.Panel { Width = 800, Height = 600 })
                using (var toast = new ProbeToast())
                {
                    var host = new WinFormsLayeredPageHostBase(root);

                    host.AddView(toast);
                    ((IToastView)toast).OnShown(null);

                    Assert.Equal(System.Windows.Forms.DockStyle.None, toast.Dock);
                    Assert.NotEqual(root.ClientRectangle.Size, toast.Size);
                    Assert.Equal(ProbeToast.DesignSize, toast.Size);
                }
            });
        }

        [Fact]
        public void WinFormsToast_AfterAttach_SitsAtTheBottomRightInset()
        {
            RunSta(() =>
            {
                using (var root = new System.Windows.Forms.Panel { Width = 800, Height = 600 })
                using (var toast = new ProbeToast())
                {
                    var host = new WinFormsLayeredPageHostBase(root);
                    var scale = host.Surface.Scale;
                    var inset = (int)Math.Round(20 * scale);

                    host.AddView(toast);
                    ((IToastView)toast).OnShown(null);

                    var expectedRight = root.ClientRectangle.Width - inset;
                    var expectedBottom = root.ClientRectangle.Height - inset;

                    Assert.Equal(expectedRight, toast.Right);
                    Assert.Equal(expectedBottom, toast.Bottom);
                }
            });
        }

        [Fact]
        public void WinFormsToast_CustomInset_IsHonoured()
        {
            RunSta(() =>
            {
                using (var root = new System.Windows.Forms.Panel { Width = 800, Height = 600 })
                using (var toast = new WideInsetToast())
                {
                    var host = new WinFormsLayeredPageHostBase(root);
                    var inset = (int)Math.Round(64 * host.Surface.Scale);

                    host.AddView(toast);
                    ((IToastView)toast).OnShown(null);

                    Assert.Equal(root.ClientRectangle.Width - inset, toast.Right);
                    Assert.Equal(root.ClientRectangle.Height - inset, toast.Bottom);
                }
            });
        }

        [Fact]
        public void LayeredHosts_ImplementTheNavigationToolkit()
        {
            RunSta(() =>
            {
                using (var root = new System.Windows.Forms.Panel { Width = 640, Height = 480 })
                {
                    var winForms = new WinFormsLayeredPageHostBase(root);
                    Assert.IsAssignableFrom<INavigationToolkit>(winForms);
                    Assert.Equal(root.ClientRectangle, winForms.Surface.ClientBounds);

                    var wpf = new NekoLib.Navigation.Wpf.Hosting.WpfLayeredPageHostBase(
                        new System.Windows.Controls.Grid());
                    Assert.IsAssignableFrom<INavigationToolkit>(wpf);
                    Assert.NotNull(wpf.Surface);
                }
            });
        }

        // The registration shape the item accepted: the same `host as ...` probe
        // bootstrap already uses for IViewHost, resolved from a mounted context.
        [Fact]
        public async Task Start_MountedContext_ResolvesTheNavigationToolkit()
        {
            NavigationContext context = null;
            System.Windows.Forms.Panel root = null;

            try
            {
                RunSta(() =>
                {
                    root = new System.Windows.Forms.Panel { Width = 800, Height = 600 };
                    context = PageNavBootstrap
                        .Use<WinFormsPlatformAdapter>(root)
                        .Start();
                });

                Assert.True(context.Services.CanResolve(typeof(INavigationToolkit)));

                var toolkit = (INavigationToolkit)context.Services.Get(typeof(INavigationToolkit));
                Assert.Same(context.Host, toolkit);
                Assert.Equal(root.ClientRectangle, toolkit.Surface.ClientBounds);
            }
            finally
            {
                await NavigationService.Shutdown();
                root?.Dispose();
            }
        }

        private class ProbeToast : ToastViewBase
        {
            internal static readonly System.Drawing.Size DesignSize =
                new System.Drawing.Size(240, 80);

            public ProbeToast()
            {
                Size = DesignSize;
            }
        }

        private sealed class WideInsetToast : ProbeToast
        {
            protected override int AnchorInset => 64;
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
