using System;
using System.Collections.Generic;

namespace NekoLib.Watchdog
{
    /// <summary>
    /// WinAPI hotkey constants + helpers. Exposes broad VK range so you can register more later.
    /// </summary>
    public static class WatchdogHotkeys
    {
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;

        public const uint VK_F1 = 0x70;
        public const uint VK_F24 = 0x87;

        // Letter keys used by the default watchdog control hotkeys (VK codes for
        // A-Z and 0-9 equal their uppercase ASCII value).
        public const uint VK_P = 0x50;
        public const uint VK_Q = 0x51;
        public const uint VK_R = 0x52;

        /// <summary>
        /// Enumerates all possible virtual key codes that WinAPI hotkeys can accept.
        /// This does not guarantee they are meaningful on a given keyboard/layout.
        /// </summary>
        public static IEnumerable<uint> EnumerateAllVirtualKeys()
        {
            for(uint vk = 0x01; vk <= 0xFE; vk++)
                yield return vk;
        }

        /// <summary>
        /// Helper to build modifiers from booleans.
        /// </summary>
        public static uint Mods(bool ctrl, bool shift, bool alt, bool win)
        {
            uint m = 0;
            if(ctrl) m |= MOD_CONTROL;
            if(shift) m |= MOD_SHIFT;
            if(alt) m |= MOD_ALT;
            if(win) m |= MOD_WIN;
            return m;
        }

        /// <summary>
        /// Virtual-key code for an alphanumeric character (case-insensitive).
        /// Valid for A-Z and 0-9, whose VK codes equal their uppercase ASCII value.
        /// </summary>
        public static uint Vk(char key)
        {
            return (uint)char.ToUpperInvariant(key);
        }
    }

    /// <summary>
    /// A single global hotkey binding (modifier mask + virtual key) for a watchdog
    /// control action. Used by <c>WatchdogOptions</c> to make the pause/resume/stop
    /// hotkeys configurable instead of hardcoded.
    /// </summary>
    public sealed class WatchdogHotkey
    {
        public uint Modifiers { get; set; }
        public uint VirtualKey { get; set; }

        /// <summary>When false the binding is skipped at registration time.</summary>
        public bool Enabled { get; set; } = true;

        public WatchdogHotkey()
        {
        }

        public WatchdogHotkey(uint modifiers, uint virtualKey)
        {
            Modifiers = modifiers;
            VirtualKey = virtualKey;
            Enabled = true;
        }

        /// <summary>Convenience for the common Ctrl+Alt+&lt;key&gt; combo.</summary>
        public static WatchdogHotkey CtrlAlt(uint virtualKey)
        {
            return new WatchdogHotkey(
                WatchdogHotkeys.MOD_CONTROL | WatchdogHotkeys.MOD_ALT,
                virtualKey);
        }
    }
}
