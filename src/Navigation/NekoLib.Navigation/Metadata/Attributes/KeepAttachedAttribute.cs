using System;

namespace NekoLib.Navigation.Metadata.Attributes { 
    /// <summary>
    /// Indicates that the page should remain attached to the visual tree when navigated away from.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class KeepAttachedAttribute : Attribute
    {
    }
}