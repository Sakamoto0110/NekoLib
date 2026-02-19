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
    public  sealed class NavigationContext
    {
        public IPageHost Host { get; }
        public ServiceLocator Services { get; }
        public PageRegistry Registry { get; }
        public NavigationHistory History { get; }
        public IUserContext User { get; }

        /// <summary>
        /// Optional diagnostics context. (You can omit this if you want only static NavigationDiagnostics.)
        /// </summary>
         public IPlatformAdapter Platform { get; }
        public NavigationDiagnostics Diagnostics { get; }
        public IDiagnosticsContext? DiagnosticsContext { get; }

        public NavigationEventHub Events => Diagnostics.Hub;

        public NavigationContext(
        IPageHost host,
        ServiceLocator services,
            PageRegistry registry,

        IPlatformAdapter platform,
        IDiagnosticsContext? diagnosticsContext = null, 
        IUserContext user = null)
        {
            Host = host ?? throw new ArgumentNullException(nameof(host));
            Services = services ?? throw new ArgumentNullException(nameof(services));
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            Platform = platform ?? throw new ArgumentNullException(nameof(platform));
            DiagnosticsContext = diagnosticsContext;
            User = user ?? new DefaultUserContext();
            History = new NavigationHistory();
            var hub = new NavigationEventHub();

            INavigationDiagnosticsSink? sink =
                diagnosticsContext != null
                    ? new DiagnosticsNavigationSink(diagnosticsContext)
                    : null;

            Diagnostics = new NavigationDiagnostics(hub, sink);
        }
      
    }
}
