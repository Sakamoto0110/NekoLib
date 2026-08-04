using NekoLib.Navigation.WinForms.Adapters;
using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using Xunit;

namespace NekoLib.Navigation.Tests.Unit
{
    /// <summary>
    /// NAV-006. <c>Control.InvokeRequired</c> answers <c>false</c> from every thread
    /// while no handle exists in the parent chain, so the adapter used to run the
    /// action on the calling thread and the <see cref="InvalidOperationException"/> it
    /// documents was unreachable in exactly the case it describes. UI-thread identity
    /// is now captured at construction; behaviour with a live handle is unchanged.
    /// </summary>
    public class WinFormsEventDispatcherAdapterTests
    {
        [Fact]
        public void Invoke_HandleAbsentOnOwningThread_RunsInline()
        {
            RunWithHost(showHost: false, body: host =>
            {
                var dispatcher = new WinFormsEventDispatcherAdapter(host);
                var ranOnThread = 0;

                dispatcher.Invoke(() => ranOnThread = Thread.CurrentThread.ManagedThreadId);

                Assert.Equal(Thread.CurrentThread.ManagedThreadId, ranOnThread);
            });
        }

        [Fact]
        public void Invoke_HandleAbsentFromWorkerThread_FailsInsteadOfRunningOffTheUiThread()
        {
            RunWithHost(showHost: false, body: host =>
            {
                var dispatcher = new WinFormsEventDispatcherAdapter(host);
                var ranOnThread = 0;

                var failure = RunOnWorkerWhilePumping(
                    () => dispatcher.Invoke(
                        () => ranOnThread = Thread.CurrentThread.ManagedThreadId));

                Assert.IsType<InvalidOperationException>(failure);
                Assert.Equal(0, ranOnThread);
            });
        }

        [Fact]
        public void Invoke_HandleCreatedOnUiThread_RunsInline()
        {
            RunWithHost(showHost: true, body: host =>
            {
                var dispatcher = new WinFormsEventDispatcherAdapter(host);
                var ranOnThread = 0;

                dispatcher.Invoke(() => ranOnThread = Thread.CurrentThread.ManagedThreadId);

                Assert.Equal(Thread.CurrentThread.ManagedThreadId, ranOnThread);
            });
        }

        [Fact]
        public void Invoke_HandleCreatedFromWorkerThread_RunsOnTheUiThread()
        {
            RunWithHost(showHost: true, body: host =>
            {
                var dispatcher = new WinFormsEventDispatcherAdapter(host);
                var uiThreadId = Thread.CurrentThread.ManagedThreadId;
                var ranOnThread = 0;

                var failure = RunOnWorkerWhilePumping(
                    () => dispatcher.Invoke(
                        () => ranOnThread = Thread.CurrentThread.ManagedThreadId));

                Assert.Null(failure);
                Assert.Equal(uiThreadId, ranOnThread);
            });
        }

        [Fact]
        public void BeginInvoke_HandleAbsentOnOwningThread_RunsInline()
        {
            RunWithHost(showHost: false, body: host =>
            {
                var dispatcher = new WinFormsEventDispatcherAdapter(host);
                var ranOnThread = 0;

                dispatcher.BeginInvoke(() => ranOnThread = Thread.CurrentThread.ManagedThreadId);

                Assert.Equal(Thread.CurrentThread.ManagedThreadId, ranOnThread);
            });
        }

        [Fact]
        public void BeginInvoke_HandleAbsentFromWorkerThread_FailsInsteadOfRunningOffTheUiThread()
        {
            RunWithHost(showHost: false, body: host =>
            {
                var dispatcher = new WinFormsEventDispatcherAdapter(host);
                var ranOnThread = 0;

                var failure = RunOnWorkerWhilePumping(
                    () => dispatcher.BeginInvoke(
                        () => ranOnThread = Thread.CurrentThread.ManagedThreadId));

                Assert.IsType<InvalidOperationException>(failure);
                Assert.Equal(0, ranOnThread);
            });
        }

        [Fact]
        public void BeginInvoke_HandleCreatedOnUiThread_PostsInsteadOfRunningInline()
        {
            RunWithHost(showHost: true, body: host =>
            {
                var dispatcher = new WinFormsEventDispatcherAdapter(host);
                var ran = false;

                dispatcher.BeginInvoke(() => ran = true);
                Assert.False(ran);

                PumpUntil(() => ran, TimeSpan.FromSeconds(10));
                Assert.True(ran);
            });
        }

        [Fact]
        public void BeginInvoke_HandleCreatedFromWorkerThread_RunsOnTheUiThread()
        {
            RunWithHost(showHost: true, body: host =>
            {
                var dispatcher = new WinFormsEventDispatcherAdapter(host);
                var uiThreadId = Thread.CurrentThread.ManagedThreadId;
                var ranOnThread = 0;

                using (var posted = new ManualResetEventSlim())
                {
                    var failure = RunOnWorkerWhilePumping(() => dispatcher.BeginInvoke(() =>
                    {
                        ranOnThread = Thread.CurrentThread.ManagedThreadId;
                        posted.Set();
                    }));

                    Assert.Null(failure);
                    PumpUntil(() => posted.IsSet, TimeSpan.FromSeconds(10));
                }

                Assert.Equal(uiThreadId, ranOnThread);
            });
        }

        /// <summary>
        /// Builds the real host shape: a panel inside a form parked far off-screen, so
        /// a shown host delivers a genuine message queue without ever being visible.
        /// With <paramref name="showHost"/> false the panel is the bootstrap case the
        /// finding measured — no handle anywhere in the parent chain.
        /// </summary>
        private static void RunWithHost(bool showHost, Action<System.Windows.Forms.Control> body)
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
                    var host = new System.Windows.Forms.Panel
                    {
                        Dock = System.Windows.Forms.DockStyle.Fill
                    };
                    form.Controls.Add(host);

                    if (showHost)
                    {
                        form.Show();
                        System.Windows.Forms.Application.DoEvents();
                        Assert.True(host.IsHandleCreated);
                    }
                    else
                    {
                        Assert.False(form.IsHandleCreated);
                        Assert.False(host.IsHandleCreated);
                    }

                    try
                    {
                        body(host);
                    }
                    finally
                    {
                        if (showHost)
                            form.Close();
                    }
                }
            });
        }

        /// <summary>
        /// Runs <paramref name="action"/> on a worker thread while this thread keeps
        /// pumping messages, because a marshaled <c>Invoke</c> blocks the worker until
        /// the UI thread services it. Returns the exception the worker observed, or
        /// null.
        /// </summary>
        private static Exception RunOnWorkerWhilePumping(Action action)
        {
            Exception captured = null;

            using (var finished = new ManualResetEventSlim())
            {
                var worker = new Thread(() =>
                {
                    try { action(); }
                    catch (Exception ex) { captured = ex; }
                    finally { finished.Set(); }
                });

                worker.IsBackground = true;
                worker.Start();

                PumpUntil(() => finished.IsSet, TimeSpan.FromSeconds(10));
                Assert.True(
                    worker.Join(TimeSpan.FromSeconds(10)),
                    "The worker thread never finished.");
            }

            return captured;
        }

        private static void PumpUntil(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (!condition() && DateTime.UtcNow < deadline)
            {
                System.Windows.Forms.Application.DoEvents();
                Thread.Sleep(1);
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
