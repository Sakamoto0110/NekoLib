using NekoLib.Diagnostics;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Diagnostics;
using NekoLib.Navigation.Runtime.Services;
using System;

namespace NekoLib.Navigation.Runtime.Core
{
    public sealed class NavigationContextBuilder
    {
        private IPageHost _host;
        private int _timeoutSeconds = 10;
        private IDiagnosticContext _diagnostics;

        private readonly ServiceLocator _services = new ServiceLocator();

        public NavigationContextBuilder UseHost(IPageHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            return this;
        }

        public NavigationContextBuilder UseService<T>(T instance) where T : class
        {
            _services.Register(instance);
            return this;
        }

        public NavigationContextBuilder UseDiagnostics(IDiagnosticContext diagnostics)
        {
            _diagnostics = diagnostics;
            // Also wire the static hook used by PageRegistry and legacy static facade.
            NavigationDiagnostics.Context = diagnostics;
            return this;
        }

        public NavigationContextBuilder UseTimeout(int seconds)
        {
            _timeoutSeconds = seconds;
            return this;
        }

        public NavigationContext Build()
        {
            if(_host == null)
                throw new InvalidOperationException("IPageHost is required.");

            // Core-owned services
            _services.Register(_host);
            var pageFactory = new Factories.PageFactory();
            _services.Register(pageFactory);

            var context = new NavigationContext(
                _host,
                _services,
                timeout: TimeSpan.FromSeconds(_timeoutSeconds),
                diagnostics: _diagnostics
            );

            _services.Register(context);
            return context;
        }
    }
}
