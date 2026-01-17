using System;

namespace NekoLib.Navigation.Metadata;
/// <summary>
/// Declarative metadata for page registration.
/// This attribute defines DEFAULT behavior only.
/// All values can be overridden manually via PageRegistry.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PageBehaviorAttribute : Attribute
{
    // --------------------------------------------------------------------
    // Classification
    // --------------------------------------------------------------------

    /// <summary>
    /// Logical category of the page (Home, Modal, Popup, etc).
    /// Default: PageKind.Default
    /// </summary>
    public PageKind Kind { get; }

    // --------------------------------------------------------------------
    // Caching
    // --------------------------------------------------------------------

    /// <summary>
    /// Cache policy for page instances.
    /// Default: PageCachePolicy.Disabled
    /// </summary>
    public PageCachePolicy CachePolicy { get; }

    // --------------------------------------------------------------------
    // Optional overrides (nullable = not specified)
    // --------------------------------------------------------------------

    /// <summary>
    /// Optional name override used for registry and debugging.
    /// If null, page type name is used.
    /// </summary>
    public string NameOverride { get; set; }

    /// <summary>
    /// Optional timeout behavior override.
    /// If null, PageRegistry / framework default applies.
    /// </summary>
    public PageTimeoutBehavior? Timeout { get; set; }

    /// <summary>
    /// Optional load mode preference.
    /// If null, NavigationLoadMode.ShowImmediately is used.
    /// </summary>
    public NavigationLoadMode? LoadMode { get; set; }

    /// <summary>
    /// Optional classification tags.
    /// Used for lookup, filtering, and timeout resolution.
    /// </summary>
    public string[] Tags { get; set; }

    // --------------------------------------------------------------------
    // Constructor
    // --------------------------------------------------------------------

    public PageBehaviorAttribute(
        PageKind kind = PageKind.Default,
        PageCachePolicy cachePolicy = PageCachePolicy.Disabled)
    {
        Kind = kind;
        CachePolicy = cachePolicy;
    }
}
