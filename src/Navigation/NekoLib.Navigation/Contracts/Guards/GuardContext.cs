using NekoLib.Navigation.Telemetry;
using System;

namespace NekoLib.Navigation.Contracts.Guards
{
    /// <summary>Data passed to guards for a single navigation attempt.</summary>
    public sealed class GuardContext
    {
        /// <summary>Initializes the immutable context for one guard evaluation.</summary>
        /// <param name="targetPage">Concrete page type requested by the navigation attempt.</param>
        /// <param name="user">Read-only session state evaluated by the guard.</param>
        /// <param name="timing">Optional application timing correlation.</param>
        /// <exception cref="ArgumentNullException"><paramref name="targetPage"/> or <paramref name="user"/> is <see langword="null"/>.</exception>
        public GuardContext(
            Type targetPage,
            IUserContext user,
            NavigationTimingContext? timing = null)
        {
            TargetPage = targetPage ?? throw new ArgumentNullException(nameof(targetPage));
            User = user ?? throw new ArgumentNullException(nameof(user));
            Timing = timing;
        }

        /// <summary>Gets the concrete target page type.</summary>
        public Type TargetPage { get; }

        /// <summary>Gets the read-only authentication state for this evaluation.</summary>
        public IUserContext User { get; }

        /// <summary>
        /// Optional timing correlation supplied with the navigation request.
        /// Application guards may mark authentication completion through it.
        /// </summary>
        public NavigationTimingContext? Timing { get; }
    }
}
