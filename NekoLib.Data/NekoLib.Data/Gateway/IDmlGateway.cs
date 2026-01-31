
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NekoLib.Data.Gateway
{
    public interface IDmlGateway
    {
        Task Insert(
            string sql,
            Dictionary<string, object?>? parameters = null,
            CancellationToken ct = default,
            DbSession? session = null);

        Task Update(
            string sql,
            Dictionary<string, object?>? parameters = null,
            CancellationToken ct = default,
            DbSession? session = null);

        Task Delete(
            string sql,
            Dictionary<string, object?>? parameters = null,
            CancellationToken ct = default,
            DbSession? session = null);
    }
}
