using System;
using System.Collections.Generic;

namespace NekoLib.Http.RuntimeTests.TheCatApi
{
    internal sealed class ScenarioResult
    {
        public string Scenario { get; set; } = "unofficial-thecatapi-provider-probe";
        public string TargetFramework { get; set; } = string.Empty;
        public string ProviderBaseAddress { get; set; } = "https://api.thecatapi.com/v1/";
        public DateTimeOffset StartedUtc { get; set; }
        public DateTimeOffset FinishedUtc { get; set; }
        public int ExitCode { get; set; }
        public bool ApiKeyPresent { get; set; }
        public string RunSubId { get; set; } = string.Empty;
        public List<ScenarioCheck> Checks { get; set; } = new List<ScenarioCheck>();
        public List<string> CleanupProblems { get; set; } = new List<string>();
    }

    internal sealed class ScenarioCheck
    {
        public string Name { get; set; } = string.Empty;
        public bool Passed { get; set; }
        public string Detail { get; set; } = string.Empty;
    }

    internal sealed class ScenarioCheckException : Exception
    {
        internal ScenarioCheckException(string message)
            : base(message)
        {
        }
    }

    internal static class ScenarioExitCodes
    {
        internal const int Success = 0;
        internal const int PrerequisiteMissing = 3;
        internal const int CheckFailed = 4;
        internal const int Timeout = 5;
        internal const int CleanupFailed = 6;
        internal const int Unexpected = 7;
        internal const int Interrupted = 8;
    }
}
