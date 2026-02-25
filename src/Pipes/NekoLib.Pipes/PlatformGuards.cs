using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NekoLib.Pipes
{
    internal static class PlatformGuards
    {
#if NET9
    public const bool IsModern = true;
#else
        public const bool IsModern = false;
#endif
    }
}