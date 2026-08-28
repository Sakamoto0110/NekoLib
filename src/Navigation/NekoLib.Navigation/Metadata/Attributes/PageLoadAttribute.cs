using System;

namespace NekoLib.Navigation.Metadata.Attributes
{

    /// <summary>Declares when a page becomes visible relative to its loading work.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class PageLoadAttribute : Attribute
    {
        /// <summary>Gets the declared page load mode.</summary>
        public NavigationLoadMode Mode { get; }

        /// <summary>Initializes the attribute with a supported load mode.</summary>
        /// <param name="mode">Load mode recorded in the page descriptor.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not a defined <see cref="NavigationLoadMode"/> value.</exception>
        public PageLoadAttribute(NavigationLoadMode mode)
        {
            if (!Enum.IsDefined(typeof(NavigationLoadMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported navigation load mode.");

            Mode = mode;
        }
    }
}
