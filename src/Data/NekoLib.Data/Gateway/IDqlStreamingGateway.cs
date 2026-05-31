using System.Collections.Generic;
using System.Threading;
using NekoLib.Data.Dynamic;
using NekoLib.Data.Query;

namespace NekoLib.Data.Gateway
{
#if !NET6_0_OR_GREATER
    [System.Obsolete("Streaming gateway members are implemented only on net6.0 or greater targets.", true)]
#endif
    public interface IDqlStreamingGateway
    {
        IAsyncEnumerable<Dictionary<string, RecordItem>> StreamRaw(
            string sql,
            Dictionary<string, object?>? parameters = null,
            CancellationToken ct = default);

        IAsyncEnumerable<T> StreamDto<T>(
            QueryBuilder builder,
            CancellationToken ct = default)
            where T : new();

        IAsyncEnumerable<DynamicRow> StreamDynamic(
            QueryBuilder builder,
            CancellationToken ct = default);

        IAsyncEnumerable<dynamic> StreamData(
            QueryBuilder builder,
            CancellationToken ct = default);

        IAsyncEnumerable<T> StreamData<T>(
            QueryBuilder builder,
            CancellationToken ct = default);
    }
}
