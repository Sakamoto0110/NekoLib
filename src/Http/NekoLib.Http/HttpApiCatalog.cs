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

        /// <summary>
        /// Gets the immutable endpoint metadata collection in registration order.
        /// The non-generic elements are for introspection and cannot be sent directly.
        /// </summary>
        public IReadOnlyCollection<HttpEndpoint> Endpoints { get; }

        /// <summary>Builds an immutable catalog through a single-use registration callback.</summary>
        /// <param name="configure">Callback that registers all endpoint instances.</param>
        /// <returns>An immutable, instance-scoped catalog.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <c>null</c>.</exception>
        public static HttpApiCatalog Create(Action<HttpApiCatalogBuilder> configure)
        {
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            var builder = new HttpApiCatalogBuilder();
            configure(builder);
            return builder.Build();
        }

        /// <summary>Looks up endpoint metadata by its case-insensitive registered name.</summary>
        /// <param name="name">Non-blank endpoint name.</param>
        /// <returns>The exact endpoint instance registered under <paramref name="name"/>.</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
        /// <exception cref="KeyNotFoundException">No endpoint is registered under that name.</exception>
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

    /// <summary>
    /// Single-use endpoint registrar supplied by <see cref="HttpApiCatalog.Create"/>.
    /// Names are unique under ordinal case-insensitive comparison.
    /// </summary>
    public sealed class HttpApiCatalogBuilder
    {
        private readonly Dictionary<string, HttpEndpoint> _endpoints
            = new Dictionary<string, HttpEndpoint>(StringComparer.OrdinalIgnoreCase);
        private bool _built;

        internal HttpApiCatalogBuilder()
        {
        }

        /// <summary>Registers one endpoint instance and returns this builder for chaining.</summary>
        /// <param name="endpoint">Immutable endpoint metadata to register.</param>
        /// <returns>This builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="endpoint"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">The builder was already built or the endpoint name is already registered.</exception>
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
