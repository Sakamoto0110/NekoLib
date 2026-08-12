using System;

namespace NekoLib.Http.Serialization
{
    /// <summary>
    /// Serializes request bodies and deserializes successful response bodies.
    /// Authentication, transport policy and error interpretation remain outside
    /// this contract.
    /// </summary>
    public interface IHttpBodySerializer
    {
        string MediaType { get; }

        string Serialize(object value, Type declaredType);

        object Deserialize(string content, Type declaredType);
    }
}
