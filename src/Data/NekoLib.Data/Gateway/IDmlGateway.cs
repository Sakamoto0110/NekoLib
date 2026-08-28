using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Data.Query;

namespace NekoLib.Data.Gateway
{
    /// <summary>
    /// Provides non-query command capabilities. This is a consumer-facing view
    /// of a gateway, not a provider plug-in contract.
    /// </summary>
    public interface IDmlGateway
    {
        /// <summary>Executes trusted INSERT SQL on an owned connection.</summary>
        /// <param name="sql">Provider-specific SQL; identifiers and SQL fragments are trusted input.</param>
        /// <param name="parameters">Optional values or <see cref="DbParameterSpec"/> entries keyed by logical name.</param>
        /// <param name="ct">Cancellation observed by asynchronous provider operations.</param>
        /// <returns>The provider-reported affected-row count.</returns>
        Task<int> Insert(
            string sql,
            Dictionary<string, object?>? parameters = null,
            CancellationToken ct = default);

        /// <summary>Executes trusted INSERT SQL in an existing session.</summary>
        /// <param name="sql">Provider-specific SQL; identifiers and SQL fragments are trusted input.</param>
        /// <param name="parameters">Optional values or parameter specifications keyed by logical name.</param>
        /// <param name="session">An open session affiliated with this gateway context.</param>
        /// <param name="ct">Cancellation observed by asynchronous provider operations.</param>
        /// <returns>The provider-reported affected-row count.</returns>
        Task<int> Insert(
            string sql,
            Dictionary<string, object?>? parameters,
            DbSession session,
            CancellationToken ct = default);

        /// <summary>Executes trusted UPDATE SQL on an owned connection.</summary>
        /// <param name="sql">Provider-specific SQL; identifiers and SQL fragments are trusted input.</param>
        /// <param name="parameters">Optional values or parameter specifications keyed by logical name.</param>
        /// <param name="ct">Cancellation observed by asynchronous provider operations.</param>
        /// <returns>The provider-reported affected-row count.</returns>
        Task<int> Update(
            string sql,
            Dictionary<string, object?>? parameters = null,
            CancellationToken ct = default);

        /// <summary>Executes trusted UPDATE SQL in an existing session.</summary>
        /// <param name="sql">Provider-specific SQL; identifiers and SQL fragments are trusted input.</param>
        /// <param name="parameters">Optional values or parameter specifications keyed by logical name.</param>
        /// <param name="session">An open session affiliated with this gateway context.</param>
        /// <param name="ct">Cancellation observed by asynchronous provider operations.</param>
        /// <returns>The provider-reported affected-row count.</returns>
        Task<int> Update(
            string sql,
            Dictionary<string, object?>? parameters,
            DbSession session,
            CancellationToken ct = default);

        /// <summary>Executes trusted DELETE SQL on an owned connection.</summary>
        /// <param name="sql">Provider-specific SQL; identifiers and SQL fragments are trusted input.</param>
        /// <param name="parameters">Optional values or parameter specifications keyed by logical name.</param>
        /// <param name="ct">Cancellation observed by asynchronous provider operations.</param>
        /// <returns>The provider-reported affected-row count.</returns>
        Task<int> Delete(
            string sql,
            Dictionary<string, object?>? parameters = null,
            CancellationToken ct = default);

        /// <summary>Executes trusted DELETE SQL in an existing session.</summary>
        /// <param name="sql">Provider-specific SQL; identifiers and SQL fragments are trusted input.</param>
        /// <param name="parameters">Optional values or parameter specifications keyed by logical name.</param>
        /// <param name="session">An open session affiliated with this gateway context.</param>
        /// <param name="ct">Cancellation observed by asynchronous provider operations.</param>
        /// <returns>The provider-reported affected-row count.</returns>
        Task<int> Delete(
            string sql,
            Dictionary<string, object?>? parameters,
            DbSession session,
            CancellationToken ct = default);

        /// <summary>Builds, translates, and executes an INSERT on an owned connection.</summary>
        /// <param name="builder">The configured INSERT builder.</param>
        /// <param name="ct">Cancellation observed by asynchronous provider operations.</param>
        /// <returns>The provider-reported affected-row count.</returns>
        Task<int> Insert(
            QueryBuilder builder,
            CancellationToken ct = default);

        /// <summary>Builds, translates, and executes an INSERT in an existing session.</summary>
        /// <param name="builder">The configured INSERT builder.</param>
        /// <param name="session">An open session affiliated with this gateway context.</param>
        /// <param name="ct">Cancellation observed by asynchronous provider operations.</param>
        /// <returns>The provider-reported affected-row count.</returns>
        Task<int> Insert(
            QueryBuilder builder,
            DbSession session,
            CancellationToken ct = default);

        /// <summary>Builds, translates, and executes an UPDATE on an owned connection.</summary>
        /// <param name="builder">The configured UPDATE builder.</param>
        /// <param name="ct">Cancellation observed by asynchronous provider operations.</param>
        /// <returns>The provider-reported affected-row count.</returns>
        Task<int> Update(
            QueryBuilder builder,
            CancellationToken ct = default);

        /// <summary>Builds, translates, and executes an UPDATE in an existing session.</summary>
        /// <param name="builder">The configured UPDATE builder.</param>
        /// <param name="session">An open session affiliated with this gateway context.</param>
        /// <param name="ct">Cancellation observed by asynchronous provider operations.</param>
        /// <returns>The provider-reported affected-row count.</returns>
        Task<int> Update(
            QueryBuilder builder,
            DbSession session,
            CancellationToken ct = default);

        /// <summary>Builds, translates, and executes a DELETE on an owned connection.</summary>
        /// <param name="builder">The configured DELETE builder.</param>
        /// <param name="ct">Cancellation observed by asynchronous provider operations.</param>
        /// <returns>The provider-reported affected-row count.</returns>
        Task<int> Delete(
            QueryBuilder builder,
            CancellationToken ct = default);

        /// <summary>Builds, translates, and executes a DELETE in an existing session.</summary>
        /// <param name="builder">The configured DELETE builder.</param>
        /// <param name="session">An open session affiliated with this gateway context.</param>
        /// <param name="ct">Cancellation observed by asynchronous provider operations.</param>
        /// <returns>The provider-reported affected-row count.</returns>
        Task<int> Delete(
            QueryBuilder builder,
            DbSession session,
            CancellationToken ct = default);
    }
}
