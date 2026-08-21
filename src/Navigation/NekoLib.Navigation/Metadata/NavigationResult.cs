using System;

namespace NekoLib.Navigation.Metadata;

/// <summary>
/// Immutable completion result for one forward navigation request. Operational,
/// registration, loading, and lifecycle failures still throw; guard denial and
/// redirect are normal outcomes represented here.
/// </summary>
public sealed class NavigationResult
{
    internal NavigationResult(
        Type requestedPage,
        Type? finalPage,
        bool succeeded,
        bool wasRedirected,
        string? denialReason)
    {
        if (requestedPage == null)
            throw new ArgumentNullException(nameof(requestedPage));

        if (succeeded && finalPage == null)
            throw new ArgumentNullException(nameof(finalPage));

        RequestedPage = requestedPage;
        FinalPage = finalPage;
        Succeeded = succeeded;
        WasRedirected = wasRedirected;
        DenialReason = denialReason;
    }

    /// <summary>The page type supplied by the caller.</summary>
    public Type RequestedPage { get; }

    /// <summary>
    /// The page that completed its lifecycle, or <c>null</c> when the request was
    /// denied without navigating a page.
    /// </summary>
    public Type? FinalPage { get; }

    /// <summary>True when a page completed the synchronous navigation lifecycle.</summary>
    public bool Succeeded { get; }

    /// <summary>True when at least one guard redirected the original request.</summary>
    public bool WasRedirected { get; }

    /// <summary>Optional reason for the terminal guard denial.</summary>
    public string? DenialReason { get; }
}
