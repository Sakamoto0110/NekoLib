using System;

namespace NekoLib.Navigation.Metadata.Attributes
{

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class PageLoadAttribute : Attribute
    {
        public NavigationLoadMode Mode { get; }

        public PageLoadAttribute(NavigationLoadMode mode)
        {
            if (!Enum.IsDefined(typeof(NavigationLoadMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported navigation load mode.");

            Mode = mode;
        }
    }
}
