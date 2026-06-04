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

        // -----------------------------------------------------------------
        // Letter VK constants + Vk(char) helper
        // -----------------------------------------------------------------

        [Fact]
        public void LetterConstants_MatchUppercaseAscii()
        {
            Assert.Equal(0x50u, WatchdogHotkeys.VK_P);
            Assert.Equal(0x51u, WatchdogHotkeys.VK_Q);
            Assert.Equal(0x52u, WatchdogHotkeys.VK_R);
        }

        [Theory]
        [InlineData('p', 0x50u)]
        [InlineData('P', 0x50u)]
        [InlineData('q', 0x51u)]
        [InlineData('r', 0x52u)]
        [InlineData('a', 0x41u)]
        [InlineData('Z', 0x5Au)]
        [InlineData('0', 0x30u)]
        [InlineData('9', 0x39u)]
        public void Vk_MapsAlphanumericToVirtualKey_CaseInsensitive(char key, uint expected)
        {
            Assert.Equal(expected, WatchdogHotkeys.Vk(key));
        }

        // -----------------------------------------------------------------
        // WatchdogHotkey binding type
        // -----------------------------------------------------------------

        [Fact]
        public void WatchdogHotkey_CtrlAlt_SetsCtrlAltModifiersAndKey()
        {
            var hk = WatchdogHotkey.CtrlAlt(WatchdogHotkeys.VK_P);

            Assert.Equal(WatchdogHotkeys.MOD_CONTROL | WatchdogHotkeys.MOD_ALT, hk.Modifiers);
            Assert.Equal(0x03u, hk.Modifiers);
            Assert.Equal(WatchdogHotkeys.VK_P, hk.VirtualKey);
            Assert.True(hk.Enabled);
        }

        [Fact]
        public void WatchdogHotkey_DefaultCtor_IsEnabled()
        {
            Assert.True(new WatchdogHotkey().Enabled);
        }
    }
}
