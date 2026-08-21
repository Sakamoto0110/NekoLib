using NekoLib.Core.Logging;
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
    /// Consumers usually access it through <c>NavigationService</c>.
    /// </summary>
    public sealed class NavigationContext
    {
        internal NavigationContext(
            IPageHost host,
            ServiceLocator services,
            PageRegistry registry,
            IPlatformAdapter platform,
            ILogger? logger = null)
        {
            Host = host ?? throw new ArgumentNullException(nameof(host));
            Services = services ?? throw new ArgumentNullException(nameof(services));
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            Platform = platform ?? throw new ArgumentNullException(nameof(platform));
            Logger = logger;
            Session = new NavigationSession();
            History = new NavigationHistory();

            var hub = new NavigationEventHub();
            INavigationDiagnosticsSink? sink = logger == null
                ? null
                : new LoggingNavigationSink(logger);
            Diagnostics = new NavigationDiagnostics(hub, sink);
        }

        public IPageHost Host { get; }
        public ServiceLocator Services { get; }
        public PageRegistry Registry { get; }
        public NavigationHistory History { get; }
        public NavigationSession Session { get; }
        public IUserContext User => Session;
        public IPlatformAdapter Platform { get; }
        public NavigationDiagnostics Diagnostics { get; }

        /// <summary>Optional independent logging writer selected at composition.</summary>
        public ILogger? Logger { get; }

        public NavigationEventHub Events => Diagnostics.Hub;
    }
}
