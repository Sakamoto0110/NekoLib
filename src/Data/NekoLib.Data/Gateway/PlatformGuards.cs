#nullable enable
using NekoLib;

namespace NekoLib.Data.Gateway 
{
    internal static class PlatformGuards
    {
        /// <summary>
        /// Retorna true quando o runtime não suporta geração dinâmica de código (AOT).
        /// </summary>
        public static bool IsAot()
        {
#if NET6_0_OR_GREATER
            return !System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported;
#else
            // .NET Framework (net481) não roda em AOT.
            return false;
#endif
        }

        /// <summary>
        /// Retorna true quando o runtime suporta Reflection.Emit / DynamicCode.
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
