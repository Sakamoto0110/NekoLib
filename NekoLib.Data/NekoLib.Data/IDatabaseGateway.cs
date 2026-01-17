using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Data.Query;
namespace NekoLib.Data 
{
    /// <summary>
    /// PROTOTYPE INTERFACE – NOT FOR PRODUCTION USE
    /// 
    /// This interface represents the full conceptual surface
    /// of the DatabaseGateway without committing to implementation,
    /// provider details, or performance guarantees.
    /// </summary>
    public interface IDatabaseGatewayPrototype : IDisposable
    {
        // ─────────────────────────────────────────────────────────────
        // Lifecycle / State
        // ─────────────────────────────────────────────────────────────

        bool IsOpen { get; }
        bool IsDisposed { get; }

        void Open();
        void Close();

        Task OpenAsync(CancellationToken ct = default);
        Task CloseAsync(CancellationToken ct = default);

        // Optional hard reset (connection drop, state purge)
        void Reset();

        // ─────────────────────────────────────────────────────────────
        // Execution (Non-query)
        // ─────────────────────────────────────────────────────────────

        int Execute(QueryBuilder query,object? parameters = null);

        Task<int> ExecuteAsync(QueryBuilder query,object? parameters = null,CancellationToken ct = default);

        // Raw execution (string-based, optional escape hatch)
        int ExecuteRaw(string commandText,object? parameters = null,CommandType commandType = CommandType.Text);

        Task<int> ExecuteRawAsync(string commandText,object? parameters = null,CommandType commandType = CommandType.Text,CancellationToken ct = default);

        // ─────────────────────────────────────────────────────────────
        // Read – Strongly typed
        // ─────────────────────────────────────────────────────────────

        IEnumerable<T> Read<T>(QueryBuilder query,object? parameters = null);

        Task<IEnumerable<T>> ReadAsync<T>(QueryBuilder query,object? parameters = null,CancellationToken ct = default);

        // Callback-based read (no allocation guarantee)
        void Read<T>(QueryBuilder query,Action<T> onRow,object? parameters = null);

        Task ReadAsync<T>(QueryBuilder query,Action<T> onRow,object? parameters = null,CancellationToken ct = default);

        // Single-row helpers
        T? Get<T>(QueryBuilder query,object? parameters = null);

        Task<T?> GetAsync<T>(QueryBuilder query,object? parameters = null,CancellationToken ct = default);

        // ─────────────────────────────────────────────────────────────
        // Read – Dynamic / Universal
        // ─────────────────────────────────────────────────────────────

        IEnumerable<dynamic> ReadDynamic(QueryBuilder query,object? parameters = null);

        Task<IEnumerable<dynamic>> ReadDynamicAsync(QueryBuilder query,object? parameters = null,CancellationToken ct = default);

        void ReadDynamic(QueryBuilder query,Action<dynamic> onRow,object? parameters = null);

        Task ReadDynamicAsync(QueryBuilder query,Action<dynamic> onRow,object? parameters = null,CancellationToken ct = default);

        dynamic? GetDynamic(QueryBuilder query,object? parameters = null);

        Task<dynamic?> GetDynamicAsync(QueryBuilder query,object? parameters = null,CancellationToken ct = default);

        // Universal read (fallback: DTO → dynamic → raw)
        void ReadUniversal(QueryBuilder query,Action<object> onRow,object? parameters = null);

        Task ReadUniversalAsync(QueryBuilder query,Action<object> onRow,object? parameters = null,CancellationToken ct = default);

        // ─────────────────────────────────────────────────────────────
        // Streaming (conceptual – no yield guarantee)
        // ─────────────────────────────────────────────────────────────

        IAsyncEnumerable<T> StreamAsync<T>(QueryBuilder query,object? parameters = null,CancellationToken ct = default);

        IAsyncEnumerable<dynamic> StreamDynamicAsync(QueryBuilder query,object? parameters = null,CancellationToken ct = default);

        // Push-based streaming
        Task StreamAsync<T>(QueryBuilder query,Func<T, Task> onRowAsync,object? parameters = null,CancellationToken ct = default);

        // ─────────────────────────────────────────────────────────────
        // Mapping / Projection
        // ─────────────────────────────────────────────────────────────

        // Explicit mapper override
        IEnumerable<T> Read<T>(QueryBuilder query,Func<IDataRecord, T> mapper,object? parameters = null);

        Task<IEnumerable<T>> ReadAsync<T>(QueryBuilder query,Func<IDataRecord, T> mapper,object? parameters = null,CancellationToken ct = default);

        // ─────────────────────────────────────────────────────────────
        // Transactions (optional support)
        // ─────────────────────────────────────────────────────────────

        void BeginTransaction(IsolationLevel isolation = IsolationLevel.ReadCommitted);

        void CommitTransaction();
        void RollbackTransaction();

        Task BeginTransactionAsync(IsolationLevel isolation = IsolationLevel.ReadCommitted,CancellationToken ct = default);

        Task CommitTransactionAsync(CancellationToken ct = default);
        Task RollbackTransactionAsync(CancellationToken ct = default);

        bool IsInTransaction { get; }

        // ─────────────────────────────────────────────────────────────
        // Diagnostics / Metadata (read-only)
        // ─────────────────────────────────────────────────────────────

        string ProviderName { get; }
        string ConnectionStringMasked { get; }

        TimeSpan LastExecutionTime { get; }
        int LastAffectedRows { get; }

        // Optional hook (telemetry / logging bridge)
        event Action<string, TimeSpan>? OnCommandExecuted;
    }


















    public interface IDatabaseGateway : IDisposable
    {
        // ─────────────────────────────────────────────
        // Connection / lifecycle
        // ─────────────────────────────────────────────

        bool IsOpen { get; }

        void Open();
        void Close();

        // Async-friendly, but sync-compatible
        Task OpenAsync(CancellationToken ct = default);
        Task CloseAsync(CancellationToken ct = default);

        // ─────────────────────────────────────────────
        // Command-style execution
        // ─────────────────────────────────────────────

        int Execute(
            QueryBuilder query,
            object? parameters = null);

        Task<int> ExecuteAsync(
            QueryBuilder query,
            object? parameters = null,
            CancellationToken ct = default);
    }
}