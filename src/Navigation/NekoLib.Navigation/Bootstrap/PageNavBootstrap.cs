
using NekoLib.Core.Diagnostics;
using NekoLib.Navigation.Contracts.Guards;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Contracts.Runtime;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Runtime.Core;
using NekoLib.Navigation.Runtime.Factories;
using NekoLib.Navigation.Runtime.Registry;
using NekoLib.Navigation.Runtime.Services;
using NekoLib.Navigation.Runtime.Session;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

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
        private PageRegistry _registry;
        private Action<PageMetadataBuilder>? _pageConfig;
        private Action<ServiceLocator, IPlatformAdapter> _serviceConfig;

        private IDiagnosticsContext _diagnostics;

        private int _idleTimeoutMs;

        private Type _idlePageType;

         
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
            return new PageNavBootstrap(nativeHost, adapter);
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
        /// Return to the idle page after <paramref name="milliseconds"/> ms without
        /// any user interaction; also signs the framework-owned session out so
        /// guards re-evaluate on the next attempt. The bootstrap wires the
        /// platform interaction observer and timer adapter automatically — no
        /// consumer code is required.
        /// <para>
        /// The idle page is resolved through the standard registry mechanism:
        /// the page tagged with <c>.AsIdle()</c> / <c>PageRole.Idle</c>, with a
        /// name fallback to a registered page named <c>IdlePage</c>.
        /// </para>
        /// </summary>
        public PageNavBootstrap UseIdleTimeout(int milliseconds)
        {
            if (milliseconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(milliseconds));
            _idleTimeoutMs = milliseconds;
            return this;
        }

        /// <summary>
        /// Declarative shortcut for marking <typeparamref name="TPage"/> as the idle
        /// page (equivalent to <c>ConfigurePages(cfg =&gt; cfg.Page&lt;TPage&gt;().AsIdle())</c>),
        /// surfaced at the top level of the fluent chain.
        /// <para>
        /// Throws if called more than once on the same bootstrap, or if a
        /// <c>.AsIdle()</c> call in <see cref="ConfigurePages(System.Action{PageBuilderConfigurator})"/>
        /// targets a different page — the idle page is exclusive, and split
        /// declarations make navigation behaviour non-obvious.
        /// </para>
        /// </summary>
        public PageNavBootstrap SetIdle<TPage>() where TPage : IPageView
        {
            if (_idlePageType != null)
                throw new InvalidOperationException(
                    $"PageNavBootstrap.SetIdle was already called with '{_idlePageType.FullName}'. " +
                    "Call SetIdle<TPage>() at most once.");
            _idlePageType = typeof(TPage);
            return this;
        }

        /// <summary>
        /// Auto-register pages by scanning an assembly for IPageView + [PageBehavior] metadata.
        /// </summary>
        public PageNavBootstrap RegisterPagesFromAssembly(Assembly asm)
        {
            _assemblies.Add(asm);
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
        // 
        /// <summary>
        /// Manual tweaks after attribute defaults. (Hybrid mode) 🔹 Lambda API
        /// </summary>
        public PageNavBootstrap ConfigurePages(Action<PageMetadataBuilder> configure)
        {
            _pageConfig += configure;
            return this;
        }
        // 
        /// <summary>
        /// Manual tweaks after attribute defaults. (Hybrid mode) 🔹 Fluent DSL API
        /// </summary>
        // 
        public PageNavBootstrap ConfigurePages(Action<PageBuilderConfigurator> configureDsl)
        {
            _pageConfig += builder =>
            {
                var dsl = new PageBuilderConfigurator(builder);
                configureDsl(dsl);
            };

            return this;
        }
        // --------------------------------------------------------------------
        // Build
        // --------------------------------------------------------------------
        private readonly List<Assembly> _assemblies = new();
         
        public NavigationContext Start()
        {
            // ------------------------------------------------------------
            // 1) Page registration
            // ------------------------------------------------------------
            // 1) Registry (instance-scoped)
             
            PageRegistry registry;
            bool hasCustomMask = _assemblies
    .SelectMany(a => a.GetTypes())
    .Any(t => typeof(IGlobalLoadingMask).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);
            if (_registry != null)
            {
                if (_assemblies.Count > 0 || _pageConfig != null || _idlePageType != null)
                    throw new InvalidOperationException(
                        "Cannot configure pages (RegisterPagesFromAssembly / ConfigurePages / SetIdle) " +
                        "when an external registry is supplied via UseRegistry.");

                registry = _registry;
            }
            else
            {
                registry = PageRegistry.Create(builder =>
                {
                    foreach (var asm in _assemblies)
                        builder.RegisterFromAssembly(asm);

                    // SetIdle<TPage>() lands here so the descriptor exists with
                    // Role=Idle before _pageConfig runs. ConfigurePages may still
                    // call .AsIdle() on the SAME page (redundant, allowed); a
                    // call targeting a DIFFERENT page is caught after Build.
                    if (_idlePageType != null)
                        builder.RegisterType(_idlePageType, d => d.Role = PageRole.Idle);

                    _pageConfig?.Invoke(builder);
                    var defaultMaskType = _platform.GetDefaultLoadingMaskType();
                    if (!hasCustomMask && defaultMaskType != null)
                    {
                        builder.RegisterType(defaultMaskType); // Clean, no reflection!
                    }
                });

                // Enforce: at most one page can be tagged Role=Idle. Multiple
                // declarations (e.g. SetIdle<X>() + ConfigurePages(cfg =>
                // cfg.Page<Y>().AsIdle())) leave the runtime to pick one
                // arbitrarily — better to fail loud at bootstrap time.
                var idlePages = registry.AllDescriptors()
                    .Where(d => d.Role == PageRole.Idle)
                    .ToList();
                if (idlePages.Count > 1)
                {
                    var names = string.Join(", ", idlePages.Select(d => d.PageType.FullName));
                    throw new InvalidOperationException(
                        "Multiple idle pages registered: " + names + ". " +
                        "Use SetIdle<TPage>() or .AsIdle() in ConfigurePages — not both, " +
                        "and never targeting different pages.");
                }
            }
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

           
             

            services.Register(typeof(ITimerAdapter),
                _platform.CreateTimerAdapter());

            // ------------------------------------------------------------
            // 6) Runtime services
            // ------------------------------------------------------------
            var pageFactory = new PageFactory();
            services.Register(typeof(PageFactory), pageFactory);

            // Replaces the legacy OverlayService with three ISP-friendly services.
            var viewHost = host as IViewHost;

            IInteractionBlocker modalBlocker = null;
            if (services.CanResolve(typeof(IInteractionBlocker)))
                modalBlocker = (IInteractionBlocker)services.Get(typeof(IInteractionBlocker));

            IEventDispatcherAdapter uiDispatcher = null;
            if (services.CanResolve(typeof(IEventDispatcherAdapter)))
                uiDispatcher = (IEventDispatcherAdapter)services.Get(typeof(IEventDispatcherAdapter));

            var toastService = new ToastService(viewHost, pageFactory, uiDispatcher);
            services.Register(typeof(IToastService), toastService);

            var dialogService = new DialogService(viewHost, pageFactory, modalBlocker);
            services.Register(typeof(IDialogService), dialogService);

            var promptService = new PromptService(viewHost, pageFactory, modalBlocker);
            services.Register(typeof(IPromptService), promptService);
            // --- SAFE FALLBACK INJECTION ---
           

           
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

        
            services.Register(context);

            // Make the framework-owned session resolvable by both its concrete
            // type and the IUserContext contract guards consume. Same instance —
            // mutating context.Session is immediately visible to any service or
            // guard that resolves it from the locator.
            services.Register(typeof(NavigationSession), context.Session);
            services.Register(typeof(IUserContext), context.Session);

            // ------------------------------------------------------------
            // 10) Lock services
            // ------------------------------------------------------------
            services.Lock();

            // ------------------------------------------------------------
            // 11) Optional idle timeout (UseIdleTimeout)
            //
            // Wires the platform interaction observer + timer adapter so any
            // period without UI input longer than _idleTimeoutMs returns to
            // the idle page and signs the built-in session out. The consumer
            // writes no timer/observer/session code.
            // ------------------------------------------------------------
            if (_idleTimeoutMs > 0 &&
                services.CanResolve(typeof(IInteractionObserverService)))
            {
                var idleObserver = (IInteractionObserverService)services.Get(typeof(IInteractionObserverService));
                var idleTimer = _platform.CreateTimerAdapter();
                idleTimer.IntervalMilliseconds = _idleTimeoutMs;

                idleObserver.InteractionDetected += () =>
                {
                    idleTimer.Stop();
                    idleTimer.Start();
                };

                idleTimer.Tick += async () =>
                {
                    idleTimer.Stop();
                    context.Session.SignOut();
                    try { await NavigationService.GoIdleAsync(); }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            "[PageNavBootstrap] Idle timeout navigation failed: " + ex);
                    }
                };

                // Start immediately so an unattended boot also lands on the idle page.
                idleTimer.Start();
            }

            // Auto-mount on the facade so consumers don't need a second
            // NavigationService.UseContext(ctx) step. Tests of the bootstrap
            // itself MUST call NavigationService.Shutdown() in teardown so the
            // next test starts clean (UseContext throws on double-mount).
            NavigationService.UseContext(context);

            return context;
        }


        

       
    }
}
