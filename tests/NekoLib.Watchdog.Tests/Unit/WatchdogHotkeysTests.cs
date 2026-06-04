using System.Linq;
using Xunit;

namespace NekoLib.Watchdog.Tests.Unit
{
    /// <summary>
    /// Unit tests for <see cref="WatchdogHotkeys"/> — the public WinAPI hotkey
    /// constant table and helpers. Pure value logic with no Win32 calls, so it is
    /// safe to assert directly on both target frameworks.
    /// </summary>
    public class WatchdogHotkeysTests
    {
        // -----------------------------------------------------------------
        // Modifier constants
        // -----------------------------------------------------------------

        [Fact]
        public void ModifierConstants_HaveExpectedWin32Values()
        {
            Assert.Equal(0x0001u, WatchdogHotkeys.MOD_ALT);
            Assert.Equal(0x0002u, WatchdogHotkeys.MOD_CONTROL);
            Assert.Equal(0x0004u, WatchdogHotkeys.MOD_SHIFT);
            Assert.Equal(0x0008u, WatchdogHotkeys.MOD_WIN);
        }

        [Fact]
        public void FunctionKeyConstants_SpanF1ToF24()
        {
            Assert.Equal(0x70u, WatchdogHotkeys.VK_F1);
            Assert.Equal(0x87u, WatchdogHotkeys.VK_F24);
        }

        // -----------------------------------------------------------------
        // Mods() flag builder
        // -----------------------------------------------------------------

        [Fact]
        public void Mods_NoModifiers_IsZero()
        {
            Assert.Equal(0u, WatchdogHotkeys.Mods(false, false, false, false));
        }

        [Fact]
        public void Mods_AllModifiers_CombineToFullMask()
        {
            var expected = WatchdogHotkeys.MOD_CONTROL
                         | WatchdogHotkeys.MOD_SHIFT
                         | WatchdogHotkeys.MOD_ALT
                         | WatchdogHotkeys.MOD_WIN;

            Assert.Equal(expected, WatchdogHotkeys.Mods(true, true, true, true));
        }

        [Theory]
        [InlineData(true, false, false, false, 0x0002u)] // ctrl
        [InlineData(false, true, false, false, 0x0004u)] // shift
        [InlineData(false, false, true, false, 0x0001u)] // alt
        [InlineData(false, false, false, true, 0x0008u)] // win
        [InlineData(true, false, true, false, 0x0003u)]  // ctrl+alt (the runtime's pause/resume/stop combo)
        public void Mods_IndividualAndCombined_MapToExpectedFlags(
            bool ctrl, bool shift, bool alt, bool win, uint expected)
        {
            Assert.Equal(expected, WatchdogHotkeys.Mods(ctrl, shift, alt, win));
        }

        // -----------------------------------------------------------------
        // EnumerateAllVirtualKeys()
        // -----------------------------------------------------------------

        [Fact]
        public void EnumerateAllVirtualKeys_Covers0x01Through0xFE()
        {
            var keys = WatchdogHotkeys.EnumerateAllVirtualKeys().ToList();

            Assert.Equal(254, keys.Count);
            Assert.Equal(0x01u, keys.First());
            Assert.Equal(0xFEu, keys.Last());
        }

        [Fact]
        public void EnumerateAllVirtualKeys_IsContiguousAndDistinct()
        {
            var keys = WatchdogHotkeys.EnumerateAllVirtualKeys().ToList();

            Assert.Equal(keys.Count, keys.Distinct().Count());
            for (int i = 1; i < keys.Count; i++)
                Assert.Equal(keys[i - 1] + 1, keys[i]);
        }
    }
}
