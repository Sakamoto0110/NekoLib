using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NekoLib.Http
{
    /// <summary>
    /// Immutable, instance-scoped collection of endpoints available to one API
    /// client. It stores metadata only and owns no transport or credentials.
    /// </summary>
    public sealed class HttpApiCatalog
    {
        private readonly IReadOnlyDictionary<string, HttpEndpoint> _byName;
        private readonly HashSet<HttpEndpoint> _registeredEndpoints;

        private HttpApiCatalog(IReadOnlyDictionary<string, HttpEndpoint> byName)
        {
            _byName = byName;
            _registeredEndpoints = new HashSet<HttpEndpoint>(byName.Values);
            Endpoints = new List<HttpEndpoint>(byName.Values).AsReadOnly();
        }

        public IReadOnlyCollection<HttpEndpoint> Endpoints { get; }

        public static HttpApiCatalog Create(Action<HttpApiCatalogBuilder> configure)
        {
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            var builder = new HttpApiCatalogBuilder();
            configure(builder);
            return builder.Build();
        }

        public HttpEndpoint Get(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Endpoint names cannot be empty.", nameof(name));

            if (!_byName.TryGetValue(name, out var endpoint))
                throw new KeyNotFoundException($"Endpoint '{name}' is not registered.");

            return endpoint;
        }

        internal bool Contains(HttpEndpoint endpoint)
            => _registeredEndpoints.Contains(endpoint);

        internal bool ContainsName(string name)
            => !string.IsNullOrWhiteSpace(name) && _byName.ContainsKey(name);

        internal static HttpApiCatalog From(Dictionary<string, HttpEndpoint> endpoints)
            => new HttpApiCatalog(
                new ReadOnlyDictionary<string, HttpEndpoint>(endpoints));
    }

    public sealed class HttpApiCatalogBuilder
    {
        private readonly Dictionary<string, HttpEndpoint> _endpoints
            = new Dictionary<string, HttpEndpoint>(StringComparer.OrdinalIgnoreCase);
        private bool _built;

        internal HttpApiCatalogBuilder()
        {
        }

        public HttpApiCatalogBuilder Register(HttpEndpoint endpoint)
        {
            if (_built)
                throw new InvalidOperationException("The HTTP API catalog has already been built.");
            if (endpoint == null)
                throw new ArgumentNullException(nameof(endpoint));
            if (_endpoints.ContainsKey(endpoint.Name))
            {
                throw new InvalidOperationException(
                    $"Endpoint name '{endpoint.Name}' is already registered.");
            }

            _endpoints.Add(endpoint.Name, endpoint);
            return this;
        }

        internal HttpApiCatalog Build()
        {
            if (_built)
                throw new InvalidOperationException("The HTTP API catalog has already been built.");

            _built = true;
            return HttpApiCatalog.From(
                new Dictionary<string, HttpEndpoint>(
                    _endpoints,
                    StringComparer.OrdinalIgnoreCase));
        }
    }
}
