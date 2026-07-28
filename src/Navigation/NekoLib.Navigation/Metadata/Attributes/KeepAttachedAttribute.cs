using System;

namespace NekoLib.Navigation.Metadata.Attributes { 
    /// <summary>
    /// Requests that a reusable page remain attached to the visual tree when
    /// navigated away from. The page must implement <c>IPageVisibility</c>;
    /// otherwise the runtime falls back to detaching it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class KeepAttachedAttribute : Attribute
    {
    }
}
