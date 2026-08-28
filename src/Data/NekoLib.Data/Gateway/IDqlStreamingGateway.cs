#if NET6_0_OR_GREATER
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using NekoLib.Data.Dynamic;
using NekoLib.Data.Query;

namespace NekoLib.Data.Gateway
{
    /// <summary>
    /// Provides pull-based asynchronous streaming on targets that support
    /// <see cref="IAsyncEnumerable{T}"/>.
    /// </summary>
    /// <remarks>
    /// Execution begins when enumeration starts. The reader, command, and any
    /// owned connection remain open until enumeration completes or is disposed;
    /// the context reports exactly one terminal event after cleanup.
    /// </remarks>
    public interface IDqlStreamingGateway
    {
        /// <summary>Streams textual rows produced by trusted SQL on an owned connection.</summary>
        /// <param name="sql">Provider-specific SQL; identifiers and SQL fragments are trusted input.</param>
        /// <param name="parameters">Optional values or <see cref="DbParameterSpec"/> entries keyed by logical name.</param>
        /// <param name="ct">Cancellation observed during setup and enumeration.</param>
        /// <returns>A lazy stream of intentionally textual rows.</returns>
        IAsyncEnumerable<Dictionary<string, RecordItem>> StreamRaw(
            string sql,
            Dictionary<string, object?>? parameters = null,
            CancellationToken ct = default);

        /// <summary>Streams textual rows produced by trusted SQL in an existing session.</summary>
        /// <param name="sql">Provider-specific SQL; identifiers and SQL fragments are trusted input.</param>
        /// <param name="parameters">Optional values or parameter specifications keyed by logical name.</param>
        /// <param name="session">An open session held for the entire enumeration.</param>
        /// <param name="ct">Cancellation observed during setup and enumeration.</param>
        /// <returns>A lazy stream of intentionally textual rows.</returns>
        IAsyncEnumerable<Dictionary<string, RecordItem>> StreamRaw(
            string sql,
            Dictionary<string, object?>? parameters,
            DbSession session,
            CancellationToken ct = default);

        /// <summary>Builds, translates, and streams DTOs on an owned connection.</summary>
        /// <typeparam name="T">A DTO with a public parameterless constructor and writable public properties.</typeparam>
        /// <param name="builder">The configured SELECT builder.</param>
        /// <param name="ct">Cancellation observed during setup and enumeration.</param>
        /// <returns>A lazy stream of mapped DTOs.</returns>
        IAsyncEnumerable<T> StreamDto<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
            T>(
            QueryBuilder builder,
            CancellationToken ct = default)
            where T : new();

        /// <summary>Builds, translates, and streams DTOs in an existing session.</summary>
        /// <typeparam name="T">A DTO with a public parameterless constructor and writable public properties.</typeparam>
        /// <param name="builder">The configured SELECT builder.</param>
        /// <param name="session">An open session held for the entire enumeration.</param>
        /// <param name="ct">Cancellation observed during setup and enumeration.</param>
        /// <returns>A lazy stream of mapped DTOs.</returns>
        IAsyncEnumerable<T> StreamDto<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
            T>(
            QueryBuilder builder,
            DbSession session,
            CancellationToken ct = default)
            where T : new();

        /// <summary>Builds, translates, and streams dynamic rows on an owned connection.</summary>
        /// <param name="builder">The configured SELECT builder.</param>
        /// <param name="ct">Cancellation observed during setup and enumeration.</param>
        /// <returns>A lazy stream of dynamic rows.</returns>
        IAsyncEnumerable<DynamicRow> StreamDynamic(
            QueryBuilder builder,
            CancellationToken ct = default);

        /// <summary>Builds, translates, and streams dynamic rows in an existing session.</summary>
        /// <param name="builder">The configured SELECT builder.</param>
        /// <param name="session">An open session held for the entire enumeration.</param>
        /// <param name="ct">Cancellation observed during setup and enumeration.</param>
        /// <returns>A lazy stream of dynamic rows.</returns>
        IAsyncEnumerable<DynamicRow> StreamDynamic(
            QueryBuilder builder,
            DbSession session,
            CancellationToken ct = default);

    }
}
#endif
