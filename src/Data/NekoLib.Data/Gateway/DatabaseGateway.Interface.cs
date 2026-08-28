#nullable enable
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
    public partial class DatabaseGateway
    {
        /// <inheritdoc/>
        public Task<List<T>> GetDto<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(
            string sql,
            Dictionary<string, object?>? parameters = null,
            CancellationToken ct = default)
            where T : new()
        {
            return GetDtoFromSql<T>(sql, parameters, ct);
        }

        /// <inheritdoc/>
        public Task<List<T>> GetDto<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(
            string sql,
            Dictionary<string, object?>? parameters,
            DbSession session,
            CancellationToken ct = default)
            where T : new()
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            return GetDtoFromSql<T>(sql, parameters, ct, session);
        }

        /// <inheritdoc/>
        public Task<int> Insert(
            string sql,
            Dictionary<string, object?>? parameters = null,
            CancellationToken ct = default)
        {
            return ExecuteDmlAsync(sql, parameters, ct, null);
        }

        /// <inheritdoc/>
        public Task<int> Insert(
            string sql,
            Dictionary<string, object?>? parameters,
            DbSession session,
            CancellationToken ct = default)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            return ExecuteDmlAsync(sql, parameters, ct, session);
        }

        /// <inheritdoc/>
        public Task<int> Update(
            string sql,
            Dictionary<string, object?>? parameters = null,
            CancellationToken ct = default)
        {
            return ExecuteDmlAsync(sql, parameters, ct, null);
        }

        /// <inheritdoc/>
        public Task<int> Update(
            string sql,
            Dictionary<string, object?>? parameters,
            DbSession session,
            CancellationToken ct = default)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            return ExecuteDmlAsync(sql, parameters, ct, session);
        }

        /// <inheritdoc/>
        public Task<int> Delete(
            string sql,
            Dictionary<string, object?>? parameters = null,
            CancellationToken ct = default)
        {
            return ExecuteDmlAsync(sql, parameters, ct, null);
        }

        /// <inheritdoc/>
        public Task<int> Delete(
            string sql,
            Dictionary<string, object?>? parameters,
            DbSession session,
            CancellationToken ct = default)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            return ExecuteDmlAsync(sql, parameters, ct, session);
        }

        private Task<int> ExecuteDmlAsync(
            string sql,
            Dictionary<string, object?>? parameters,
            CancellationToken ct,
            DbSession? session,
            DbCommandPolicy? commandPolicy = null,
            IReadOnlyList<LogicalParameter>? logicalParameters = null)
        {
            return WithCommandAsync(
                sql,
                parameters,
                cmd => ExecuteNonQuerySafeAsync(cmd, ct),
                ct,
                session,
                commandPolicy,
                logicalParameters);
        }

#if NET6_0_OR_GREATER
        /// <inheritdoc/>
        public IAsyncEnumerable<Dictionary<string, RecordItem>> StreamRaw(
            string sql,
            Dictionary<string, object?>? parameters = null,
            CancellationToken ct = default)
        {
            return StreamRawCore(new DatabaseQuery(sql, parameters ?? new Dictionary<string, object?>()), null, ct);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// The <see cref="System.Data.Common.DbDataReader"/> remains open for the entire enumeration,
        /// so the <paramref name="session"/> connection and transaction remain
        /// occupied while the consumer iterates. Avoid slow per-row I/O inside
        /// an open transaction; consume promptly or materialize the results.
        /// </remarks>
        public IAsyncEnumerable<Dictionary<string, RecordItem>> StreamRaw(
            string sql,
            Dictionary<string, object?>? parameters,
            DbSession session,
            CancellationToken ct = default)
        {
            return StreamRawCore(new DatabaseQuery(sql, parameters ?? new Dictionary<string, object?>()), session, ct);
        }

#endif
    }
}
