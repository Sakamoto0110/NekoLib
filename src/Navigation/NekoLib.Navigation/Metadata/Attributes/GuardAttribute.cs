using NekoLib.Navigation.Contracts.Guards;
using System;

namespace NekoLib.Navigation.Metadata.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public abstract class GuardAttribute : Attribute
    {
        public Type RedirectTo { get; set; }    
        public abstract IGuard CreateGuard();
    }

}