using System;
using System.IO;
using System.Reflection;
using Xunit;

namespace NekoLib.Diagnostics.Tests.Unit
{
    public sealed class CrashHandlerTests
    {
        [Fact]
        public void CrashDetectedSubscriberFailure_DoesNotPreventCrashArtifacts()
        {
            var root = Path.Combine(Path.GetTempPath(), "nekolib-diagnostics-test-" + Guid.NewGuid().ToString("N"));

            try
            {
                var handler = new global::NekoLib.Diagnostics.CrashHandler(
                    new global::NekoLib.Diagnostics.CrashHandlerOptions
                    {
                        CrashRootDirectory = root,
                        DumpLevel = global::NekoLib.Diagnostics.CrashDumpLevel.None
                    });

                string crashTextPath = null;
                handler.CrashDetected += (s, e) => throw new InvalidOperationException("bad subscriber");
                handler.CrashBundleWritten += (s, e) => crashTextPath = e.CrashTextPath;

                InvokeHandleCrash(handler);

                Assert.False(string.IsNullOrWhiteSpace(crashTextPath));
                Assert.True(File.Exists(crashTextPath));
                Assert.Contains("unit-test", File.ReadAllText(crashTextPath));
            }
            finally
            {
                try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
            }
        }

        private static void InvokeHandleCrash(global::NekoLib.Diagnostics.CrashHandler handler)
        {
            var method = typeof(global::NekoLib.Diagnostics.CrashHandler).GetMethod(
                "HandleCrash",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(method);

            method.Invoke(handler, new object[]
            {
                "unit-test",
                new InvalidOperationException("unit-test"),
                false
            });
        }
    }
}
