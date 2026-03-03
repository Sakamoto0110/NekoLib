using NekoLib.Navigation.Contracts.Guards;
using NekoLib.Navigation.Runtime.Guards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Metadata.Attributes
{
    public sealed class RequireAuthenticatedAttribute : GuardAttribute
    {
        public override IGuard CreateGuard()
            => new RequireAuthenticatedGuard();
    }



}