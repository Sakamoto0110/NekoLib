using Xunit;

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
        [InlineData("log_history", "LogHistory")]
        [InlineData("exception_notify", "ExceptionNotify")]
        public void Command_HasExpectedWireValue(string expected, string member)
        {
            var field = typeof(WatchdogCommands).GetField(member);
            Assert.NotNull(field);
            Assert.Equal(expected, (string)field.GetValue(null));
        }
    }
}
