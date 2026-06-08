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
    /// Navigation-scoped state created by <c>PageNavBootstrap.Start()</c>.
    /// Consumers usually access it through <c>NavigationService</c>, while tests
    /// and diagnostics may keep the returned context directly.
    /// </summary>
    public  sealed class NavigationContext
    {
        /// <summary>Platform host responsible for attaching and detaching pages.</summary>
        public IPageHost Host { get; }

        /// <summary>Locked runtime service registry for this navigation context.</summary>
        public ServiceLocator Services { get; }

        /// <summary>Registered page descriptors used for lookup and runtime policy.</summary>
        public PageRegistry Registry { get; }

        /// <summary>Back/forward history owned by this context.</summary>
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
        /// Platform adapter that created the host and platform services.
        /// </summary>
         public IPlatformAdapter Platform { get; }

        /// <summary>Navigation diagnostics emitter and event hub.</summary>
        public NavigationDiagnostics Diagnostics { get; }

        /// <summary>
        /// Optional bridge into <c>NekoLib.Diagnostics</c>. When omitted, local
        /// navigation events still emit through <see cref="Events"/>.
        /// </summary>
        public IDiagnosticsContext? DiagnosticsContext { get; }

        /// <summary>Convenience accessor for the navigation diagnostics event hub.</summary>
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
