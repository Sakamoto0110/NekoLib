using System;

namespace NekoLib.Navigation.Metadata;
/// <summary>
/// Declarative defaults for page registration. Attribute values are merged into
/// the page descriptor during assembly scanning and can be overridden by manual
/// bootstrap configuration.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PageBehaviorAttribute : Attribute
{
    // --------------------------------------------------------------------
    // Classification
    // --------------------------------------------------------------------

    /// <summary>
    /// Presentation category of the page. This describes how the runtime should
    /// present the page relative to the current page.
    /// Default: <see cref="PagePresentationMode.Replace"/>.
    /// </summary>
    public PagePresentationMode Kind { get; }

    // --------------------------------------------------------------------
    // Caching
    // --------------------------------------------------------------------

    /// <summary>
    /// Cache policy for page instances.
    /// Default: PageReusePolicy.Transient
    /// </summary>
    public PageReusePolicy ReusePolicy { get; }

    // --------------------------------------------------------------------
    // Optional overrides (nullable = not specified)
    // --------------------------------------------------------------------

    /// <summary>
    /// Optional name override used for registry and debugging.
    /// If null, page type name is used.
    /// </summary>
    public string NameOverride { get; set; }

    /// <summary>
    /// Optional timeout behavior override. Idle-page selection is controlled by
    /// <see cref="PageRole.Idle"/> / <c>SetIdle&lt;TPage&gt;()</c>; this property
    /// is retained for timeout-specific metadata.
    /// </summary>
    public PageTimeoutPolicy? Timeout { get; set; }

    /// <summary>
    /// Optional load mode preference.
    /// If null, NavigationLoadMode.ShowImmediately is used.
    /// </summary>
    public NavigationLoadMode? LoadMode { get; set; }

    /// <summary>
    /// Optional classification tags. The runtime uses tags for convention-based
    /// lookup, including the <c>idle</c> fallback.
    /// </summary>
    public string[] Tags { get; set; }

    // --------------------------------------------------------------------
    // Constructor
    // --------------------------------------------------------------------

    public PageBehaviorAttribute(
        PagePresentationMode kind = PagePresentationMode.Replace,
        PageReusePolicy reusePolicy = PageReusePolicy.Transient)
    {
        Kind = kind;
        ReusePolicy = reusePolicy;
    }
}
