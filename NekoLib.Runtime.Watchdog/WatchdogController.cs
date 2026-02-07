using System.IO;
using System.IO.Pipes;

namespace NekoLib.Runtime.Watchdog
{
    public static class WatchdogController
    {
        private const string PipeName = "NekoLib.Watchdog";

        private static string Send(string cmd)
        {
            using (var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut))
            {
                client.Connect(500);

                using (var writer = new StreamWriter(client) { AutoFlush = true })
                using (var reader = new StreamReader(client))
                {
                    writer.WriteLine(cmd);
                    return reader.ReadLine();
                }
            }
        }

        public static void Start() => Send("start");
        public static void Pause() => Send("pause");
        public static void Stop() => Send("stop");

        public static bool Ping() => Send("ping") == "pong";
        public static string Status() => Send("status");
    }
}
