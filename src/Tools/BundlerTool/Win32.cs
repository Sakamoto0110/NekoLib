using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BundlerTool
{
    public static class Win32
    {
        [DllImport("user32.dll")]
        internal static extern bool SetProcessDPIAware();
    }
}
