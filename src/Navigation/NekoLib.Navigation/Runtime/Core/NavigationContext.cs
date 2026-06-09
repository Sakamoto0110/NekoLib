using NekoLib.Core.Diagnostics;
using NekoLib.Navigation.Contracts.Guards;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Runtime.History;
using NekoLib.Navigation.Runtime.Registry;
using NekoLib.Navigation.Runtime.Services;
using NekoLib.Navigation.Runtime.Session;
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

        /// <summary>
        /// The framework-owned mutable session. Same instance is returned through
        /// <see cref="User"/> as the <see cref="IUserContext"/> that guards read,
        /// so calling <c>Session.SignIn(...)</c> immediately satisfies role/auth
        /// guards on the next navigation.
        /// </summary>
        public NavigationSession Session { get; }

        public IUserContext User => Session;

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
        IDiagnosticsContext? diagnosticsContext = null)
        {
            Host = host ?? throw new ArgumentNullException(nameof(host));
            Services = services ?? throw new ArgumentNullException(nameof(services));
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            Platform = platform ?? throw new ArgumentNullException(nameof(platform));
            DiagnosticsContext = diagnosticsContext;
            Session = new NavigationSession();
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
