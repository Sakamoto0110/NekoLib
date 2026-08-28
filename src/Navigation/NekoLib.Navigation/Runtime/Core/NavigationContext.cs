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

        /// <summary>Gets the platform page host owned by this context.</summary>
        public IPageHost Host { get; }
        /// <summary>Gets the locked context-scoped service registry.</summary>
        public ServiceLocator Services { get; }
        /// <summary>Gets the immutable page metadata registry.</summary>
        public PageRegistry Registry { get; }
        /// <summary>Gets the mutable back/forward history owned by this context.</summary>
        public NavigationHistory History { get; }
        /// <summary>Gets the built-in mutable authentication session.</summary>
        public NavigationSession Session { get; }
        /// <summary>Gets the session through the read-only guard contract.</summary>
        public IUserContext User => Session;
        /// <summary>Gets the platform adapter used to compose this context.</summary>
        public IPlatformAdapter Platform { get; }
        /// <summary>Gets the context-owned diagnostics publisher.</summary>
        public NavigationDiagnostics Diagnostics { get; }

        /// <summary>Optional independent logging writer selected at composition.</summary>
        public ILogger? Logger { get; }

        /// <summary>Gets the public subscriber-safe navigation outcome hub.</summary>
        public NavigationEventHub Events => Diagnostics.Hub;
    }
}
