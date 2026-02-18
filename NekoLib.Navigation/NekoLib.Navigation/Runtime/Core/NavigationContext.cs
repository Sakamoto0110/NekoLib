using NekoLib.Diagnostics.Contracts;
using NekoLib.Navigation.Contracts.Guards;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Runtime.History;
using NekoLib.Navigation.Runtime.Registry;
using NekoLib.Navigation.Runtime.Services;
using System;

namespace NekoLib.Navigation.Runtime.Core
{
    /// <summary>
    /// Passive container for navigation-scoped state and services.
    /// </summary>
    public sealed class NavigationContext
    {
        public IPageHost Host { get; }
        public ServiceLocator Services { get; }
        public PageRegistry Registry { get; }
        public NavigationHistory History { get; }
        public IUserContext User { get; }

        /// <summary>
        /// Optional diagnostics context. (You can omit this if you want only static NavigationDiagnostics.)
        /// </summary>
        public IDiagnosticsContext Diagnostics { get; }
        public IPlatformAdapter Platform { get; }
        
        public NavigationContext(
    IPageHost host,
    ServiceLocator services,
    PageRegistry registry,
    IPlatformAdapter platform,
    IUserContext user = null,
    IDiagnosticsContext diagnostics = null)
        {
            Host = host ?? throw new ArgumentNullException(nameof(host));
            Services = services ?? throw new ArgumentNullException(nameof(services));
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            Platform = platform ?? throw new ArgumentNullException(nameof(platform));

            User = user ?? new DefaultUserContext();
            Diagnostics = diagnostics;
            History = new NavigationHistory();
        }
    }
}
