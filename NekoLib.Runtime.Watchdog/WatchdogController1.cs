//using System;
//using System.IO;
//using System.IO.Pipes;
 
//namespace NekoLib.Runtime.Watchdog
//{
//    public static class WatchdogController
//    {
//        private const string PipeName = "NekoLib.Watchdog";

//        private static string Send(string cmd)
//        {
//            try
//            {
//                using (var client = new NamedPipeClientStream(
//                    ".",
//                    PipeName,
//                    PipeDirection.InOut,
//                    PipeOptions.None))
//                {
//                    client.Connect(1500);

//                    using (var writer = new StreamWriter(client) { AutoFlush = true })
//                    {
//                        writer.WriteLine(cmd);
//                        var reader = new StreamReader(client);
//                        return reader.ReadLine();
//                    }

                     
//                }
//            }
//            catch (ObjectDisposedException)
//            {
//                return "error=pipe_closed";
//            }
//            catch (TimeoutException)
//            {
//                return "error=watchdog_not_running";
//            }
//            catch (IOException)
//            {
//                return "error=pipe_io";
//            }
//        }


//        public static void Start() => Send("start");
//        public static void Pause() => Send("pause");
//        public static void Stop() => Send("stop");

//        public static bool Ping() => Send("ping") == "pong";
//        public static string Status() => Send("status");
//    }
//}
