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

        public string Value { get; }

        public static RelativeUri FromPathSegments(params string[] pathSegments)
            => RelativeUriBuilder.Create(pathSegments).Build();

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

        public static RelativeUriBuilder Create(params string[] pathSegments)
        {
            if (pathSegments == null)
                throw new ArgumentNullException(nameof(pathSegments));

            var builder = new RelativeUriBuilder();
            foreach (var segment in pathSegments)
                builder.AppendPathSegment(segment);

            return builder;
        }

        public RelativeUriBuilder AppendPathSegment(string segment)
        {
            if (string.IsNullOrWhiteSpace(segment))
                throw new ArgumentException("Path segments cannot be empty.", nameof(segment));

            _segments.Add(Uri.EscapeDataString(segment));
            return this;
        }

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

        public RelativeUriBuilder AddQuery(string name, int value)
            => AddQuery(name, value.ToString(CultureInfo.InvariantCulture));

        public RelativeUriBuilder AddQuery(string name, long value)
            => AddQuery(name, value.ToString(CultureInfo.InvariantCulture));

        public RelativeUriBuilder AddQuery(string name, bool value)
            => AddQuery(name, value ? "true" : "false");

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
