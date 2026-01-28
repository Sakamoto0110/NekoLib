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
        private readonly ILogger _logger;
        private bool _started;

        public NekoHost(
            List<INekoModule> modules,
            INekoServiceRegistry services,
            ILogger logger)
        {
            _modules = modules ?? throw new ArgumentNullException(nameof(modules));
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Start()
        {
            if (_started) return;
            _started = true;

            _logger.Info("Host starting...");

            for (int i = 0; i < _modules.Count; i++)
            {
                var module = _modules[i];
                try
                {
                    _logger.Info("Starting module: " + module.Name);
                    module.Start();
                }
                catch (Exception ex)
                {
                    _logger.Error("Module start failed: " + module.Name, ex);
                    throw;
                }
            }

            _logger.Info("Host started.");
        }

        public void Stop()
        {
            if (!_started) return;
            _started = false;

            _logger.Info("Host stopping...");

            for (int i = _modules.Count - 1; i >= 0; i--)
            {
                var module = _modules[i];
                try
                {
                    _logger.Info("Stopping module: " + module.Name);
                    module.Stop();
                }
                catch (Exception ex)
                {
                    _logger.Error("Module stop failed: " + module.Name, ex);
                }
            }

            _logger.Info("Host stopped.");
        }

        public void Dispose()
        {
            try { Stop(); }
            catch { }
        }
    }

}
