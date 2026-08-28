using Newtonsoft.Json;
using System;

namespace NekoLib.Http.Serialization
{
    /// <summary>
    /// JSON body serializer with consistent behavior on both supported targets.
    /// Consumers may supply a different <see cref="IHttpBodySerializer"/>.
    /// </summary>
    public sealed class JsonHttpBodySerializer : IHttpBodySerializer
    {
        private readonly JsonSerializerSettings _settings;

        /// <summary>Creates a serializer with new default Newtonsoft.Json settings.</summary>
        public JsonHttpBodySerializer()
            : this(new JsonSerializerSettings())
        {
        }

        /// <summary>Creates a serializer that retains the supplied Newtonsoft.Json settings instance.</summary>
        /// <param name="settings">Settings used for every serialization and deserialization call.</param>
        /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <c>null</c>.</exception>
        public JsonHttpBodySerializer(JsonSerializerSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <inheritdoc />
        public string MediaType => "application/json";

        /// <inheritdoc />
        public string Serialize(object value, Type declaredType)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (declaredType == null)
                throw new ArgumentNullException(nameof(declaredType));

            return JsonConvert.SerializeObject(value, declaredType, _settings);
        }

        /// <inheritdoc />
        public object Deserialize(string content, Type declaredType)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));
            if (declaredType == null)
                throw new ArgumentNullException(nameof(declaredType));

            var value = JsonConvert.DeserializeObject(content, declaredType, _settings);
            if (value == null)
            {
                throw new JsonSerializationException(
                    $"The response body produced no '{declaredType.FullName}' value.");
            }

            return value;
        }
    }
}
