 using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NekoLib.Data.Gateway
{
    public interface IDqlGateway
    {
        Task<bool> ContainsData(
            string sql,
            CancellationToken ct = default);

        Task<List<Dictionary<string, RecordItem>>> GetRaw(
            string sql,
            Dictionary<string, object?>? parameters = null,
            CancellationToken ct = default);

        Task<List<T>> GetDto<T>(
            string sql,
            Dictionary<string, object?>? parameters = null,
            CancellationToken ct = default)
            where T : new();

        Task<List<dynamic>> GetDynamic(
            string sql,
            Dictionary<string, object?>? parameters = null,
            CancellationToken ct = default);

        Task<List<T>> GetUniversal<T>(
            string sql,
            Dictionary<string, object?>? parameters = null,
            CancellationToken ct = default)
            where T : new();
    }
}
