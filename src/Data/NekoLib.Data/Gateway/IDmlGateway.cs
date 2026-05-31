using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Data.Query;

namespace NekoLib.Data.Gateway
{
    public interface IDmlGateway
    {
        Task<int> Insert(
            string sql,
            Dictionary<string, object?>? parameters = null,
            CancellationToken ct = default,
            DbSession? session = null);

        Task<int> Update(
            string sql,
            Dictionary<string, object?>? parameters = null,
            CancellationToken ct = default,
            DbSession? session = null);

        Task<int> Delete(
            string sql,
            Dictionary<string, object?>? parameters = null,
            CancellationToken ct = default,
            DbSession? session = null);

        Task<int> Insert(
            QueryBuilder builder,
            CancellationToken ct = default);

        Task<int> Update(
            QueryBuilder builder,
            CancellationToken ct = default);
    }
}
