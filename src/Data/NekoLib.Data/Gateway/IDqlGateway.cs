using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Data.Dynamic;
using NekoLib.Data.Query;

namespace NekoLib.Data.Gateway
{
    public interface IDqlGateway :
        IRawQueryGateway,
        IDtoQueryGateway,
        IDynamicQueryGateway,
        IUniversalQueryGateway
    {
    }

    public interface IRawQueryGateway
    {
        Task<bool> ContainsData(
            string sql,
            CancellationToken ct = default);

        Task<List<Dictionary<string, RecordItem>>> GetRaw(
            string sql,
            Dictionary<string, object?>? parameters = null,
            CancellationToken ct = default);

        Task<List<Dictionary<string, RecordItem>>> GetRaw(
            QueryBuilder builder,
            CancellationToken ct = default);

        Task ReadRaw(
            string sql,
            Action<Dictionary<string, RecordItem>> callback,
            CancellationToken ct = default);

        Task ReadRaw(
            QueryBuilder builder,
            Action<Dictionary<string, RecordItem>> callback,
            CancellationToken ct = default);
    }

    public interface IDtoQueryGateway
    {
        Task<List<T>> GetDto<T>(
            string sql,
            Dictionary<string, object?>? parameters = null,
            CancellationToken ct = default)
            where T : new();

        Task<List<T>> GetDto<T>(
            QueryBuilder builder,
            CancellationToken ct = default)
            where T : new();

        Task ReadDto<T>(
            QueryBuilder builder,
            Action<T> callback,
            CancellationToken ct = default)
            where T : new();
    }

    public interface IDynamicQueryGateway
    {
        Task<List<DynamicRow>> GetDynamic(
            QueryBuilder builder,
            CancellationToken ct = default);

        Task ReadDynamic(
            QueryBuilder builder,
            Action<DynamicRow> callback,
            CancellationToken ct = default);
    }

    public interface IUniversalQueryGateway
    {
        Task<List<T>> Get<TTranslator, T>(
            QueryBuilder builder,
            CancellationToken ct = default)
            where TTranslator : IDbQueryTranslator, new()
            where T : new();

        Task Read(
            QueryBuilder builder,
            Delegate handler,
            CancellationToken ct = default);

        Task Read<T>(
            QueryBuilder builder,
            Action<T> callback,
            CancellationToken ct = default);
    }
}
