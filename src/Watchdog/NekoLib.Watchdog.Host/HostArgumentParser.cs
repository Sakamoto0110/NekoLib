using System;
using System.IO;
using NekoLib.Watchdog;

namespace NekoLib.Watchdog.Host
{
    internal static class HostArgumentParser
    {
        public static WatchdogOptions Parse(string[] args)
        {
            if (args == null || args.Length == 0)
                throw new ArgumentException("Missing --target argument.");

            string target = null;
            string targetArgs = null;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--target" && i + 1 < args.Length)
                {
                    target = args[++i];
                    continue;
                }

                if (args[i] == "--args" && i + 1 < args.Length)
                {
                    targetArgs = args[++i];
                    continue;
                }
            }

            if (string.IsNullOrWhiteSpace(target))
                throw new ArgumentException("--target is required.");

            var fullPath = Path.GetFullPath(target);

            if (!File.Exists(fullPath))
                throw new FileNotFoundException("Target executable not found.", fullPath);

            return new WatchdogOptions
            {
                TargetPath = fullPath,
                TargetArguments = targetArgs ?? ""
            };
        }
    }
}