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

        public PageHistoryEntry PopBack()
            => _back.Pop();

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
