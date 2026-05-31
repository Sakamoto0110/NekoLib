using System;

namespace NekoLib.Navigation.Metadata.Attributes
{

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class PageLoadAttribute : Attribute
    {
        public NavigationLoadMode Mode { get; }

        public PageLoadAttribute(NavigationLoadMode mode)
        {
            Mode = mode;
        }
    }
}