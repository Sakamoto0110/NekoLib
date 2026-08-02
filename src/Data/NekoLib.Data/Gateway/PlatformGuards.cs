#nullable enable
using NekoLib;

namespace NekoLib.Data.Gateway 
{
    internal static class PlatformGuards
    {
        /// <summary>
        /// Returns whether the runtime does not support dynamic code generation.
        /// </summary>
        public static bool IsAot()
        {
#if NET6_0_OR_GREATER
            return !System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported;
#else
            // .NET Framework net481 does not run as AOT.
            return false;
#endif
        }

        /// <summary>
        /// Returns whether the runtime supports Reflection.Emit dynamic code.
        /// </summary>
        public static bool SupportsDynamicIL()
        {
#if NET6_0_OR_GREATER
            return System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported;
#else
            return true;
#endif
        }
    }
}
