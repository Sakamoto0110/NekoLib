using NekoLib.Navigation.Contracts.Pages;
using System;
using System.Collections.Generic;

namespace NekoLib.Navigation.Metadata
{
    // ============================================================
    // DESCRIPTOR
    // ============================================================

    /// <summary>
    /// Contains all runtime metadata about a registered page,
    /// including type, name, cache state, tags, and stack instances.
    /// </summary>
      public  sealed class PageDescriptor
    {
        /// <summary>CLR type that implements IPageView.</summary>
        public Type PageType { get; set; }

        /// <summary>Unique page name used as dictionary key.</summary>
        public string Name { get; set; }

        /// <summary>General category of the page.</summary>
        public PageKind Kind { get; set; }

        /// <summary>Caching rules for resolving pages.</summary>
        public PageCachePolicy CachePolicy { get; set; }

        /// <summary>Instance used when page is a singleton.</summary>
        public IPageView CachedInstance { get; set; }

        /// <summary>Stack for stackable pages.</summary>
        public Stack<IPageView> StackInstances { get; private set; }
        public NavigationLoadMode WaitCompletionBeforeShow { get; set; }

        /// <summary>Additional classification tags.</summary>
        public HashSet<string> Tags { get; set; }
        public PageTimeoutBehavior Timeout { get; set; } = PageTimeoutBehavior.Default;
      
        /// <summary>
        /// Initializes the descriptor with empty stack and tag set.
        /// </summary>
        public PageDescriptor()
        {
            StackInstances = new Stack<IPageView>();
            Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

}
