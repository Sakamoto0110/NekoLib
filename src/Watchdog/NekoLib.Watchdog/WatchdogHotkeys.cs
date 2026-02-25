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
    }
}
