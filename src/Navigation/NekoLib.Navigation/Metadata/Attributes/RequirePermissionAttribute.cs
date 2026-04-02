using NekoLib.Navigation.Contracts.Guards;
using NekoLib.Navigation.Runtime.Guards;
using System;

namespace NekoLib.Navigation.Metadata.Attributes

{
    public sealed class RequirePermissionAttribute : GuardAttribute
    {
        public string Permission { get; }
        private readonly Type Redirect;

        public RequirePermissionAttribute(string permission, Type redirect)
        {
            Permission = permission;
            Redirect = redirect;
        }

        public override IGuard CreateGuard()
            => new RequirePermissionGuard(Permission, Redirect);
    }
}