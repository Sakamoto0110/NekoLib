 using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NekoLib.Data.Gateway
{
    public interface IDqlStreamingGateway
    {
        IAsyncEnumerable<Dictionary<string, RecordItem>> StreamRaw(
            string sql,
            Dictionary<string, object?>? parameters = null,
            CancellationToken ct = default);

        IAsyncEnumerable<T> StreamDto<T>(
            string sql,
            Dictionary<string, object?>? parameters = null,
            CancellationToken ct = default)
            where T : new();

        IAsyncEnumerable<dynamic> StreamDynamic(
            string sql,
            Dictionary<string, object?>? parameters = null,
            CancellationToken ct = default);

        IAsyncEnumerable<T> StreamUniversal<T>(
            string sql,
            Dictionary<string, object?>? parameters = null,
            CancellationToken ct = default)
            where T : new();
    }
}
