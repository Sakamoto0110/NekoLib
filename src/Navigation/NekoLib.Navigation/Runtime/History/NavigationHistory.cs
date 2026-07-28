// FILE: PageNav.Core/Services/NavigationHistory.cs
using NekoLib.Navigation.Metadata;
using System.Collections.Generic;
using System.Linq;

namespace NekoLib.Navigation.Runtime.History
{
    /// <summary>
    /// Instance-scoped navigation history.
    /// Owned by NavigationContext.
    /// </summary>
    public sealed partial class NavigationHistory
    {
        private readonly Stack<PageHistoryEntry> _back = new();
        private readonly Stack<PageHistoryEntry> _forward = new();

        public bool CanGoBack => _back.Count > 0;
        public bool CanGoForward => _forward.Count > 0;

        // ------------------------------------------------------------
        // RECORDING
        // ------------------------------------------------------------

        public void Record(PageHistoryEntry entry)
        {
            if (entry == null)
                return;

            _back.Push(entry);
            _forward.Clear();
        }

        // ------------------------------------------------------------
        // BACK / FORWARD
        // ------------------------------------------------------------

        public bool TryPopBack(out PageHistoryEntry entry)
        {
            if (_back.Count == 0)
            {
                entry = null;
                return false;
            }

            entry = _back.Pop();
            return true;
        }

        /// <summary>
        /// Commits a previously inspected back entry only when it is still the
        /// current stack head. Lifecycle callbacks can reach this public history
        /// object, so the navigation gate alone cannot guarantee that the entry
        /// has not changed while a back navigation was in progress.
        /// </summary>
        internal bool TryPopExpectedBack(
            PageHistoryEntry expected,
            out PageHistoryEntry entry)
        {
            if (_back.Count == 0 ||
                !ReferenceEquals(_back.Peek(), expected))
            {
                entry = null!;
                return false;
            }

            entry = _back.Pop();
            return true;
        }

        public void PushForward(PageHistoryEntry entry)
        {
            if (entry != null)
                _forward.Push(entry);
        }

        public PageHistoryEntry PopForward()
            => _forward.Pop();

        // ------------------------------------------------------------
        // INSPECTION (debug / UI)
        // ------------------------------------------------------------

        public IEnumerable<PageHistoryEntry> HistoryBack
            => _back.ToList();

        public IEnumerable<PageHistoryEntry> HistoryForward
            => _forward.ToList();

        // ------------------------------------------------------------
        // RESET
        // ------------------------------------------------------------

        public void Clear()
        {
            _back.Clear();
            _forward.Clear();
        }
    }
}
