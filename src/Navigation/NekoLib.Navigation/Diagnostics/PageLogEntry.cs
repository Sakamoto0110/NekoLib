using NekoLib.Core.Diagnostics;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Runtime;
using System;

namespace NekoLib.Navigation.Diagnostics
{
    /// <summary>
    /// Immutable navigation log entry used for diagnostics and tracing.
    /// This is NOT a general-purpose logger entry.
    /// </summary>
    public   class PageLogEntry : LogEntry
    {
        // --------------------------------------------------------------------
        // Identity
        // --------------------------------------------------------------------

        /// <summary>Source page type (null for first navigation).</summary>
        public Type FromPageType { get; }

        /// <summary>Source page logical name.</summary>
        public string FromPageName { get; }

        /// <summary>Target page type.</summary>
        public Type ToPageType { get; }

        /// <summary>Target page logical name.</summary>
        public string ToPageName { get; }

        // --------------------------------------------------------------------
        // Context
        // --------------------------------------------------------------------

        /// <summary>UTC timestamp when navigation was initiated.</summary>
        public DateTime TimestampUtc { get; }

        /// <summary>Navigation behavior flags.</summary>
        public PagePresentationMode Presentation { get; }
        /// <summary>Load mode used for navigation.</summary>
        public NavigationLoadMode LoadMode { get; }
        public PageReusePolicy ReusePolicy { get; }

        /// <summary>
        /// True if this navigation was triggered by timeout logic.
        /// </summary>
        public bool IsTimeout { get; }

        /// <summary>
        /// True if this navigation represents a backward navigation.
        /// </summary>
        public bool IsBackNavigation { get; }

        /// <summary>
        /// True if navigation completed successfully.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Optional error message if navigation failed.
        /// </summary>
        public string Error { get; }

        public NavigationFailureKind FailureKind { get; }

        // --------------------------------------------------------------------
        // Constructor
        // --------------------------------------------------------------------

        public PageLogEntry(
            Type fromType,
            string fromName,
            Type toType,
            string toName,
            NavigationArgs args,
            bool success,
            PagePresentationMode navigationBehavior,
            NavigationLoadMode navigationLoadMode,
            PageReusePolicy reusePolicy,
            NavigationFailureKind failureKind = NavigationFailureKind.None,
            bool isTimeout = false,
            bool isBackNavigation = false,
            string error = null
            
            ) :
            base(DateTime.UtcNow, success? LogLevel.Info:LogLevel.Error, "",null)
        {
            FromPageType = fromType;
            FromPageName = fromName;
            ToPageType = toType ?? throw new ArgumentNullException(nameof(toType));
            ToPageName = toName ?? toType.Name;
             
            TimestampUtc = DateTime.UtcNow;

            ReusePolicy = reusePolicy;
            Presentation = navigationBehavior;
            LoadMode = navigationLoadMode;

            FailureKind = failureKind;

            IsTimeout = isTimeout;
            IsBackNavigation = isBackNavigation;
            Success = success;
            Error = error;
        }

        // --------------------------------------------------------------------
        // Debug helpers
        // --------------------------------------------------------------------

        public override string ToString()
        {
            var dir = IsBackNavigation ? "BACK" : "NAV";
            var status = Success ? "OK" : "FAIL";

            return $"[{TimestampUtc:HH:mm:ss}] {dir} {FromPageName ?? "<null>"} -> {ToPageName} " +
                   $"({status}, {Presentation} |, {LoadMode} | {ReusePolicy})";
        }
    }
}
