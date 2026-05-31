#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Data.Dynamic;
using NekoLib.Data.Gateway;
using NekoLib.Data.Query;

namespace NekoLib.Data.Internal.Gateway
{
    public partial class DatabaseGateway
    {
        Task<List<T>> IDtoQueryGateway.GetDto<T>(
            string sql,
            Dictionary<string, object?>? parameters,
            CancellationToken ct)
        {
            return GetDtoFromSql<T>(sql, parameters, ct);
        }

        Task<int> IDmlGateway.Insert(
            string sql,
            Dictionary<string, object?>? parameters,
            CancellationToken ct,
            DbSession? session)
        {
            return ExecuteDmlAsync(sql, parameters, ct, session);
        }

        Task<int> IDmlGateway.Update(
            string sql,
            Dictionary<string, object?>? parameters,
            CancellationToken ct,
            DbSession? session)
        {
            return ExecuteDmlAsync(sql, parameters, ct, session);
        }

        Task<int> IDmlGateway.Delete(
            string sql,
            Dictionary<string, object?>? parameters,
            CancellationToken ct,
            DbSession? session)
        {
            return ExecuteDmlAsync(sql, parameters, ct, session);
        }

        private Task<int> ExecuteDmlAsync(
            string sql,
            Dictionary<string, object?>? parameters,
            CancellationToken ct,
            DbSession? session)
        {
            return WithCommandAsync(sql, parameters, cmd => ExecuteNonQuerySafeAsync(cmd, ct), ct, session);
        }

#if NET6_0_OR_GREATER
        IAsyncEnumerable<Dictionary<string, RecordItem>> IDqlStreamingGateway.StreamRaw(
            string sql,
            Dictionary<string, object?>? parameters,
            CancellationToken ct)
        {
            return StreamRawCore(new DatabaseQuery(sql, parameters ?? new Dictionary<string, object?>()), ct);
        }

#endif
    }
}
