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
    public sealed class RequireAnyPermissionsAttribute : GuardAttribute
    {
        private readonly string[] _permissions;

        public RequireAnyPermissionsAttribute(params string[] permissions)
        {
            _permissions = permissions;
        }

        public override IGuard CreateGuard( )
            => new RequireAnyPermissionGuard(_permissions);
    }


}