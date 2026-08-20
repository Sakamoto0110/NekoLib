using Xunit;
using System.Linq;
using System.Reflection;

namespace NekoLib.Watchdog.Tests.Unit
{
    /// <summary>
    /// Pins the RPC command names. These strings are a wire-protocol contract
    /// between <c>WatchdogController</c> (client) and <c>WatchdogRuntime</c>'s
    /// handler map (server) — an accidental rename would silently break control
    /// commands at runtime, so the exact literals are locked here.
    /// </summary>
    public class WatchdogCommandsTests
    {
        [Theory]
        [InlineData("ping", "Ping")]
        [InlineData("status", "Status")]
        [InlineData("pause", "Pause")]
        [InlineData("resume", "Resume")]
        [InlineData("restart", "Restart")]
        [InlineData("stop", "Stop")]
        public void PublicCommand_HasExpectedWireValue(string expected, string member)
        {
            var field = typeof(WatchdogCommands).GetField(member);
            Assert.NotNull(field);
            Assert.Equal(expected, (string)field.GetValue(null));
        }

        [Fact]
        public void PublicCommands_ContainOnlySupportedControlOperations()
        {
            var names = typeof(WatchdogCommands)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(field => field.Name)
                .OrderBy(name => name)
                .ToArray();

            Assert.Equal(
                new[] { "Pause", "Ping", "Restart", "Resume", "Status", "Stop" },
                names);
        }

        [Theory]
        [InlineData("log_history", "LogHistory")]
        [InlineData("exception_notify", "ExceptionNotify")]
        [InlineData("protocol_version", "ProtocolVersion")]
        [InlineData("attach_status", "AttachStatus")]
        public void InternalCommand_HasExpectedWireValue(string expected, string member)
        {
            var field = typeof(WatchdogCommands).GetField(
                member,
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(field);
            Assert.Equal(expected, (string)field.GetValue(null));
        }
    }
}
