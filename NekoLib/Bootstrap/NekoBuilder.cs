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

        private readonly DiagnosticsRuntime _diagnosticsRuntime = new DiagnosticsRuntime();

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

        public NekoBuilder UseDiagnostics(Action<IDiagnosticsBuilder> configure)
        {
            if(configure == null) return this;
            configure(new DiagnosticsBuilder(_diagnosticsRuntime));
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

            // Diagnostics runtime + default context
            services.RegisterInstance<IDiagnosticsRuntime>(_diagnosticsRuntime);
            services.RegisterInstance<IDiagnosticContext>(_diagnosticsRuntime.CreateContext("NekoLib"));
            services.RegisterInstance<ILogger>(_diagnosticsRuntime.CreateLogger("NekoLib"));

            var ctx = new NekoModuleContext(services, _configuration, _environment);

            // Configure all modules before start
            for(int i = 0; i < _modules.Count; i++)
            {
                _modules[i].Configure(ctx);
            }

            return new NekoHost(_modules, services, _diagnosticsRuntime);
        }

        private sealed class DiagnosticsBuilder : IDiagnosticsBuilder
        {
            private readonly DiagnosticsRuntime _rt;

            public DiagnosticsBuilder(DiagnosticsRuntime rt) { _rt = rt; }

            public IDiagnosticsBuilder AddLogSink(ILogSink sink)
            {
                _rt.AddLogSink(sink);
                return this;
            }

            public IDiagnosticsBuilder AddTelemetrySink(ITelemetrySink sink)
            {
                _rt.AddTelemetrySink(sink);
                return this;
            }
        }

        private sealed class NekoModuleContext : INekoModuleContext
        {
            public INekoServiceRegistry Services { get; }
            public INekoConfiguration Configuration { get; }
            public INekoEnvironment Environment { get; }

            public NekoModuleContext(INekoServiceRegistry services, INekoConfiguration configuration, INekoEnvironment environment)
            {
                Services = services;
                Configuration = configuration;
                Environment = environment;
            }
        }
    }
}
