using System;

namespace NekoLib.Navigation.Metadata.Attributes
{
    /// <summary>
    /// Declares how the runtime caches the page instance:
    /// <see cref="PageReusePolicy.Transient"/> (new each navigation, disposed on
    /// leave), <see cref="PageReusePolicy.StrongSingleton"/> (one strong-referenced
    /// instance for the context lifetime), or <see cref="PageReusePolicy.WeakSingleton"/>
    /// (one weakly-referenced instance reused while still alive, recreated after GC).
    /// <para>
    /// The bootstrap DSL (<c>.Transient()</c> / <c>.StrongSingleton()</c> /
    /// <c>.WeakSingleton()</c>) overrides this attribute.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class PageReuseAttribute : Attribute
    {
        public PageReusePolicy Policy { get; }

        public PageReuseAttribute(PageReusePolicy policy)
        {
            Policy = policy;
        }
    }
}
