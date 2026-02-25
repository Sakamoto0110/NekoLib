using NekoLib.Navigation.Contracts.Guards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Runtime.Guards {
    internal static class GuardComposer
    {
        public static IGuard And(IEnumerable<IGuard> guards)
            => new AndGuard(guards.ToArray());
    }
}
