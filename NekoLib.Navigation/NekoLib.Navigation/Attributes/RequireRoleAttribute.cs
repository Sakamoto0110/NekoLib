using NekoLib.Navigation.Contracts.Guards;
using NekoLib.Navigation.Runtime.Guards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Attributes
{
    public sealed class RequireRoleAttribute : GuardAttribute
    {
        public string Role { get; }
         

        public RequireRoleAttribute(string role)
        {
            Role = role;
        }

        public override IGuard CreateGuard()
        {
            return new RequireRoleGuard(Role, RedirectTo);
        }
    }


}