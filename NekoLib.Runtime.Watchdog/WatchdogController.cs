using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;

 
namespace NekoLib.Runtime.Watchdog
{
    public static class WatchdogController
    {
        private const string PipeName = "NekoLib.Watchdog";

        private const string LogPipeName = PipeName + ".logs";

        private static string Send(string cmd)
        {
            try
            {
                using (var client = new NamedPipeClientStream(
                    ".",
                    PipeName,
                    PipeDirection.InOut,
                    PipeOptions.None))
                {
                    client.Connect(1500);

                    using (var writer = new StreamWriter(client) { AutoFlush = true })
                    {
                        writer.WriteLine(cmd);
                        var reader = new StreamReader(client);
                        return reader.ReadLine();
                    }

                     
                }
            }
            catch (ObjectDisposedException)
            {
                return "error=pipe_closed";
            }
            catch (TimeoutException)
            {
                return "error=watchdog_not_running";
            }
            catch (IOException)
            {
                return "error=pipe_io";
            }
        }


        public static void Start() => Send("start");
        public static void Pause() => Send("pause");
        public static void Stop() => Send("stop");

        public static bool Ping() => Send("ping") == "pong";
        public static string Status() => Send("status");

        /// <summary>
        /// Subscribes to watchdog log streaming (watchdog -> this process) via a named pipe.
        /// The returned subscription reconnects automatically if the watchdog restarts.
        /// </summary>
        public static IDisposable SubscribeLogs(Action<string> onLogLine)
        {
            if (onLogLine == null) throw new ArgumentNullException(nameof(onLogLine));
            return new WatchdogLogSubscription(LogPipeName, onLogLine);
        }

        private sealed class WatchdogLogSubscription : IDisposable
        {
            private readonly string _pipeName;
            private readonly Action<string> _onLine;
            private readonly Thread _thread;
            private volatile bool _stop;

            public WatchdogLogSubscription(string pipeName, Action<string> onLine)
            {
                _pipeName = pipeName;
                _onLine = onLine;

                _thread = new Thread(Run)
                {
                    IsBackground = true,
                    Name = "WDG-LogPipe-Client"
                };
                _thread.Start();
            }

            private void Run()
            {
                while (!_stop)
                {
                    NamedPipeClientStream client = null;

                    try
                    {
                        client = new NamedPipeClientStream(
                            ".",
                            _pipeName,
                            PipeDirection.In,
                            PipeOptions.None);

                        // If watchdog isn't up, this throws TimeoutException.
                        client.Connect(1500);

                        using (var reader = new StreamReader(client))
                        {
                            client = null; // ownership transferred to reader scope

                            while (!_stop)
                            {
                                var line = reader.ReadLine();
                                if (line == null) break;

                                try { _onLine(line); }
                                catch { /* observers must not break listener */ }
                            }
                        }
                    }
                    catch
                    {
                        // ignore and retry
                    }
                    finally
                    {
                        try { client?.Dispose(); } catch { }
                    }

                    // Small backoff before reconnecting.
                    for (int i = 0; i < 10 && !_stop; i++)
                        Thread.Sleep(50);
                }
            }

            public void Dispose()
            {
                _stop = true;
                try { _thread.Join(1000); } catch { }
            }
        }

    }
}
