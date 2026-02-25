using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NekoLib
{
    public static class NekoLibVersion
    {
        // Versão da FAMÍLIA, não dos módulos
        public const int Major = 1;
        public const int Minor = 0;
        public const int Patch = 0;

        public static string String => $"{Major}.{Minor}.{Patch}";
    }
}

