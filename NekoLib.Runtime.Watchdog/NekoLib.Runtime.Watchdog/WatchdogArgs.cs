using System;

namespace NekoLib.Runtime.Watchdog
{
    /// <summary>
    /// Minimal CLI parser compatible with the legacy watchdog.
    /// Keep CLI parsing outside of the runtime if you prefer, but this helper makes migration easier.
    /// </summary>
    public static class WatchdogArgs
    {
        /// <summary>
        /// Parses legacy-style arguments:
        /// --target, --args, --workdir, --instancetag, --delay, --backoff, --maxbackoff, --attachpid, --flagsdir
        /// Supports --key value and --key=value.
        /// </summary>
        public static WatchdogOptions FromArgs(string[] args)
        {
            var o = new WatchdogOptions();

            string GetVal(string key, string defVal)
            {
                if(args == null || args.Length == 0) return defVal;
                for(int i = 0; i < args.Length; i++)
                {
                    var tok = args[i] ?? "";
                    if(tok.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                        return tok.Substring(key.Length + 1);

                    if(string.Equals(tok, key, StringComparison.OrdinalIgnoreCase))
                    {
                        if(i + 1 < args.Length)
                        {
                            var next = args[i + 1] ?? "";
                            if(!(next.StartsWith("--") || next.StartsWith("-")))
                                return next;
                        }
                        return defVal;
                    }
                }
                return defVal;
            }

            o.TargetPath = GetVal("--target", o.TargetPath);
            o.TargetArgs = GetVal("--args", o.TargetArgs);
            o.WorkDir = GetVal("--workdir", o.WorkDir);
            o.InstanceTag = GetVal("--instancetag", o.InstanceTag);
            o.FlagsDirectory = GetVal("--flagsdir", o.FlagsDirectory);

            int v;
            if(int.TryParse(GetVal("--delay", o.RestartDelayMs.ToString()), out v)) o.RestartDelayMs = v;
            if(int.TryParse(GetVal("--backoff", o.BackoffStepMs.ToString()), out v)) o.BackoffStepMs = v;
            if(int.TryParse(GetVal("--maxbackoff", o.MaxBackoffMs.ToString()), out v)) o.MaxBackoffMs = v;
            if(int.TryParse(GetVal("--attachpid", "0"), out v)) o.AttachPid = v;

            return o;
        }
    }
}
