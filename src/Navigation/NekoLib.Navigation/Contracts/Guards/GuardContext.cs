using NekoLib.Navigation.Telemetry;
using System;

namespace NekoLib.Navigation.Contracts.Guards
{
    /// <summary>Data passed to guards for a single navigation attempt.</summary>
    public sealed class GuardContext
    {
        public GuardContext(
            Type targetPage,
            IUserContext user,
            NavigationTimingContext? timing = null)
        {
            TargetPage = targetPage ?? throw new ArgumentNullException(nameof(targetPage));
            User = user ?? throw new ArgumentNullException(nameof(user));
            Timing = timing;
        }

        public Type TargetPage { get; }
        public IUserContext User { get; }

        /// <summary>
        /// Optional timing correlation supplied with the navigation request.
        /// Application guards may mark authentication completion through it.
        /// </summary>
        public NavigationTimingContext? Timing { get; }
    }
}
