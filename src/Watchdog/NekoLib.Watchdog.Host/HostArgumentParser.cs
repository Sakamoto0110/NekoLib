using System;
using System.Collections.Generic;
using System.IO;
using NekoLib.Watchdog;

namespace NekoLib.Watchdog.Host
{
    internal static class HostArgumentParser
    {
        private const string SupportedProtocolVersion = "1";

        public static WatchdogOptions Parse(string[] args)
        {
            if (args == null || args.Length == 0)
                throw new ArgumentException("Missing --target argument.");

            string? target = null;
            string? targetArgs = null;
            string? workdir = null;
            string? attachToken = null;
            string? protocolVersion = null;
            int? attachPid = null;
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--protocol-version")
                {
                    EnsureUnique(seen, args[i]);
                    protocolVersion = ReadValue(
                        args,
                        ref i,
                        "--protocol-version");
                    continue;
                }

                if (args[i] == "--target")
                {
                    EnsureUnique(seen, args[i]);
                    target = ReadValue(args, ref i, "--target");
                    continue;
                }

                if (args[i] == "--args")
                {
                    EnsureUnique(seen, args[i]);
                    targetArgs = ReadValue(args, ref i, "--args");
                    continue;
                }

                if (args[i] == "--workdir")
                {
                    EnsureUnique(seen, args[i]);
                    workdir = ReadValue(args, ref i, "--workdir");
                    continue;
                }

                if (args[i] == "--attach-pid")
                {
                    EnsureUnique(seen, args[i]);
                    var rawPid = ReadValue(args, ref i, "--attach-pid");
                    if (!int.TryParse(
                            rawPid,
                            System.Globalization.NumberStyles.None,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out var parsedPid) ||
                        parsedPid < 1)
                    {
                        throw new ArgumentException(
                            "--attach-pid must be a positive process ID.");
                    }

                    attachPid = parsedPid;
                    continue;
                }

                if (args[i] == "--attach-token")
                {
                    EnsureUnique(seen, args[i]);
                    attachToken = ReadValue(args, ref i, "--attach-token");
                    continue;
                }

                throw new ArgumentException(
                    "Unknown Watchdog Host argument: " + args[i]);
            }

            if (string.IsNullOrWhiteSpace(target))
                throw new ArgumentException("--target is required.");
            if (string.IsNullOrWhiteSpace(protocolVersion))
                throw new ArgumentException("--protocol-version is required.");
            if (!string.Equals(
                    protocolVersion,
                    SupportedProtocolVersion,
                    StringComparison.Ordinal))
            {
                throw new NotSupportedException(
                    "Unsupported Watchdog Host protocol version '" +
                    protocolVersion +
                    "'. Expected '" +
                    SupportedProtocolVersion +
                    "'.");
            }

            var fullPath = Path.GetFullPath(target);
            var fullWorkdir = string.IsNullOrWhiteSpace(workdir)
                ? ""
                : Path.GetFullPath(workdir);

            if (!File.Exists(fullPath))
                throw new FileNotFoundException("Target executable not found.", fullPath);
            if (!string.IsNullOrEmpty(fullWorkdir) &&
                !Directory.Exists(fullWorkdir))
            {
                if (File.Exists(fullWorkdir))
                {
                    throw new ArgumentException(
                        "--workdir must identify a directory.");
                }

                throw new DirectoryNotFoundException(
                    "Watchdog Host working directory not found: " +
                    fullWorkdir);
            }
            if (attachPid.HasValue && string.IsNullOrWhiteSpace(attachToken))
                throw new ArgumentException(
                    "--attach-token is required when --attach-pid is supplied.");
            if (!attachPid.HasValue && !string.IsNullOrWhiteSpace(attachToken))
                throw new ArgumentException(
                    "--attach-pid is required when --attach-token is supplied.");

            return new WatchdogOptions
            {
                TargetPath = fullPath,
                TargetArguments = targetArgs ?? "",
                WorkingDirectory = fullWorkdir,
                InitialProcessId = attachPid,
                AttachToken = attachToken ?? ""
            };
        }

        private static void EnsureUnique(HashSet<string> seen, string option)
        {
            if (!seen.Add(option))
                throw new ArgumentException(
                    "Duplicate Watchdog Host argument: " + option);
        }

        private static string ReadValue(string[] args, ref int index, string option)
        {
            if (index + 1 >= args.Length)
                throw new ArgumentException(option + " requires a value.");

            index++;
            return args[index];
        }
    }
}
