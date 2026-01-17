using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Diagnostics;
using NekoLib.Navigation.Contracts.Plataform;
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

        private NekoLib.Diagnostics.IDiagnosticContext _diagnostics;

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
                services: services,TimeSpan.FromSeconds(10)
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

        public PageNavBootstrap Timeout(int seconds)
        {
            if(seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            _timeoutSeconds = seconds;
            return this;
        }

        
        public PageNavBootstrap UseDiagnostics(IDiagnosticContext diagnostics)
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
            // 1) Register pages by attribute scan (optional but typical)
            if(_pagesAssembly != null)
                PageRegistry.RegisterFromAssembly(_pagesAssembly);

            // 2) Apply manual tweaks (hybrid)
            if(_pageConfig != null)
                _pageConfig(new PageRegistryConfigurator());

            // 3) Build navigation context via builder (composition root)
            var ctx = new NavigationContextBuilder()
                .UseHost((IPageHost)_nativeHost)
                .UseTimeout(_timeoutSeconds)
                .UseDiagnostics(_diagnostics)
                
                .Build();


            // 4) Allow app to add extra services before lock
            if(_serviceConfig != null)
                _serviceConfig(ctx.Services,_platform);

            // 5) Register context itself (handy for overlays / dialogs / DM extensions)
            ctx.Services.Register(ctx);

            // 6) Lock locator
            ctx.Services.Lock();

            return ctx;
        }

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
