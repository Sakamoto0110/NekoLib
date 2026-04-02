using NekoLib.Navigation.Contracts.Guards;
using System.Collections.Generic;
using System.Linq;

namespace NekoLib.Navigation.Runtime.Guards {
    internal static class GuardComposer
    {
        public static IGuard And(IEnumerable<IGuard> guards)
            => new AndGuard(guards.ToArray());
    }
}
