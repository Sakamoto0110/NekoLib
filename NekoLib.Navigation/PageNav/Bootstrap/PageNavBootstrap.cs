using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Diagnostics;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Infrastructure;
using NekoLib.Navigation.Runtime.Core;
using NekoLib.Navigation.Runtime.Factories;
using NekoLib.Navigation.Runtime.Registry;
using NekoLib.Navigation.Runtime.Services;
using System;
using System.Reflection;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Contracts.Runtime;

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

        private NekoLib.Diagnostics.IDiagnostics _diagnostics;

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

        public static NavigationContext Initialize<TPlataform>(IPageHost host) where TPlataform : IPlatformAdapter, new() => Initialize(new TPlataform(), host); 
        

        public static NavigationContext Initialize(
        IPlatformAdapter platform,
        IPageHost host)
        {
            if (platform == null)
                throw new ArgumentNullException(nameof(platform));

            if (host == null)
                throw new ArgumentNullException(nameof(host));

            // ------------------------------------------------------------
            // Platform (HARD REQUIREMENT)
            // ------------------------------------------------------------
            PlatformRegistry.Register(platform);

            // ------------------------------------------------------------
            // Services
            // ------------------------------------------------------------
            var services = new ServiceLocator();

            // Platform-provided services
            services.Register(typeof(IEventDispatcherAdapter), platform.CreateEventDispatcher(host));
            services.Register(typeof(IInteractionBlocker), platform.CreateInteractionBlocker(host));
            services.Register(typeof(ITimerAdapter), platform.CreateTimerAdapter());

            // Runtime services
            services.Register(typeof(PageFactory), new PageFactory());

            // ------------------------------------------------------------
            // Lock services (NO MORE REGISTRATION)
            // ------------------------------------------------------------
            services.Lock();

            // ------------------------------------------------------------
            // Context
            // ------------------------------------------------------------
            return new NavigationContext(
                host: host,
                 services: services
            );
        }

        /// <summary>
        /// Use a specific platform adapter (WinForms/WPF/etc). You can call this multiple
        /// times across app startup, but resolution locks at first context creation.
        /// </summary>
        public static PageNavBootstrap Use<TPlataform>(object nativeHost) where TPlataform : IPlatformAdapter, new()
        {
            var adapter = new TPlataform();
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


        
        public PageNavBootstrap UseDiagnostics(IDiagnostics diagnostics)
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

        public NavigationContext Start()
        {
            // ------------------------------------------------------------
            // 1) Page registration
            // ------------------------------------------------------------
            if (_pagesAssembly != null)
                PageRegistry.RegisterFromAssembly(_pagesAssembly);

            _pageConfig?.Invoke(new PageRegistryConfigurator());

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
            // 7) Diagnostics bridge (optional)
            // ------------------------------------------------------------
            if (_diagnostics != null)
                NavigationDiagnostics.Context = _diagnostics;

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
                diagnostics: _diagnostics
            );

            services.Register(context);

            // ------------------------------------------------------------
            // 10) Lock services
            // ------------------------------------------------------------
            services.Lock();

            return context;
        }


        //public NavigationContext Start()
        //{
        //    // 1) Register pages by attribute scan (optional but typical)
        //    if(_pagesAssembly != null)
        //        PageRegistry.RegisterFromAssembly(_pagesAssembly);

        //    // 2) Apply manual tweaks (hybrid)
        //    if(_pageConfig != null)
        //        _pageConfig(new PageRegistryConfigurator());

        //    // 3) Build navigation context via builder (composition root)
        //    var ctx = new NavigationContextBuilder()
        //        .UseHost((IPageHost)_nativeHost)
        //        .UseTimeout(_timeoutSeconds)
        //        .UseDiagnostics(_diagnostics)

        //        .Build();


        //    // 4) Allow app to add extra services before lock
        //    if(_serviceConfig != null)
        //        _serviceConfig(ctx.Services,_platform);

        //    // 5) Register context itself (handy for overlays / dialogs / DM extensions)
        //    ctx.Services.Register(ctx);

        //    // 6) Lock locator
        //    ctx.Services.Lock();

        //    return ctx;
        //}

        // Convenience to keep legacy static facade alive if you still want it:
        public NavigationContext StartAsDefault()
        {
            var ctx = Start();
            // If you want to keep a global pointer for legacy, do it explicitly:
            // PageNav.Core.Services.Svc.Current = ctx;
            return ctx;
        }
    }
}
