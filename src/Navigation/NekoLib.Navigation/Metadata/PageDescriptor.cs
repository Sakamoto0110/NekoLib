using NekoLib.Navigation.Contracts.Guards;
using System;
using System.Collections.Generic;

namespace NekoLib.Navigation.Metadata
{
    /// <summary>
    /// Immutable metadata describing a registered page. Built during bootstrap by
    /// merging descriptor defaults, attributes, and manual configuration.
    /// </summary>
    public sealed class PageDescriptor
    {
        /// <summary>Concrete page type registered with the runtime.</summary>
        public Type PageType { get; }

        /// <summary>Logical name used for lookup, diagnostics, and history display.</summary>
        public string Name { get; }

        /// <summary>Semantic role such as normal or idle page.</summary>
        public PageRole Role { get; }

        /// <summary>Presentation mode requested for the page.</summary>
        public PagePresentationMode Presentation { get; }

        /// <summary>Instance reuse policy used by the runtime page cache.</summary>
        public PageReusePolicy ReusePolicy { get; }

        /// <summary>
        /// Idle timeout in seconds declared on the idle page, or <c>null</c> when the
        /// page declares none. Only the idle page may carry a value (enforced at
        /// bootstrap); it overrides the global <c>UseIdleTimeout(milliseconds)</c>.
        /// </summary>
        public int? IdleTimeoutSeconds { get; }

        /// <summary>Preferred load timing for pages that implement background loading.</summary>
        public NavigationLoadMode LoadMode { get; }

        /// <summary>Whether authentication/authorization guards should be bypassed.</summary>
        public bool AllowAnonymous { get; }

        /// <summary>
        /// Classification tags used for querying and grouping.
        /// </summary>
        public IReadOnlyList<string> Tags { get; }

        /// <summary>
        /// Navigation guard pipeline. <c>null</c> means no restrictions.
        /// </summary>
        public IGuard? Guard { get; }

        /// <summary>
        /// When true, the runtime hides the page but keeps it attached when another
        /// page replaces it, provided the page is reusable, not disposed, and
        /// implements <c>IPageVisibility</c>. Otherwise it is detached.
        /// </summary>
        public bool KeepAttachedWhenHidden { get; }  
        internal PageDescriptor(
            Type pageType,
            string name,
            PageRole role,
            PagePresentationMode presentation,
            PageReusePolicy reusePolicy,
            int? idleTimeoutSeconds,
            NavigationLoadMode loadMode,
            bool allowAnonymous,
            IReadOnlyList<string> tags,
            IGuard? guard,
            bool keepAttachedWhenHidden)
        {
            PageType = pageType;
            Name = name;
            Role = role;
            Presentation = presentation;
            ReusePolicy = reusePolicy;
            IdleTimeoutSeconds = idleTimeoutSeconds;
            LoadMode = loadMode;
            AllowAnonymous = allowAnonymous;
            Tags = tags;
            Guard = guard;
            KeepAttachedWhenHidden = keepAttachedWhenHidden;
         }
    }
}
