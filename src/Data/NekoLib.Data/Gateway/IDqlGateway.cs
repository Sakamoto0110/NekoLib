using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Data.Dynamic;
using NekoLib.Data.Query;
#if NET6_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace NekoLib.Data.Gateway
{
    /// <summary>Combines raw, DTO, and dynamic buffered query capability views.</summary>
    public interface IDqlGateway :
        IRawQueryGateway,
        IDtoQueryGateway,
        IDynamicQueryGateway
    {
    }

    /// <summary>
    /// Provides buffered and callback query operations using the intentionally
    /// textual <see cref="RecordItem"/> compatibility shape.
    /// </summary>
    public interface IRawQueryGateway
    {
        /// <summary>Tests whether trusted SQL returns at least one row.</summary>
        /// <param name="sql">Provider-specific SQL; identifiers and SQL fragments are trusted input.</param>
        /// <param name="ct">Cancellation observed by asynchronous provider operations.</param>
        /// <returns><see langword="true"/> when the reader exposes at least one row.</returns>
        Task<bool> ContainsData(
            string sql,
            CancellationToken ct = default);

        /// <summary>Tests whether parameterized trusted SQL returns at least one row.</summary>
        /// <param name="sql">Provider-specific SQL; identifiers and SQL fragments are trusted input.</param>
        /// <param name="parameters">Values or <see cref="DbParameterSpec"/> entries keyed by logical name.</param>
        /// <param name="ct">Cancellation observed by asynchronous provider operations.</param>
        /// <returns><see langword="true"/> when the reader exposes at least one row.</returns>
        Task<bool> ContainsData(
            string sql,
            Dictionary<string, object?>? parameters,
            CancellationToken ct = default);

        /// <summary>Tests whether trusted SQL returns a row in an existing session.</summary>
        /// <param name="sql">Provider-specific SQL; identifiers and SQL fragments are trusted input.</param>
        /// <param name="session">An open session affiliated with this gateway context.</param>
        /// <param name="ct">Cancellation observed by asynchronous provider operations.</param>
        /// <returns><see langword="true"/> when the reader exposes at least one row.</returns>
        Task<bool> ContainsData(
            string sql,
            DbSession session,
            CancellationToken ct = default);

        /// <summary>Tests whether parameterized trusted SQL returns a row in an existing session.</summary>
        /// <param name="sql">Provider-specific SQL; identifiers and SQL fragments are trusted input.</param>
        /// <param name="parameters">Values or parameter specifications keyed by logical name.</param>
        /// <param name="session">An open session affiliated with this gateway context.</param>
        /// <param name="ct">Cancellation observed by asynchronous provider operations.</param>
        /// <returns><see langword="true"/> when the reader exposes at least one row.</returns>
        Task<bool> ContainsData(
            string sql,
            Dictionary<string, object?>? parameters,
            DbSession session,
            CancellationToken ct = default);

        /// <summary>Buffers textual rows produced by trusted SQL.</summary>
        /// <param name="sql">Provider-specific SQL; identifiers and SQL fragments are trusted input.</param>
        /// <param name="parameters">Optional values or parameter specifications keyed by logical name.</param>
        /// <param name="ct">Cancellation observed by asynchronous provider operations.</param>
        /// <returns>The complete textual result set.</returns>
        Task<List<Dictionary<string, RecordItem>>> GetRaw(
            string sql,
            Dictionary<string, object?>? parameters = null,
            CancellationToken ct = default);

        /// <summary>Buffers textual rows produced by trusted SQL in an existing session.</summary>
        /// <param name="sql">Provider-specific SQL; identifiers and SQL fragments are trusted input.</param>
        /// <param name="parameters">Optional values or parameter specifications keyed by logical name.</param>
        /// <param name="session">An open session affiliated with this gateway context.</param>
        /// <param name="ct">Cancellation observed by asynchronous provider operations.</param>
        /// <returns>The complete textual result set.</returns>
        Task<List<Dictionary<string, RecordItem>>> GetRaw(
            string sql,
            Dictionary<string, object?>? parameters,
            DbSession session,
            CancellationToken ct = default);

        /// <summary>Builds, translates, and buffers textual rows on an owned connection.</summary>
        /// <param name="builder">The configured SELECT builder.</param>
        /// <param name="ct">Cancellation observed by asynchronous provider operations.</param>
        /// <returns>The complete textual result set.</returns>
        Task<List<Dictionary<string, RecordItem>>> GetRaw(
            QueryBuilder builder,
            CancellationToken ct = default);

        /// <summary>Builds, translates, and buffers textual rows in an existing session.</summary>
        /// <param name="builder">The configured SELECT builder.</param>
        /// <param name="session">An open session affiliated with this gateway context.</param>
        /// <param name="ct">Cancellation observed by asynchronous provider operations.</param>
        /// <returns>The complete textual result set.</returns>
        Task<List<Dictionary<string, RecordItem>>> GetRaw(
            QueryBuilder builder,
            DbSession session,
            CancellationToken ct = default);

        /// <summary>Invokes a callback synchronously for each textual row.</summary>
        /// <param name="sql">Provider-specific SQL; identifiers and SQL fragments are trusted input.</param>
        /// <param name="callback">The inline row callback; its exception terminates the operation.</param>
        /// <param name="ct">Cancellation observed between rows and by asynchronous provider operations.</param>
        /// <returns>A task that completes after the reader and owned connection are disposed.</returns>
        Task ReadRaw(
            string sql,
            Action<Dictionary<string, RecordItem>> callback,
            CancellationToken ct = default);

        /// <summary>Invokes a callback synchronously for each textual row in an existing session.</summary>
        /// <param name="sql">Provider-specific SQL; identifiers and SQL fragments are trusted input.</param>
        /// <param name="parameters">Optional values or parameter specifications keyed by logical name.</param>
        /// <param name="callback">The inline row callback; its exception terminates the operation.</param>
        /// <param name="session">An open session affiliated with this gateway context.</param>
        /// <param name="ct">Cancellation observed between rows and by asynchronous provider operations.</param>
        /// <returns>A task that completes after the reader and command are disposed.</returns>
        Task ReadRaw(
            string sql,
            Dictionary<string, object?>? parameters,
            Action<Dictionary<string, RecordItem>> callback,
            DbSession session,
            CancellationToken ct = default);

        /// <summary>Builds, translates, and invokes a callback for each textual row.</summary>
        /// <param name="builder">The configured SELECT builder.</param>
        /// <param name="callback">The inline row callback; its exception terminates the operation.</param>
        /// <param name="ct">Cancellation observed between rows and by asynchronous provider operations.</param>
        /// <returns>A task that completes after the reader and owned connection are disposed.</returns>
        Task ReadRaw(
            QueryBuilder builder,
            Action<Dictionary<string, RecordItem>> callback,
            CancellationToken ct = default);

        /// <summary>Builds, translates, and invokes a callback for each textual row in a session.</summary>
        /// <param name="builder">The configured SELECT builder.</param>
        /// <param name="callback">The inline row callback; its exception terminates the operation.</param>
        /// <param name="session">An open session affiliated with this gateway context.</param>
        /// <param name="ct">Cancellation observed between rows and by asynchronous provider operations.</param>
        /// <returns>A task that completes after the reader and command are disposed.</returns>
        Task ReadRaw(
            QueryBuilder builder,
            Action<Dictionary<string, RecordItem>> callback,
            DbSession session,
            CancellationToken ct = default);
    }

    /// <summary>Provides buffered and callback queries that preserve provider values while mapping DTOs.</summary>
    public interface IDtoQueryGateway
    {
        /// <summary>Buffers DTOs produced by trusted SQL.</summary>
        /// <typeparam name="T">A DTO with a public parameterless constructor and writable public properties.</typeparam>
        /// <param name="sql">Provider-specific SQL; identifiers and SQL fragments are trusted input.</param>
        /// <param name="parameters">Optional values or parameter specifications keyed by logical name.</param>
        /// <param name="ct">Cancellation observed by asynchronous provider operations.</param>
        /// <returns>The complete mapped result set.</returns>
        Task<List<T>> GetDto<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(
            string sql,
            Dictionary<string, object?>? parameters = null,
            CancellationToken ct = default)
            where T : new();

        /// <summary>Buffers DTOs produced by trusted SQL in an existing session.</summary>
        /// <typeparam name="T">A DTO with a public parameterless constructor and writable public properties.</typeparam>
        /// <param name="sql">Provider-specific SQL; identifiers and SQL fragments are trusted input.</param>
        /// <param name="parameters">Optional values or parameter specifications keyed by logical name.</param>
        /// <param name="session">An open session affiliated with this gateway context.</param>
        /// <param name="ct">Cancellation observed by asynchronous provider operations.</param>
        /// <returns>The complete mapped result set.</returns>
        Task<List<T>> GetDto<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(
            string sql,
            Dictionary<string, object?>? parameters,
            DbSession session,
            CancellationToken ct = default)
            where T : new();

        /// <summary>Builds, translates, and buffers DTOs on an owned connection.</summary>
        /// <typeparam name="T">A DTO with a public parameterless constructor and writable public properties.</typeparam>
        /// <param name="builder">The configured SELECT builder.</param>
        /// <param name="ct">Cancellation observed by asynchronous provider operations.</param>
        /// <returns>The complete mapped result set.</returns>
        Task<List<T>> GetDto<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(
            QueryBuilder builder,
            CancellationToken ct = default)
            where T : new();

        /// <summary>Builds, translates, and buffers DTOs in an existing session.</summary>
        /// <typeparam name="T">A DTO with a public parameterless constructor and writable public properties.</typeparam>
        /// <param name="builder">The configured SELECT builder.</param>
        /// <param name="session">An open session affiliated with this gateway context.</param>
        /// <param name="ct">Cancellation observed by asynchronous provider operations.</param>
        /// <returns>The complete mapped result set.</returns>
        Task<List<T>> GetDto<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(
            QueryBuilder builder,
            DbSession session,
            CancellationToken ct = default)
            where T : new();

        /// <summary>Builds, translates, and invokes a callback synchronously for each DTO.</summary>
        /// <typeparam name="T">A DTO with a public parameterless constructor and writable public properties.</typeparam>
        /// <param name="builder">The configured SELECT builder.</param>
        /// <param name="callback">The inline DTO callback; its exception terminates the operation.</param>
        /// <param name="ct">Cancellation observed between rows and by asynchronous provider operations.</param>
        /// <returns>A task that completes after the reader and owned connection are disposed.</returns>
        Task ReadDto<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(
            QueryBuilder builder,
            Action<T> callback,
            CancellationToken ct = default)
            where T : new();

        /// <summary>Builds, translates, and invokes a DTO callback in an existing session.</summary>
        /// <typeparam name="T">A DTO with a public parameterless constructor and writable public properties.</typeparam>
        /// <param name="builder">The configured SELECT builder.</param>
        /// <param name="callback">The inline DTO callback; its exception terminates the operation.</param>
        /// <param name="session">An open session affiliated with this gateway context.</param>
        /// <param name="ct">Cancellation observed between rows and by asynchronous provider operations.</param>
        /// <returns>A task that completes after the reader and command are disposed.</returns>
        Task ReadDto<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(
            QueryBuilder builder,
            Action<T> callback,
            DbSession session,
            CancellationToken ct = default)
            where T : new();
    }

    /// <summary>Provides buffered and callback queries using <see cref="DynamicRow"/>.</summary>
    public interface IDynamicQueryGateway
    {
        /// <summary>Builds, translates, and buffers dynamic rows on an owned connection.</summary>
        /// <param name="builder">The configured SELECT builder.</param>
        /// <param name="ct">Cancellation observed by asynchronous provider operations.</param>
        /// <returns>The complete dynamic result set.</returns>
        Task<List<DynamicRow>> GetDynamic(
            QueryBuilder builder,
            CancellationToken ct = default);

        /// <summary>Builds, translates, and buffers dynamic rows in an existing session.</summary>
        /// <param name="builder">The configured SELECT builder.</param>
        /// <param name="session">An open session affiliated with this gateway context.</param>
        /// <param name="ct">Cancellation observed by asynchronous provider operations.</param>
        /// <returns>The complete dynamic result set.</returns>
        Task<List<DynamicRow>> GetDynamic(
            QueryBuilder builder,
            DbSession session,
            CancellationToken ct = default);

        /// <summary>Builds, translates, and invokes a callback for each dynamic row.</summary>
        /// <param name="builder">The configured SELECT builder.</param>
        /// <param name="callback">The inline row callback; its exception terminates the operation.</param>
        /// <param name="ct">Cancellation observed between rows and by asynchronous provider operations.</param>
        /// <returns>A task that completes after the reader and owned connection are disposed.</returns>
        Task ReadDynamic(
            QueryBuilder builder,
            Action<DynamicRow> callback,
            CancellationToken ct = default);

        /// <summary>Builds, translates, and invokes a dynamic-row callback in a session.</summary>
        /// <param name="builder">The configured SELECT builder.</param>
        /// <param name="callback">The inline row callback; its exception terminates the operation.</param>
        /// <param name="session">An open session affiliated with this gateway context.</param>
        /// <param name="ct">Cancellation observed between rows and by asynchronous provider operations.</param>
        /// <returns>A task that completes after the reader and command are disposed.</returns>
        Task ReadDynamic(
            QueryBuilder builder,
            Action<DynamicRow> callback,
            DbSession session,
            CancellationToken ct = default);
    }

}
