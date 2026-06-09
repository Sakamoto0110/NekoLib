using System;
using System.Collections.Generic;
using System.Linq;

namespace NekoLib.Navigation.Metadata
{
    /// <summary>
    /// Single source of truth for "which registered page is the idle page". Shared
    /// by the runtime (idle/timeout navigation) and the bootstrap (idle-timeout
    /// wiring + <c>[PageTimeout]</c> placement validation) so the two never drift.
    /// </summary>
    internal static class IdlePageRules
    {
        /// <summary>
        /// True when a page qualifies as the idle page. Any one condition is enough
        /// (order independent):
        /// <list type="bullet">
        /// <item><description><see cref="PageRole.Idle"/> (set via <c>.AsIdle()</c> / <c>SetIdle&lt;T&gt;()</c>)</description></item>
        /// <item><description>carries the <c>idle</c> tag</description></item>
        /// <item><description>its <see cref="PageDescriptor.Name"/> contains "idle" (covers <c>Idle</c>, <c>IdlePage</c>, ...)</description></item>
        /// </list>
        /// </summary>
        public static bool IsIdleCandidate(PageDescriptor d)
            => d != null
               && (d.Role == PageRole.Idle
                   || HasIdleTag(d)
                   || HasIdleName(d));

        /// <summary>
        /// Resolves the idle descriptor by priority: explicit role first, then the
        /// <c>idle</c> tag, then the name convention. Returns <c>null</c> when no page
        /// qualifies. "MainPage" is deliberately not a fallback — a hub page is not
        /// the same as the page the runtime drops back to on inactivity.
        /// </summary>
        public static PageDescriptor Resolve(IEnumerable<PageDescriptor> descriptors)
        {
            if (descriptors == null)
                return null;

            var all = descriptors as IList<PageDescriptor> ?? descriptors.ToList();

            return all.FirstOrDefault(d => d != null && d.Role == PageRole.Idle)
                ?? all.FirstOrDefault(HasIdleTag)
                ?? all.FirstOrDefault(HasIdleName);
        }

        private static bool HasIdleTag(PageDescriptor d)
            => d?.Tags != null
               && d.Tags.Contains("idle", StringComparer.OrdinalIgnoreCase);

        private static bool HasIdleName(PageDescriptor d)
            => d?.Name != null
               && d.Name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
