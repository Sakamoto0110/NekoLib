using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using NekoLib.Navigation.Contracts.Runtime;
using NekoLib.Navigation.Tests.Unit.Fakes;
using NekoLib.Navigation.WinForms.Adapters;
using NekoLib.Navigation.Wpf.Adapters;
using NekoLib.Navigation.Wpf.Toolkit;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    public sealed class AdapterParityTests
    {
        [Fact]
        public void ToastClickContract_PreservesNativeRouting_AndExplicitCloseWorksOnBoth()
        {
            RunSta(() =>
            {
                var toast = new WpfToast();
                var text = new TextBlock { Text = "content" };
                var button = new Button { Content = "Close" };
                var content = new StackPanel();
                content.Children.Add(text);
                content.Children.Add(button);
                toast.Content = content;
                var window = new Window { Content = toast, Left = -32000, Width = 320, Height = 240, ShowInTaskbar = false };
                window.Show();
                Pump();
                var count = 0;
                ((NekoLib.Navigation.Contracts.Pages.IToastView)toast).BindDismiss(() => count++);
                RaiseMouseDown(toast);
                Assert.Equal(1, count);
                RaiseMouseDown(text);
                Assert.Equal(2, count);
                RaiseMouseDown(button);
                Assert.Equal(2, count); // Button handles the native mouse event.
                Mouse.Capture(null);
                button.Click += (_, __) => toast.CloseExplicitly();
                button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Assert.Equal(3, count);
                toast.Dispose();
                window.Close();

                using (var formsToast = new FormsToast())
                using (var formsButton = new InputButton())
                {
                    formsToast.Controls.Add(formsButton);
                    var formsCount = 0;
                    ((NekoLib.Navigation.Contracts.Pages.IToastView)formsToast).BindDismiss(() => formsCount++);
                    formsToast.ClickBackground();
                    formsButton.ClickProgrammatically();
                    Assert.Equal(1, formsCount); // Native WinForms clicks never bubble.
                    formsButton.Click += (_, __) => formsToast.CloseExplicitly();
                    formsButton.ClickProgrammatically();
                    Assert.Equal(2, formsCount);
                }
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            });
        }

        private static void RaiseMouseDown(UIElement element) => element.RaiseEvent(
            new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Mouse.MouseDownEvent });

        [Fact]
        public void WpfFocusObserver_DistinguishesInternalFocusFromLeavingSubtree_AndUnsubscribes()
        {
            RunSta(() =>
            {
                var root = new StackPanel();
                var surface = new StackPanel();
                var field = new TextBox();
                var confirm = new Button { Content = "Confirm" };
                var outside = new Button { Content = "Outside" };
                surface.Children.Add(field);
                surface.Children.Add(confirm);
                root.Children.Add(surface);
                root.Children.Add(outside);
                var window = new Window { Content = root, Left = -32000, Width = 320, Height = 240, ShowInTaskbar = false };
                window.Show();
                field.Focus();
                Pump();
                var count = 0;
                var subscription = new WpfFocusObserverAdapter().Track(surface, () => count++);
                confirm.Focus();
                Pump();
                Assert.Equal(0, count);
                outside.Focus();
                Pump();
                Assert.Equal(1, count);
                subscription.Dispose();
                field.Focus();
                outside.Focus();
                Pump();
                Assert.Equal(1, count);
                window.Close();
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            });
        }

        [Fact]
        public void BootstrapLifetime_FromWorker_DisposesNativeResourcesOnOwner()
        {
            RunSta(() =>
            {
                var uiThread = Thread.CurrentThread.ManagedThreadId;
                var resources = new OwnerResources();
                var lifetime = new NekoLib.Navigation.Bootstrap.NavigationBootstrapLifetime(
                    resources, resources, dispatcher: new WpfEventDispatcherAdapter(Dispatcher.CurrentDispatcher));
                var worker = new Thread(lifetime.Dispose);
                worker.Start();
                PumpUntil(() => !worker.IsAlive);
                Assert.Equal(uiThread, resources.StoppedOnThread);
                Assert.Equal(uiThread, resources.DisposedOnThread);
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            });
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void WpfDispatcher_AfterShutdown_NeverExecutesOnWorker(bool asynchronous)
        {
            RunSta(() =>
            {
                var native = Dispatcher.CurrentDispatcher;
                var adapter = new WpfEventDispatcherAdapter(native);
                native.InvokeShutdown();
                var ran = false;
                Exception failure = null;
                var worker = new Thread(() =>
                {
                    try
                    {
                        if (asynchronous) adapter.BeginInvoke(() => ran = true);
                        else adapter.Invoke(() => ran = true);
                    }
                    catch (Exception e) { failure = e; }
                });
                worker.Start();
                Assert.True(worker.Join(5000));
                Assert.IsType<InvalidOperationException>(failure);
                Assert.False(ran);
                adapter.Invoke(() => ran = true);
                Assert.True(ran);
            });
        }

        [Fact]
        public void WpfDispatcherAndFocus_FromWorker_UseOwningThread()
        {
            RunSta(() =>
            {
                var host = new TextBox();
                var window = new Window { Content = host, ShowInTaskbar = false, Left = -32000, Width = 300, Height = 200 };
                window.Show();
                var uiThread = Thread.CurrentThread.ManagedThreadId;
                var adapter = new WpfEventDispatcherAdapter(host.Dispatcher);
                var focus = new WpfNavigationToolkit(host);
                var invokedOn = 0;
                Exception failure = null;
                var worker = new Thread(() =>
                {
                    try
                    {
                        adapter.Invoke(() => invokedOn = Thread.CurrentThread.ManagedThreadId);
                        focus.FocusSurface();
                    }
                    catch (Exception e) { failure = e; }
                });
                worker.Start();
                PumpUntil(() => !worker.IsAlive);
                Pump();
                Assert.Null(failure);
                Assert.Equal(uiThread, invokedOn);
                Assert.True(host.IsKeyboardFocused);
                window.Close();
                host.Dispatcher.InvokeShutdown();
                focus.FocusSurface();
            });
        }

        [Theory]
        [InlineData(InteractionObservationMode.InputOnly, 0)]
        [InlineData(InteractionObservationMode.InputAndControlChanges, 2)]
        public void ExplicitActivityPolicy_MatchesForProgrammaticChanges(InteractionObservationMode mode, int expected)
        {
            RunSta(() =>
            {
                var root = new StackPanel();
                var text = new TextBox();
                var button = new Button();
                root.Children.Add(text);
                root.Children.Add(button);
                var wpfCount = 0;
                using (var observer = new WpfInteractionObserver(root, mode))
                {
                    observer.InteractionDetected += () => wpfCount++;
                    text.Text = "background refresh";
                    button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                }
                text.Text = "after disposal";
                Assert.Equal(expected, wpfCount);

                using (var formsRoot = new System.Windows.Forms.Panel())
                using (var formsText = new System.Windows.Forms.TextBox())
                using (var formsButton = new InputButton())
                {
                    formsRoot.Controls.Add(formsText);
                    formsRoot.Controls.Add(formsButton);
                    var formsCount = 0;
                    using (var observer = new WinFormsInteractionObserver(formsRoot, mode))
                    {
                        observer.InteractionDetected += () => formsCount++;
                        formsText.Text = "background refresh";
                        formsButton.ClickProgrammatically();
                    }
                    formsText.Text = "after disposal";
                    Assert.Equal(expected, formsCount);
                }
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            });
        }

        [Fact]
        public void LegacyActivityConstructors_KeepTheirExistingPolicy()
        {
            RunSta(() =>
            {
                var wpf = new TextBox();
                using (var observer = new WpfInteractionObserver(wpf))
                {
                    var count = 0;
                    observer.InteractionDetected += () => count++;
                    wpf.Text = "programmatic";
                    Assert.Equal(0, count);
                }
                using (var forms = new System.Windows.Forms.TextBox())
                using (var observer = new WinFormsInteractionObserver(forms))
                {
                    var count = 0;
                    observer.InteractionDetected += () => count++;
                    forms.Text = "programmatic";
                    Assert.Equal(1, count);
                }
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            });
        }

        [Fact]
        public async Task RuntimeShutdown_DispatchFailure_DoesNotDisposePagesInline()
        {
            var dispatcher = new UnavailableDispatcher();
            var fixture = RuntimeTestFixture.BuildWithDispatcher<StubIdle>(dispatcher);
            await fixture.Runtime.GoIdleAsync();
            var page = fixture.Runtime.Current;
            dispatcher.Unavailable = true;
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Runtime.DisposeAsync().AsTask());
            Assert.False(page.IsDisposed);
            dispatcher.Unavailable = false;
            await fixture.Runtime.DisposeAsync();
            Assert.True(page.IsDisposed);
        }

        internal static void Pump()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, (Action)(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }

        private static void PumpUntil(Func<bool> done)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!done() && DateTime.UtcNow < deadline) { Pump(); Thread.Sleep(1); }
            Assert.True(done());
        }

        internal static void RunSta(Action body)
        {
            Exception failure = null;
            var thread = new Thread(() => { try { body(); } catch (Exception e) { failure = e; } });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(15000));
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private sealed class InputButton : System.Windows.Forms.Button
        {
            public void ClickProgrammatically() => OnClick(EventArgs.Empty);
        }

        private sealed class WpfToast : NekoLib.Navigation.Wpf.Hosting.ToastViewBase
        {
            public void CloseExplicitly() => Dismiss();
        }

        private sealed class FormsToast : NekoLib.Navigation.WinForms.Hosting.ToastViewBase
        {
            public void ClickBackground() => OnClick(EventArgs.Empty);
            public void CloseExplicitly() => Dismiss();
        }

        private sealed class UnavailableDispatcher : NekoLib.Navigation.Contracts.Platform.IEventDispatcherAdapter
        {
            public bool Unavailable;
            public void Invoke(Action action)
            {
                if (Unavailable) throw new InvalidOperationException("UI unavailable");
                action();
            }
            public void BeginInvoke(Action action) => Invoke(action);
        }

        private sealed class OwnerResources : IInteractionObserverService, NekoLib.Navigation.Contracts.Platform.ITimerAdapter
        {
            public event Action InteractionDetected { add { } remove { } }
            public event Action Tick { add { } remove { } }
            public int IntervalMilliseconds { get; set; }
            public int StoppedOnThread;
            public int DisposedOnThread;
            public void Start() { }
            public void Stop() { StoppedOnThread = Thread.CurrentThread.ManagedThreadId; }
            public void Dispose() { DisposedOnThread = Thread.CurrentThread.ManagedThreadId; }
        }
    }
}
