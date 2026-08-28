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
        /// <summary>Gets the declared reuse policy.</summary>
        public PageReusePolicy Policy { get; }

        /// <summary>Initializes the attribute with a supported reuse policy.</summary>
        /// <param name="policy">Reuse policy recorded in the page descriptor.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="policy"/> is not a defined <see cref="PageReusePolicy"/> value.</exception>
        public PageReuseAttribute(PageReusePolicy policy)
        {
            if (!Enum.IsDefined(typeof(PageReusePolicy), policy))
                throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unsupported page reuse policy.");

            Policy = policy;
        }
    }
}
