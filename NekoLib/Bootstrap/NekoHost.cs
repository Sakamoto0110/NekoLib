using NekoLib.Core;
using NekoLib.Diagnostics;
using System;
using System.Collections.Generic;

namespace NekoLib
{
    public sealed class NekoHost : INekoHost
    {
        private readonly List<INekoModule> _modules;
        private readonly INekoServiceRegistry _services;
        private readonly IDiagnosticsRuntime _diagnostics;
        private bool _started;

        public NekoHost(List<INekoModule> modules, INekoServiceRegistry services, IDiagnosticsRuntime diagnostics)
        {
            _modules = modules ?? throw new ArgumentNullException(nameof(modules));
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        }

        public void Start()
        {
            if(_started) return;
            _started = true;

            var logger = _services.Resolve<ILogger>();
            logger.Info("Host starting...");

            for(int i = 0; i < _modules.Count; i++)
            {
                try
                {
                    logger.Info("Starting module: " + _modules[i].Name);
                    _modules[i].Start();
                }
                catch(Exception ex)
                {
                    logger.Error("Module start failed: " + _modules[i].Name, ex);
                    throw;
                }
            }

            logger.Info("Host started.");
        }

        public void Stop()
        {
            if(!_started) return;
            _started = false;

            var logger = _services.Resolve<ILogger>();
            logger.Info("Host stopping...");

            for(int i = _modules.Count - 1; i >= 0; i--)
            {
                try
                {
                    logger.Info("Stopping module: " + _modules[i].Name);
                    _modules[i].Stop();
                }
                catch(Exception ex)
                {
                    logger.Error("Module stop failed: " + _modules[i].Name, ex);
                }
            }

            logger.Info("Host stopped.");
        }

        public void Dispose()
        {
            try { Stop(); }
            catch { }

            try { _diagnostics.Dispose(); }
            catch { }
        }
    }
}
