using System;

namespace NekoLib.Navigation.Metadata
{
    /// <summary>
    /// Snapshot stored in navigation history for a page that can be revisited with
    /// <c>GoBackAsync</c>. The state value is captured from <c>IPageStateful</c>
    /// when available and passed back to the page during back-navigation.
    /// </summary>
    public sealed class PageHistoryEntry
    {
        /// <summary>Concrete page type to recreate or resolve from cache.</summary>
        public Type PageType { get; }

        /// <summary>Logical page name captured for diagnostics and history display.</summary>
        public string PageName { get; }

        /// <summary>
        /// Page-owned history state. This is not the original forward-navigation
        /// payload; it is the value returned by <c>IPageStateful.CaptureState()</c>.
        /// </summary>
        public object State { get; }

        /// <summary>Time the history entry was created.</summary>
        public DateTime Timestamp { get; }

        public PageHistoryEntry(Type pageType, string pageName, object state)
        {
            PageType = pageType;
            PageName = pageName;
            State = state;
            Timestamp = DateTime.Now;
        }
    }

}
