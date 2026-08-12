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

        public JsonHttpBodySerializer()
            : this(new JsonSerializerSettings())
        {
        }

        public JsonHttpBodySerializer(JsonSerializerSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public string MediaType => "application/json";

        public string Serialize(object value, Type declaredType)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (declaredType == null)
                throw new ArgumentNullException(nameof(declaredType));

            return JsonConvert.SerializeObject(value, declaredType, _settings);
        }

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
