// FILE: PageNav.Core/Models/NavigationEnums.cs

using System;

namespace NekoLib.Navigation.Metadata
{
    // ============================================================
    // PAGE CLASSIFICATION
    // ============================================================

    /// <summary>
    /// Logical category of a page.
    /// Used for navigation rules, overlays, and timeout resolution.
    /// </summary>
    [Flags]
    public enum PageKind
    {
        Default = 0,
        Home = 1,
        Modal = 2,
        Popup = 4
    }

    /// <summary>
    /// Defines how page instances are cached and reused.
    /// </summary>
     
    public enum PageReusePolicy
    {
        

        /// <summary>
        /// Always create a new instance, dispose on detach.
        /// </summary>
        Transient = 1,

        /// <summary>
        /// Single instance, released on navigation away.
        /// </summary>
        WeakSingleton = 2,

        /// <summary>
        /// Single instance, kept alive for entire context lifetime.
        /// </summary>
        StrongSingleton = 4,

        /// <summary>
        /// Multiple instances stacked (push/pop behavior).
        /// </summary>
        Stackable = 8,
         
    }

    // ============================================================
    // TIMEOUT
    // ============================================================

    /// <summary>
    /// Defines how a page interacts with global timeout logic.
    /// </summary>
    public enum PageTimeoutBehavior
    {
        /// <summary>
        /// Use framework default behavior.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Page is exempt from timeout logic.
        /// </summary>
        Exempt = 1,

        /// <summary>
        /// Reset timeout when navigating away from this page.
        /// </summary>
        ResetOnNavigateAway = 2,

        /// <summary>
        /// This page becomes the timeout target instead of Home.
        /// </summary>
        OverrideHome = 3,

        /// <summary>
        /// Ignore timeout completely while this page is active.
        /// </summary>
        IgnoreTimeout = 4
    }

    // ============================================================
    // NAVIGATION FLOW
    // ============================================================

    /// <summary>
    /// Defines how page loading is staged relative to UI attachment.
    /// </summary>
    public enum NavigationLoadMode
    {
        /// <summary>
        /// Attach page immediately, then load synchronously.
        /// </summary>
        ShowImmediately = 0,

        /// <summary>
        /// Fully load page before attaching to UI.
        /// </summary>
        LoadBeforeShow = 1,

        /// <summary>
        /// Attach immediately, load asynchronously in background.
        /// </summary>
        LoadInBackground = 2
    }

    /// <summary>
    /// Navigation behavior flags.
    /// </summary>
    [Flags]
    public enum NavigationBehavior
    {
        Default = 0,

        /// <summary>
        /// Do not block interaction / show mask during navigation.
        /// </summary>
        NoMask = 1,

        /// <summary>
        /// Page is transient (does not participate in history).
        /// </summary>
        Transient = 2,

        /// <summary>
        /// Do not record navigation in history.
        /// </summary>
        NoHistory = 4
    }

    // ============================================================
    // LIFECYCLE / DIAGNOSTICS
    // ============================================================

    /// <summary>
    /// Runtime lifecycle state used for diagnostics and leak detection.
    /// </summary>
    public enum PageLifecycleState
    {
        Created,
        Attached,
        Entering,
        Entered,
        Leaving,
        Detached,
        Released,
        Disposed
    }

    public enum TimeoutState
    {
        Uninitialized,
        Running,
        Paused,
        Fired,
        Stopped
    }
    

    public enum NavigationFailureKind
    {
        None = 0,

        /// <summary>
        /// Navigation was aborted because another navigation was already in progress
        /// (gate / reentrancy / drop policy).
        /// </summary>
        ReentrancyBlocked,

        /// <summary>
        /// Target page is not registered in PageRegistry.
        /// </summary>
        PageNotRegistered,

        /// <summary>
        /// Page instance could not be created (factory / ctor / DI failure).
        /// </summary>
        PageCreationFailed,

        /// <summary>
        /// Failure during page lifecycle callbacks (enter/exit).
        /// </summary>
        LifecycleFailed,

        /// <summary>
        /// Failure during background or preload stage.
        /// </summary>
        LoadFailed,

        /// <summary>
        /// Navigation triggered by timeout failed.
        /// </summary>
        TimeoutNavigationFailed,

        /// <summary>
        /// Unexpected exception.
        /// </summary>
        UnhandledException
    }

    public enum PageVisibilityPolicy
    {
        /// Default framework behavior
        KeepCached,

        /// Dispose page when it becomes invisible
        DisposeOnHide,

        /// Hide page and reveal underlying pages
        RevealUnderlying,

        /// Hide page but keep input blocked (modal-like)
        HideButBlockInput
    }
     
}
