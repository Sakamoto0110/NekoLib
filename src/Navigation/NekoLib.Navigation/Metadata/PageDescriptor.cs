using NekoLib.Navigation.Contracts.Guards;
using System;
using System.Collections.Generic;

namespace NekoLib.Navigation.Metadata
{
    /// <summary>
    /// Immutable metadata describing a registered page.
    /// Built during bootstrap by merging defaults,
    /// attributes, and manual configuration.
    /// </summary>
    public sealed class PageDescriptor
    {
        public Type PageType { get; }

        public string Name { get; }

        public PageRole Role { get; }

        public PagePresentationMode Presentation { get; }

        public PageReusePolicy ReusePolicy { get; }

        public PageTimeoutPolicy TimeoutPolicy { get; }

        public NavigationLoadMode LoadMode { get; }

        public bool AllowAnonymous { get; }

        /// <summary>
        /// Classification tags used for querying and grouping.
        /// </summary>
        public IReadOnlyList<string> Tags { get; }

        /// <summary>
        /// Navigation guard pipeline.
        /// Null means no restrictions.
        /// </summary>
        public IGuard? Guard { get; }
        public bool KeepAttachedWhenHidden { get; }  
        internal PageDescriptor(
            Type pageType,
            string name,
            PageRole role,
            PagePresentationMode presentation,
            PageReusePolicy reusePolicy,
            PageTimeoutPolicy timeoutPolicy,
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
            TimeoutPolicy = timeoutPolicy;
            LoadMode = loadMode;
            AllowAnonymous = allowAnonymous;
            Tags = tags;
            Guard = guard;
            KeepAttachedWhenHidden = keepAttachedWhenHidden;
         }
    }
}