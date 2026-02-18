using NekoLib.Navigation.Contracts.Guards;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Runtime.Guards;
using System;
using System.Collections.Generic;
using System.Linq;

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
        public PageReusePolicy ReusePolicy { get; set; }

        /// <summary>Instance used when page is a singleton.</summary>
        
         public NavigationLoadMode WaitCompletionBeforeShow { get; set; }

        /// <summary>Additional classification tags.</summary>
        public HashSet<string> Tags { get; set; }
        public PageTimeoutBehavior Timeout { get; set; } = PageTimeoutBehavior.Default;
        public IGuard  Guard { get; set; }
        public PagePresentation Presentation { get; set; } = PagePresentation.Normal;
        /// <summary>
        /// Initializes the descriptor with empty stack and tag set.
        /// </summary>


        public void Guards(params IGuard[] guards)
        {
            Guard = guards == null || !guards.Any()
    ? null
    : guards.Length == 1
        ? guards[0]
        : new AndGuard(guards);
        }

        public PageDescriptor()
        {
             Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        public PageDescriptor(
       Type pageType,
       params IGuard[] guards)
        {
            PageType = pageType
                ?? throw new ArgumentNullException(nameof(pageType));

            Guard = guards == null || !guards.Any()
    ? null
    : guards.Length == 1
        ? guards[0]
        : new AndGuard(guards);
        }
    }

}
