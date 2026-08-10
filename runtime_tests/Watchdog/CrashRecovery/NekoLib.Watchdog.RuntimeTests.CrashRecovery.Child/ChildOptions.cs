#nullable enable
using System;
using System.IO;

namespace NekoLib.Watchdog.RuntimeTests.CrashRecovery.Child
{
    internal sealed class ChildOptions
    {
        public string RunRoot = string.Empty;
        public string PlanPath = string.Empty;

        public static ChildOptions Parse(string[] args)
        {
            ChildOptions options = new ChildOptions();
            bool role = false;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--scenario-child":
                        role = true;
                        break;
                    case "--run-root":
                        options.RunRoot = Read(args, ref i, "--run-root");
                        break;
                    case "--plan":
                        options.PlanPath = Read(args, ref i, "--plan");
                        break;
                    default:
                        throw new ArgumentException("Unknown E3-WDOG child argument: " + args[i]);
                }
            }

            if (!role) throw new ArgumentException("--scenario-child is required.");
            if (!Path.IsPathRooted(options.RunRoot))
                throw new ArgumentException("--run-root must be absolute.");
            if (!Path.IsPathRooted(options.PlanPath))
                throw new ArgumentException("--plan must be absolute.");

            options.RunRoot = Path.GetFullPath(options.RunRoot);
            options.PlanPath = Path.GetFullPath(options.PlanPath);
            if (!File.Exists(options.PlanPath))
                throw new FileNotFoundException("The persisted E3-WDOG child plan was not found.", options.PlanPath);

            return options;
        }

        private static string Read(string[] args, ref int index, string option)
        {
            if (index + 1 >= args.Length) throw new ArgumentException(option + " requires a value.");
            index++;
            return args[index];
        }
    }
}
