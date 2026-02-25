
using NekoLib.Diagnostics.Contracts;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Contracts.Runtime;
using NekoLib.Navigation.Infrastructure;
using NekoLib.Navigation.Runtime.Core;
using NekoLib.Navigation.Runtime.Factories;
using NekoLib.Navigation.Runtime.Registry;
using NekoLib.Navigation.Runtime.Services;
using System;
using System.Reflection;

namespace NekoLib.Navigation.Bootstrap
{
   

    /// <summary>
    /// Fluent bootstrap to initialize PageNav in 1–3 lines,
    /// while still allowing hybrid registration (attributes + manual tweaks).
    /// </summary>
    public sealed class PageNavBootstrap
    {
        private readonly object _nativeHost;
        private readonly IPlatformAdapter _platform;
        private Assembly _pagesAssembly;
        private Action<PageRegistryConfigurator> _pageConfig;
        private Action<ServiceLocator, IPlatformAdapter> _serviceConfig;

        private int _timeoutSeconds = 10;

        private IDiagnosticsContext _diagnostics;

         
        private PageNavBootstrap(object nativeHost, IPlatformAdapter adapter)
        {
            if (nativeHost == null) throw new ArgumentNullException(nameof(nativeHost));
            _nativeHost = nativeHost;
            _platform = adapter;
        }
        private PageNavBootstrap(object nativeHost)
        {
            if (nativeHost == null) throw new ArgumentNullException(nameof(nativeHost));
            _nativeHost = nativeHost;
     
        }
        // --------------------------------------------------------------------
        // Entry points
        // --------------------------------------------------------------------
 


        /// <summary>
        /// Use a specific platform adapter (WinForms/WPF/etc). You can call this multiple
        /// times across app startup, but resolution locks at first context creation.
        /// </summary>
        public static PageNavBootstrap Use<TPlatform>(object nativeHost) where TPlatform : IPlatformAdapter, new()
        {
            var adapter = new TPlatform();
            PlatformRegistry.Register(adapter);
             
            return new PageNavBootstrap(nativeHost,adapter);
        }
      

        /// <summary>
        /// If you already registered adapters manually, use this.
        /// </summary>
        public static PageNavBootstrap UseRegistered(object nativeHost)
            => new PageNavBootstrap(nativeHost);

        // --------------------------------------------------------------------
        // Configuration
        // --------------------------------------------------------------------

        

        public PageNavBootstrap UseRegistry(PageRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            return this;
        }

        public PageNavBootstrap UseDiagnostics(IDiagnosticsContext diagnostics)
        {
            _diagnostics = diagnostics;
            return this;
        }

        /// <summary>
        /// Auto-register pages by scanning an assembly for IPageView + [PageBehavior] metadata.
        /// </summary>
        public PageNavBootstrap RegisterPagesFromAssembly(Assembly asm)
        {
            _pagesAssembly = asm ?? throw new ArgumentNullException(nameof(asm));
            return this;
        }

        /// <summary>
        /// Manual tweaks after attribute defaults. (Hybrid mode)
        /// </summary>
        public PageNavBootstrap ConfigurePages(Action<PageRegistryConfigurator> configure)
        {
            _pageConfig = configure;
            return this;
        }

        /// <summary>
        /// Optional: add/override services before ServiceLocator gets locked.
        /// </summary>
        public PageNavBootstrap ConfigureServices(Action<ServiceLocator,IPlatformAdapter> configure)
        {
            _serviceConfig = configure;
            return this;
        }

        // --------------------------------------------------------------------
        // Build
        // --------------------------------------------------------------------
        private PageRegistry _registry;
        public NavigationContext Start()
        {
            // ------------------------------------------------------------
            // 1) Page registration
            // ------------------------------------------------------------
            // 1) Registry (instance-scoped)
            var registry = _registry ?? new PageRegistry();

            if (_pagesAssembly != null)
                registry.RegisterFromAssembly(_pagesAssembly);

            _pageConfig?.Invoke(new PageRegistryConfigurator(registry));

            // ------------------------------------------------------------
            // 2) Validate platform
            // ------------------------------------------------------------
            if (_platform == null)
                throw new InvalidOperationException(
                    "No platform adapter registered. Call Use<TPlatform>(nativeHost).");

            if (!_platform.CanHandle(_nativeHost))
                throw new InvalidOperationException(
                    $"Platform adapter '{_platform.GetType().Name}' cannot handle host of type '{_nativeHost?.GetType().FullName}'.");

            // ------------------------------------------------------------
            // 3) Service locator (OPEN phase)
            // ------------------------------------------------------------
            var services = new ServiceLocator();

            // ------------------------------------------------------------
            // 4) Create IPageHost from native host
            // ------------------------------------------------------------
            var host = _platform.CreateHost(_nativeHost)
                ?? throw new InvalidOperationException(
                    "Platform adapter returned null IPageHost.");

            services.Register(typeof(IPageHost), host);

            // ------------------------------------------------------------
            // 5) Register platform services (from native host)
            // ------------------------------------------------------------
            services.Register(typeof(IEventDispatcherAdapter),
                _platform.CreateEventDispatcher(_nativeHost));

            services.Register(typeof(IInteractionBlocker),
                _platform.CreateInteractionBlocker(_nativeHost));

            var observer = _platform.CreateInteractionObserverAdapter(_nativeHost);
            if (observer != null)
                services.Register(typeof(IInteractionObserverService), observer);

            var subscriber = _platform.CreateEventSubscriber(_nativeHost);
            if (subscriber != null)
                services.Register(typeof(IEventSubscriptionAdapter), subscriber);

            var overlay = _platform.CreateOverlayService(_nativeHost);
            if (overlay != null)
                services.Register(typeof(IPageOverlay), overlay);

            services.Register(typeof(ITimerAdapter),
                _platform.CreateTimerAdapter());

            // ------------------------------------------------------------
            // 6) Runtime services
            // ------------------------------------------------------------
            var pageFactory = new PageFactory();
            services.Register(typeof(PageFactory), pageFactory);

            

            // ------------------------------------------------------------     
            // 8) Allow app-level service extensions
            // ------------------------------------------------------------
            _serviceConfig?.Invoke(services, _platform);

            // ------------------------------------------------------------
            // 9) Create NavigationContext
            // ------------------------------------------------------------
            var context = new NavigationContext(
                host: host,
                services: services,
                registry: registry,
                diagnosticsContext: _diagnostics,
                platform: _platform
            );
            // ------------------------------------------------------------
            // 7) Diagnostics bridge (optional)
            // ------------------------------------------------------------

            if (_diagnostics != null)
            {
                registry.Info += msg => context.Diagnostics.EmitInfo(msg);
                registry.Warn += msg => context.Diagnostics.EmitWarn(msg);


                pageFactory.Warn += msg => context.Diagnostics.EmitWarn(msg);
            }
            services.Register(context);

            // ------------------------------------------------------------
            // 10) Lock services
            // ------------------------------------------------------------
            services.Lock();
            return context;



        }


        

       
    }
}
