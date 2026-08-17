#if NET6_0_OR_GREATER
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using NekoLib.Data.Dynamic;
using NekoLib.Data.Query;

namespace NekoLib.Data.Gateway
{
    public interface IDqlStreamingGateway
    {
        IAsyncEnumerable<Dictionary<string, RecordItem>> StreamRaw(
            string sql,
            Dictionary<string, object?>? parameters = null,
            CancellationToken ct = default);

        IAsyncEnumerable<Dictionary<string, RecordItem>> StreamRaw(
            string sql,
            Dictionary<string, object?>? parameters,
            DbSession session,
            CancellationToken ct = default);

        IAsyncEnumerable<T> StreamDto<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
            T>(
            QueryBuilder builder,
            CancellationToken ct = default)
            where T : new();

        IAsyncEnumerable<T> StreamDto<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
            T>(
            QueryBuilder builder,
            DbSession session,
            CancellationToken ct = default)
            where T : new();

        IAsyncEnumerable<DynamicRow> StreamDynamic(
            QueryBuilder builder,
            CancellationToken ct = default);

        IAsyncEnumerable<DynamicRow> StreamDynamic(
            QueryBuilder builder,
            DbSession session,
            CancellationToken ct = default);

    }
}
#endif
