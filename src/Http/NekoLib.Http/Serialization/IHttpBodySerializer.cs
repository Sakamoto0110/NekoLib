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
        /// <summary>
        /// Gets the media type placed on serialized request content, without a
        /// charset parameter.
        /// </summary>
        string MediaType { get; }

        /// <summary>Serializes one non-null request body.</summary>
        /// <param name="value">The request value.</param>
        /// <param name="declaredType">
        /// The runtime type selected for request serialization.
        /// </param>
        /// <returns>The textual request body.</returns>
        string Serialize(object value, Type declaredType);

        /// <summary>Deserializes one successful, bounded response body.</summary>
        /// <param name="content">The decoded response text.</param>
        /// <param name="declaredType">The endpoint response type.</param>
        /// <returns>A non-null value assignable to <paramref name="declaredType"/>.</returns>
        object Deserialize(string content, Type declaredType);
    }
}
