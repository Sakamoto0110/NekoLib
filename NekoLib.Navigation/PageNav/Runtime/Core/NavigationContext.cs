/// <summary>
/// Passive container for navigation-scoped state and services.
/// </summary>
// FILE: PageNav.Core/Services/NavigationContext.cs
using NekoLib.Diagnostics;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Runtime.History;
using NekoLib.Navigation.Runtime.Services;
using System;

namespace NekoLib.Navigation.Runtime.Core
{
    public sealed class NavigationContext
    {
        public IPageHost Host { get; }
        public ServiceLocator Services { get; }
        public NavigationHistory History { get; }
       // public TimeSpan Timeout { get; }

        /// <summary>
        /// Optional diagnostics context. When not provided, navigation emits no logs/telemetry.
        /// </summary>
        public IDiagnostics Diagnostics { get; }

        public NavigationContext(
            IPageHost host,
            ServiceLocator services,
          //  TimeSpan timeout,
            IDiagnostics diagnostics = null)
        {
            Host = host ?? throw new ArgumentNullException(nameof(host));
            Services = services ?? throw new ArgumentNullException(nameof(services));
           // Timeout = timeout;
            Diagnostics = diagnostics;

            History = new NavigationHistory();
        }
    }
}
