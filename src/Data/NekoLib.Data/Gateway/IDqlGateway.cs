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
    public interface IDqlGateway :
        IRawQueryGateway,
        IDtoQueryGateway,
        IDynamicQueryGateway
    {
    }

    public interface IRawQueryGateway
    {
        Task<bool> ContainsData(
            string sql,
            CancellationToken ct = default);

        Task<bool> ContainsData(
            string sql,
            Dictionary<string, object?>? parameters,
            CancellationToken ct = default);

        Task<bool> ContainsData(
            string sql,
            DbSession session,
            CancellationToken ct = default);

        Task<bool> ContainsData(
            string sql,
            Dictionary<string, object?>? parameters,
            DbSession session,
            CancellationToken ct = default);

        Task<List<Dictionary<string, RecordItem>>> GetRaw(
            string sql,
            Dictionary<string, object?>? parameters = null,
            CancellationToken ct = default);

        Task<List<Dictionary<string, RecordItem>>> GetRaw(
            string sql,
            Dictionary<string, object?>? parameters,
            DbSession session,
            CancellationToken ct = default);

        Task<List<Dictionary<string, RecordItem>>> GetRaw(
            QueryBuilder builder,
            CancellationToken ct = default);

        Task<List<Dictionary<string, RecordItem>>> GetRaw(
            QueryBuilder builder,
            DbSession session,
            CancellationToken ct = default);

        Task ReadRaw(
            string sql,
            Action<Dictionary<string, RecordItem>> callback,
            CancellationToken ct = default);

        Task ReadRaw(
            string sql,
            Dictionary<string, object?>? parameters,
            Action<Dictionary<string, RecordItem>> callback,
            DbSession session,
            CancellationToken ct = default);

        Task ReadRaw(
            QueryBuilder builder,
            Action<Dictionary<string, RecordItem>> callback,
            CancellationToken ct = default);

        Task ReadRaw(
            QueryBuilder builder,
            Action<Dictionary<string, RecordItem>> callback,
            DbSession session,
            CancellationToken ct = default);
    }

    public interface IDtoQueryGateway
    {
        Task<List<T>> GetDto<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(
            string sql,
            Dictionary<string, object?>? parameters = null,
            CancellationToken ct = default)
            where T : new();

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

        Task<List<T>> GetDto<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(
            QueryBuilder builder,
            CancellationToken ct = default)
            where T : new();

        Task<List<T>> GetDto<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(
            QueryBuilder builder,
            DbSession session,
            CancellationToken ct = default)
            where T : new();

        Task ReadDto<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(
            QueryBuilder builder,
            Action<T> callback,
            CancellationToken ct = default)
            where T : new();

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

    public interface IDynamicQueryGateway
    {
        Task<List<DynamicRow>> GetDynamic(
            QueryBuilder builder,
            CancellationToken ct = default);

        Task<List<DynamicRow>> GetDynamic(
            QueryBuilder builder,
            DbSession session,
            CancellationToken ct = default);

        Task ReadDynamic(
            QueryBuilder builder,
            Action<DynamicRow> callback,
            CancellationToken ct = default);

        Task ReadDynamic(
            QueryBuilder builder,
            Action<DynamicRow> callback,
            DbSession session,
            CancellationToken ct = default);
    }

}
