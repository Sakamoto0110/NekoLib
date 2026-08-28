using System;

namespace NekoLib.Navigation.Metadata.Attributes

{
    /// <summary>
    /// Marks a page as exempt from the runtime's implicit authentication guard.
    /// Explicit guards declared on the same page are still evaluated.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class AllowAnonymousAttribute : Attribute
    {
    }
}
