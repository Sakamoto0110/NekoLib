using System;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Pipes.Tests.Unit.Fakes;
using Xunit;

namespace NekoLib.Pipes.Tests.Unit
{
    /// <summary>
    /// Resilience tests for <see cref="PipeEventClient"/> (audit M4): it used to
    /// die silently on a dropped/restarted hub or a throwing handler. It now
    /// auto-reconnects and isolates handler faults.
    /// </summary>
    public class PipeEventClientTests
    {
        private static PipeServer NewEventServer(string name)
            => new PipeServer(new PipeServerOptions
            {
                PipeName = name,
                EnableEvents = true,
                MaxEventSubscribers = 4
            });

        private static async Task PublishUntil(PipeServer server, Func<bool> condition, int attempts = 20)
        {
            for (int i = 0; i < attempts && !condition(); i++)
            {
                await server.Events.PublishAsync("e", new { hello = "world", i });
                await Task.Delay(150);
            }
        }

        [Fact]
        public async Task Subscriber_AutoReconnects_AfterServerRestart()
        {
            var name = PipeTestUtil.UniqueName();
            int received = 0;

            using (var client = new PipeEventClient(name) { ReconnectDelay = TimeSpan.FromMilliseconds(200) })
            {
                client.OnEvent += _ => Interlocked.Increment(ref received);

                // --- Server #1 ---
                using (var server1 = NewEventServer(name))
                {
                    server1.Start();
                    client.Start();

                    Assert.True(
                        PipeTestUtil.WaitUntil(() => server1.Events.SubscriberCount >= 1, 5000),
                        "client never connected to server #1");

                    await PublishUntil(server1, () => received >= 1);
                    Assert.True(received >= 1, "no event from server #1");
                }
                // server #1 disposed — the client should now reconnect.

                // --- Server #2 on the same pipe name ---
                using (var server2 = NewEventServer(name))
                {
                    server2.Start();

                    Assert.True(
                        PipeTestUtil.WaitUntil(() => server2.Events.SubscriberCount >= 1, 8000),
                        "client did not auto-reconnect to server #2");

                    int before = received;
                    await PublishUntil(server2, () => received > before);
                    Assert.True(received > before, "no event received after reconnect");
                }
            }
        }

        [Fact]
        public async Task ThrowingHandler_DoesNotStopOtherHandlers()
        {
            var name = PipeTestUtil.UniqueName();
            var secondHandlerRan = new ManualResetEventSlim(false);

            using (var server = NewEventServer(name))
            {
                server.Start();

                using (var client = new PipeEventClient(name))
                {
                    client.OnEvent += _ => throw new InvalidOperationException("bad handler");
                    client.OnEvent += _ => secondHandlerRan.Set();
                    client.Start();

                    Assert.True(
                        PipeTestUtil.WaitUntil(() => server.Events.SubscriberCount >= 1, 5000),
                        "client never connected");

                    for (int i = 0; i < 10 && !secondHandlerRan.IsSet; i++)
                    {
                        await server.Events.PublishAsync("e", new { i });
                        secondHandlerRan.Wait(300);
                    }

                    Assert.True(secondHandlerRan.IsSet,
                        "the second handler did not run despite the first throwing");
                }
            }
        }

        [Fact]
        public async Task FailedConnect_RaisesIsolatedErrorWithoutDisconnected_AndStartRemainsOneShot()
        {
            var client = new PipeEventClient(PipeTestUtil.UniqueName())
            {
                AutoReconnect = false,
                ConnectTimeout = TimeSpan.FromMilliseconds(150)
            };
            var observed = new TaskCompletionSource<Exception>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var disconnected = 0;

            client.OnError += _ => throw new InvalidOperationException("bad error subscriber");
            client.OnError += error => observed.TrySetResult(error);
            client.OnDisconnected += () => Interlocked.Increment(ref disconnected);
            client.Start();

            Assert.NotNull(await AwaitWithin(observed.Task, 5000));
            Assert.Equal(0, Volatile.Read(ref disconnected));
            Assert.Throws<InvalidOperationException>(() => client.Start());

            await client.ShutdownAsync();

            Assert.Throws<ObjectDisposedException>(() => client.Start());
        }

        [Fact]
        public async Task ConnectionCallbacks_IsolateSubscribersAndShutdownDisconnectsInOrder()
        {
            var name = PipeTestUtil.UniqueName();
            using (var server = NewEventServer(name))
            using (var client = new PipeEventClient(name))
            {
                var connected = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                var disconnected = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                var errors = 0;

                client.OnConnected += () => throw new InvalidOperationException("first connected");
                client.OnConnected += () => connected.TrySetResult(true);
                client.OnDisconnected += () => throw new InvalidOperationException("first disconnected");
                client.OnDisconnected += () => disconnected.TrySetResult(true);
                client.OnError += _ => Interlocked.Increment(ref errors);

                server.Start();
                client.Start();

                await AwaitWithin(connected.Task, 5000);
                await client.ShutdownAsync();
                await AwaitWithin(disconnected.Task, 5000);

                Assert.Equal(0, Volatile.Read(ref errors));
            }
        }

        [Fact]
        public async Task MalformedFrame_RaisesErrorBeforeDisconnected()
        {
            var name = PipeTestUtil.UniqueName();
            using (var server = new NamedPipeServerStream(
                name + ".events",
                PipeDirection.Out,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous))
            {
                var serverTask = Task.Run(async () =>
                {
                    await Task.Run(() => server.WaitForConnection());
                    var payload = Encoding.UTF8.GetBytes("{not-json}");
                    var length = BitConverter.GetBytes(payload.Length);
                    await server.WriteAsync(length, 0, length.Length);
                    await server.WriteAsync(payload, 0, payload.Length);
                    await server.FlushAsync();
                });
                var order = new ConcurrentQueue<string>();
                var disconnected = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                using (var client = new PipeEventClient(name)
                {
                    AutoReconnect = false,
                    ConnectTimeout = TimeSpan.FromSeconds(3)
                })
                {
                    client.OnConnected += () => order.Enqueue("connected");
                    client.OnError += _ => throw new InvalidOperationException("first error");
                    client.OnError += _ => order.Enqueue("error");
                    client.OnDisconnected += () =>
                    {
                        order.Enqueue("disconnected");
                        disconnected.TrySetResult(true);
                    };
                    client.Start();

                    await AwaitWithin(serverTask, 5000);
                    await AwaitWithin(disconnected.Task, 5000);
                    await client.ShutdownAsync();
                }

                Assert.Equal(
                    new[] { "connected", "error", "disconnected" },
                    order.ToArray());
            }
        }

        [Fact]
        public async Task CleanRemoteEof_DisconnectsWithoutError()
        {
            var name = PipeTestUtil.UniqueName();
            using (var server = new NamedPipeServerStream(
                name + ".events",
                PipeDirection.Out,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous))
            {
                var serverTask = Task.Run(() => server.WaitForConnection());
                var order = new ConcurrentQueue<string>();
                var disconnected = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                var errors = 0;
                using (var client = new PipeEventClient(name)
                {
                    AutoReconnect = false,
                    ConnectTimeout = TimeSpan.FromSeconds(3)
                })
                {
                    client.OnConnected += () => order.Enqueue("connected");
                    client.OnError += _ => Interlocked.Increment(ref errors);
                    client.OnDisconnected += () =>
                    {
                        order.Enqueue("disconnected");
                        disconnected.TrySetResult(true);
                    };
                    client.Start();

                    await AwaitWithin(serverTask, 5000);
                    server.Disconnect();
                    await AwaitWithin(disconnected.Task, 5000);
                    await client.ShutdownAsync();
                }

                Assert.Equal(0, Volatile.Read(ref errors));
                Assert.Equal(new[] { "connected", "disconnected" }, order.ToArray());
            }
        }

        private static async Task AwaitWithin(Task task, int timeoutMs)
        {
            var winner = await Task.WhenAny(task, Task.Delay(timeoutMs));
            Assert.Same(task, winner);
            await task;
        }

        private static async Task<T> AwaitWithin<T>(Task<T> task, int timeoutMs)
        {
            await AwaitWithin((Task)task, timeoutMs);
            return await task;
        }
    }
}
