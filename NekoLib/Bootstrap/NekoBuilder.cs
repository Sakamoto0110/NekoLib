using NekoLib.Core;
using NekoLib.Diagnostics;
using System;
using System.Collections.Generic;

namespace NekoLib
{
    public sealed class NekoBuilder : INekoBuilder
    {
        private readonly List<INekoModule> _modules = new List<INekoModule>();
        private INekoConfiguration _configuration = NekoConfiguration.Empty;
        private INekoEnvironment _environment = DefaultEnvironment.Production;
        private IDiagnostics _diagnostics = null;

        public NekoBuilder() { }

        public INekoBuilder UseConfiguration(INekoConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            return this;
        }

        public INekoBuilder UseEnvironment(INekoEnvironment environment)
        {
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            return this;
        }

        public INekoBuilder UseDiagnostics(IDiagnostics diagnostics)
        {
            _diagnostics = diagnostics  ;
            return this;
        }

        public INekoBuilder AddModule(INekoModule module)
        {
            if(module == null) throw new ArgumentNullException(nameof(module));
            _modules.Add(module);
            return this;
        }

        public INekoHost Build()
        {
            var services = new NekoServiceRegistry();

            // Core
            services.RegisterInstance(_configuration);
            services.RegisterInstance(_environment);

             

            var ctx = new NekoModuleContext(services, _configuration, _environment, _diagnostics);

            // Configure all modules before start
            for(int i = 0; i < _modules.Count; i++)
            {
                _modules[i].Configure(ctx);
            }

            return new NekoHost(_modules, services, _diagnostics.Logger);
        }

    

        private sealed class NekoModuleContext : INekoModuleContext
        {
            public INekoServiceRegistry Services { get; }
            public INekoConfiguration Configuration { get; }
            public INekoEnvironment Environment { get; }
            public IDiagnostics Diagnostics { get; }

            public NekoModuleContext(INekoServiceRegistry services, INekoConfiguration configuration, INekoEnvironment environment, IDiagnostics diagnostics)
            {
                Services = services;
                Configuration = configuration;
                Environment = environment;
                Diagnostics = diagnostics;
            }
        }
    }
}
