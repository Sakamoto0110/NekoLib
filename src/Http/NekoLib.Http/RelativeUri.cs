using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NekoLib.Http
{
    /// <summary>
    /// A relative URI that cannot replace the scheme or authority configured on
    /// the consumer-owned <see cref="System.Net.Http.HttpClient"/>.
    /// </summary>
    public sealed class RelativeUri
    {
        internal RelativeUri(string value)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>Gets the escaped relative URI text; an empty value targets the client base address.</summary>
        public string Value { get; }

        /// <summary>Creates a relative URI from independently escaped path segments.</summary>
        /// <param name="pathSegments">Ordered path segments; an empty array creates an empty relative URI.</param>
        /// <returns>An immutable relative URI.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="pathSegments"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">A segment is blank.</exception>
        public static RelativeUri FromPathSegments(params string[] pathSegments)
            => RelativeUriBuilder.Create(pathSegments).Build();

        /// <summary>Returns <see cref="Value"/>.</summary>
        /// <returns>The escaped relative URI text.</returns>
        public override string ToString() => Value;
    }

    /// <summary>
    /// Builds relative request URIs by escaping each path segment and query value.
    /// Static route and parameter names still belong to the endpoint declaration.
    /// </summary>
    public sealed class RelativeUriBuilder
    {
        private readonly List<string> _segments = new List<string>();
        private readonly List<KeyValuePair<string, string>> _query
            = new List<KeyValuePair<string, string>>();

        private RelativeUriBuilder()
        {
        }

        /// <summary>Creates a builder and appends each supplied path segment in order.</summary>
        /// <param name="pathSegments">Initial path segments; an empty array is valid.</param>
        /// <returns>A mutable builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="pathSegments"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">A segment is blank.</exception>
        public static RelativeUriBuilder Create(params string[] pathSegments)
        {
            if (pathSegments == null)
                throw new ArgumentNullException(nameof(pathSegments));

            var builder = new RelativeUriBuilder();
            foreach (var segment in pathSegments)
                builder.AppendPathSegment(segment);

            return builder;
        }

        /// <summary>Escapes and appends one non-blank path segment.</summary>
        /// <param name="segment">Literal segment value; slashes are escaped rather than treated as separators.</param>
        /// <returns>This builder.</returns>
        /// <exception cref="ArgumentException"><paramref name="segment"/> is blank.</exception>
        public RelativeUriBuilder AppendPathSegment(string segment)
        {
            if (string.IsNullOrWhiteSpace(segment))
                throw new ArgumentException("Path segments cannot be empty.", nameof(segment));

            _segments.Add(Uri.EscapeDataString(segment));
            return this;
        }

        /// <summary>Adds one escaped query pair, or omits it when <paramref name="value"/> is <c>null</c>.</summary>
        /// <param name="name">Non-blank query name.</param>
        /// <param name="value">Optional query value.</param>
        /// <returns>This builder. Repeated names are retained in insertion order.</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
        public RelativeUriBuilder AddQuery(string name, string? value)
        {
            ValidateQueryName(name);
            if (value != null)
            {
                _query.Add(new KeyValuePair<string, string>(
                    Uri.EscapeDataString(name),
                    Uri.EscapeDataString(value)));
            }

            return this;
        }

        /// <summary>Adds an invariant-culture integer query value.</summary>
        /// <param name="name">Non-blank query name.</param>
        /// <param name="value">Integer value.</param>
        /// <returns>This builder.</returns>
        public RelativeUriBuilder AddQuery(string name, int value)
            => AddQuery(name, value.ToString(CultureInfo.InvariantCulture));

        /// <summary>Adds an invariant-culture long-integer query value.</summary>
        /// <param name="name">Non-blank query name.</param>
        /// <param name="value">Long-integer value.</param>
        /// <returns>This builder.</returns>
        public RelativeUriBuilder AddQuery(string name, long value)
            => AddQuery(name, value.ToString(CultureInfo.InvariantCulture));

        /// <summary>Adds a lowercase <c>true</c> or <c>false</c> query value.</summary>
        /// <param name="name">Non-blank query name.</param>
        /// <param name="value">Boolean value.</param>
        /// <returns>This builder.</returns>
        public RelativeUriBuilder AddQuery(string name, bool value)
            => AddQuery(name, value ? "true" : "false");

        /// <summary>Materializes the current path and query sequence.</summary>
        /// <returns>An immutable relative URI.</returns>
        public RelativeUri Build()
        {
            var path = string.Join("/", _segments);
            if (_query.Count == 0)
                return new RelativeUri(path);

            var query = string.Join(
                "&",
                _query.Select(pair => pair.Key + "=" + pair.Value));
            return new RelativeUri(path + "?" + query);
        }

        private static void ValidateQueryName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Query parameter names cannot be empty.", nameof(name));
        }
    }
}
