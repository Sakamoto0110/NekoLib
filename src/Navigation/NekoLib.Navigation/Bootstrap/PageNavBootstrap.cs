
using NekoLib.Core.Inspection;
using NekoLib.Core.Logging;
using NekoLib.Core.Telemetry;
using NekoLib.Navigation.Contracts.Guards;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Contracts.Platform;
using NekoLib.Navigation.Contracts.Runtime;
using NekoLib.Navigation.Diagnostics;
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
    /// Fluent bootstrap for creating a navigation context, registering pages,
    /// wiring platform services, and mounting the static <c>NavigationService</c>
    /// facade.
    /// </summary>
    public sealed class PageNavBootstrap
    {
        private readonly object _nativeHost;
        private readonly IPlatformAdapter _platform;
        private PageRegistry _registry;
        private Action<PageMetadataBuilder>? _pageConfig;
        private Action<ServiceLocator, IPlatformAdapter> _serviceConfig;

        private ILogger? _logger;
        private ITelemetry? _telemetry;
        private IInspectionRecorder? _inspection;
        private bool _useGlobalInspection;

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
        /// Start a bootstrap chain with a concrete platform adapter such as
        /// <c>WinFormsPlatformAdapter</c> or <c>WpfPlatformAdapter</c>.
        /// </summary>
        public static PageNavBootstrap Use<TPlatform>(object nativeHost) where TPlatform : IPlatformAdapter, new()
        {
            var adapter = new TPlatform();
            return new PageNavBootstrap(nativeHost, adapter);
        }
      

        /// <summary>
        /// Start a bootstrap chain when the platform adapter is supplied through
        /// a previously registered platform mechanism.
        /// </summary>
        public static PageNavBootstrap UseRegistered(object nativeHost)
            => new PageNavBootstrap(nativeHost);

        // --------------------------------------------------------------------
        // Configuration
        // --------------------------------------------------------------------

        /// <summary>
        /// Use a prebuilt registry instead of assembly scanning/manual page
        /// configuration. Page configuration methods cannot be combined with this.
        /// </summary>
        public PageNavBootstrap UseRegistry(PageRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            return this;
        }

        /// <summary>
        /// Project navigation outcomes into the independent logging pipeline.
        /// </summary>
        public PageNavBootstrap UseLogging(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            return this;
        }

        /// <summary>
        /// Capture correlated page-switch operations in the independent telemetry
        /// pipeline. Authentication remains application-owned and is reported
        /// through <c>NavigationTimingContext</c> when supplied in the arguments.
        /// </summary>
        public PageNavBootstrap UseTelemetry(ITelemetry telemetry)
        {
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            return this;
        }

        /// <summary>
        /// Forward navigation events into an opt-in observability sink (debug builds).
        /// A disabled sink (e.g. <see cref="NullInspection"/>) is a no-op. Events stay
        /// available through the context event hub regardless.
        /// </summary>
        public PageNavBootstrap UseInspection(IInspectionRecorder inspection)
        {
            _inspection = inspection ?? throw new ArgumentNullException(nameof(inspection));
            _useGlobalInspection = false;
            return this;
        }

        /// <summary>
        /// Resolve the opt-in process-wide observability hub from
        /// <see cref="InspectionProvider.Current"/> when <see cref="Start"/> runs.
        /// If the current hub is the no-op implementation, no observer is attached.
        /// </summary>
        public PageNavBootstrap UseInspection()
        {
            _inspection = null;
            _useGlobalInspection = true;
            return this;
        }

        /// <summary>
        /// Return to the idle page after <paramref name="milliseconds"/> ms without
        /// any user interaction; also signs the framework-owned session out so
        /// guards re-evaluate on the next attempt. The bootstrap wires the
        /// platform interaction observer and timer adapter automatically; no
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
        /// targets a different page. The idle page is exclusive, and split
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
        /// Auto-register pages by scanning an assembly for <c>IPageView</c>
        /// implementations and page metadata attributes.
        /// </summary>
        public PageNavBootstrap RegisterPagesFromAssembly(Assembly asm)
        {
            _assemblies.Add(asm);
            return this;
        }
 

        /// <summary>
        /// Add application-specific services before the runtime service locator is locked.
        /// </summary>
        public PageNavBootstrap ConfigureServices(Action<ServiceLocator,IPlatformAdapter> configure)
        {
            _serviceConfig = configure;
            return this;
        }
        // 
        /// <summary>
        /// Apply manual page metadata overrides after attribute defaults.
        /// </summary>
        public PageNavBootstrap ConfigurePages(Action<PageMetadataBuilder> configure)
        {
            _pageConfig += configure;
            return this;
        }
        // 
        /// <summary>
        /// Apply manual page metadata overrides through the fluent page DSL.
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
         
        /// <summary>
        /// Build the registry and runtime services, lock the service locator, wire
        /// optional idle timeout, mount <c>NavigationService</c>, and return the
        /// created context.
        /// </summary>
        public NavigationContext Start()
        {
            // ------------------------------------------------------------
            // 1) Page registration
            // ------------------------------------------------------------
            // 1) Registry (instance-scoped)
             
            PageRegistry registry;
            // NAV-008(b): the tolerant scan, not raw GetTypes(). A single unloadable
            // type in a scanned assembly used to abort Start() here, on its first
            // line, before any of the tolerance the rest of bootstrap advertises.
            bool hasCustomMask = _assemblies
                .SelectMany(AssemblyTypeScanner.GetLoadableTypes)
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
                // arbitrarily; better to fail loud at bootstrap time.
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
            // 1b) Resolve the idle page and validate [PageTimeout] placement
            //
            // The idle timeout (seconds) is the *inactivity* timeout, so it is
            // declared on the idle page itself. Any other page carrying a timeout
            // is a configuration mistake — fail loud instead of silently ignoring
            // it (the timeout would never fire, since only the idle descriptor's
            // value is read below).
            // ------------------------------------------------------------
            var allDescriptors = registry.AllDescriptors().ToList();
            var idleDescriptor = IdlePageRules.Resolve(allDescriptors);

            foreach (var d in allDescriptors)
            {
                if (d.IdleTimeoutSeconds.HasValue && !ReferenceEquals(d, idleDescriptor))
                    throw new InvalidOperationException(
                        $"Page '{d.PageType.FullName}' declares an idle timeout " +
                        $"({d.IdleTimeoutSeconds}s) but is not the idle page. Declare the " +
                        "timeout on the idle page (PageRole.Idle, an 'idle' tag, or a name " +
                        "containing 'Idle'), or use UseIdleTimeout(ms) for the global timeout.");
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

            var platformBlocker = _platform.CreateInteractionBlocker(_nativeHost);
            if (platformBlocker != null)
            {
                services.Register(
                    typeof(IInteractionBlocker),
                    new ReferenceCountedInteractionBlocker(platformBlocker));
            }

            var observer = _platform.CreateInteractionObserverAdapter(_nativeHost);
            if (observer != null)
                services.Register(typeof(IInteractionObserverService), observer);

            ITimerAdapter timerAdapter;
            try
            {
                timerAdapter = _platform.CreateTimerAdapter();
            }
            catch
            {
                if (observer is IDisposable disposableObserver)
                    disposableObserver.Dispose();
                throw;
            }

            var bootstrapLifetime = new NavigationBootstrapLifetime(observer, timerAdapter);

            try
            {
                var subscriber = _platform.CreateEventSubscriber(_nativeHost);
                if (subscriber != null)
                    services.Register(typeof(IEventSubscriptionAdapter), subscriber);

                var focusObserver = _platform.CreateFocusObserver(_nativeHost);
                if (focusObserver != null)
                    services.Register(typeof(IFocusObserverAdapter), focusObserver);

                services.Register(typeof(ITimerAdapter), timerAdapter);

            // ------------------------------------------------------------
            // 6) Runtime services
            // ------------------------------------------------------------
            var pageFactory = new PageFactory();

            // NAV-008(d): PageFactory.Warn had no subscriber anywhere, so every page
            // and every surface was being built through the migration-only
            // default-constructor fallback in complete silence — nothing here ever
            // registers a factory. Give the event a real consumer instead of touching
            // the frozen PageFactory. Capture the logger in a local so the handler
            // does not root this builder for the lifetime of the factory.
            var warnLogger = _logger;
            pageFactory.Warn += message =>
            {
                if (warnLogger != null)
                    warnLogger.Warn(message, category: "Navigation");
                else
                    System.Diagnostics.Debug.WriteLine(message);
            };

            services.Register(typeof(PageFactory), pageFactory);

            // Replaces the legacy OverlayService with three ISP-friendly services.
            var viewHost = host as IViewHost
                ?? throw new InvalidOperationException(
                    "The platform page host must also implement IViewHost.");

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

            var popoverService = new PopoverService(viewHost, pageFactory, focusObserver);
            services.Register(typeof(IPopoverService), popoverService);

            if (modalBlocker is IPageAwareInteractionBlocker pageAwareBlocker)
            {
                ((INavigationInteractionBlockerAware)toastService)
                    .AttachInteractionBlocker(pageAwareBlocker);
                ((INavigationInteractionBlockerAware)dialogService)
                    .AttachInteractionBlocker(pageAwareBlocker);
                ((INavigationInteractionBlockerAware)promptService)
                    .AttachInteractionBlocker(pageAwareBlocker);
                ((INavigationInteractionBlockerAware)popoverService)
                    .AttachInteractionBlocker(pageAwareBlocker);
            }
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
                platform: _platform,
                logger: _logger
            );
            // ------------------------------------------------------------
            // 7) Diagnostics bridge (optional)
            // ------------------------------------------------------------

            // Opt-in observability: the observer projects the runtime's internal
            // scalar trace without retaining pages or payloads. No-op when disabled.
            var resolvedInspection = _useGlobalInspection
                ? InspectionProvider.Current
                : _inspection;

            services.Register(context);

            // Make the framework-owned session resolvable by both its concrete
            // type and the IUserContext contract guards consume. Same instance:
            // mutating context.Session is immediately visible to any service or
            // guard that resolves it from the locator.
            services.Register(typeof(NavigationSession), context.Session);
            services.Register(typeof(IUserContext), context.Session);

            // ------------------------------------------------------------
            // 10) Lock services
            // ------------------------------------------------------------
            services.Lock();

            // ------------------------------------------------------------
            // 11) Optional idle timeout
            //
            // Wires the platform interaction observer + timer adapter so any
            // period without UI input longer than the effective interval returns
            // to the idle page and signs the built-in session out. The consumer
            // writes no timer/observer/session code.
            //
            // Effective interval (idle page wins, global is fallback):
            //   idle page [PageTimeout]/.IdleTimeout(seconds)  ->  UseIdleTimeout(ms).
            // A timeout declared on the idle page also enables the timer on its
            // own, so UseIdleTimeout(ms) is not required when the idle page sets one.
            // ------------------------------------------------------------
            int effectiveIdleMs = _idleTimeoutMs;
            if (idleDescriptor?.IdleTimeoutSeconds is int idleSeconds && idleSeconds > 0)
                effectiveIdleMs = idleSeconds * 1000;

            // Auto-mount on the facade so consumers don't need a second
            // NavigationService.UseContext(ctx) step. Tests of the bootstrap
            // itself MUST call NavigationService.Shutdown() in teardown so the
            // next test starts clean (UseContext throws on double-mount).
            var inspectionObserver = resolvedInspection != null && resolvedInspection.IsEnabled
                ? InspectionNavigationObserver.Attach(context, resolvedInspection)
                : NekoLib.Core.Disposable.Empty;
            var telemetryObserver = _telemetry != null
                ? NavigationTelemetryObserver.Attach(context.Events, _telemetry)
                : NekoLib.Core.Disposable.Empty;
            var observationLifetime = NavigationObservationLifetime.Combine(
                inspectionObserver,
                telemetryObserver);

            try
            {
                // Attach diagnostics before idle configuration so the initial
                // Configured/Unavailable state is visible in the debug snapshot.
                if (effectiveIdleMs > 0)
                    bootstrapLifetime.ConfigureIdle(effectiveIdleMs, context);

                NavigationService.UseContext(
                    context,
                    observationLifetime,
                    bootstrapLifetime);
            }
            catch
            {
                try { observationLifetime.Dispose(); }
                catch { }
                throw;
            }

            return context;
            }
            catch
            {
                // Also covers failures before ownership is transferred to the
                // facade. Dispose is idempotent, so a rejected double-mount can
                // safely clean up in UseContext and again here.
                bootstrapLifetime.Dispose();
                throw;
            }
        }


        

       
    }
}
