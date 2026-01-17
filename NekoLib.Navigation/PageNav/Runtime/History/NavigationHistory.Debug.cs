// FILE: PageNav.Core/Services/NavigationHistory.Debug.cs
using NekoLib.Navigation.Metadata;
using System.Collections.Generic;
using System.Linq;

namespace NekoLib.Navigation.Runtime.History
{
    public sealed partial class NavigationHistory
    {
        /// <summary>
        /// Snapshot of back stack (top = most recent).
        /// </summary>
        public IReadOnlyList<PageHistoryEntry> BackStackSnapshot()
            => _back.Reverse().ToList();

        /// <summary>
        /// Snapshot of forward stack (top = next forward).
        /// </summary>
        public IReadOnlyList<PageHistoryEntry> ForwardStackSnapshot()
            => _forward.Reverse().ToList();

        /// <summary>
        /// Returns true if any history exists.
        /// </summary>
        public bool HasHistory
            => _back.Count > 0 || _forward.Count > 0;
    }
}
