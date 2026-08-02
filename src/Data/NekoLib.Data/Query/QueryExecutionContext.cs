#nullable enable
using System;
using System.Collections.Generic;
using NekoLib.Data.Connection;

namespace NekoLib.Data.Query
{
    /// <summary>
    /// Owns connection and translation policy plus ordered query-lifecycle
    /// notifications for one gateway context.
    /// </summary>
    /// <remarks>
    /// Notifications run synchronously in subscription order, so subscriber
    /// latency remains part of the database call. Subscriber exceptions are
    /// isolated from database outcomes and retained only in the bounded
    /// observer-failure snapshot.
    /// </remarks>
    public class QueryExecutionContext : IDisposable
    {
        private const string RedactedSql = "[SQL redacted]";
        private readonly object _observerFailureSync = new object();
        private readonly Queue<DbQueryObserverFailure> _observerFailures =
            new Queue<DbQueryObserverFailure>();
        private long _observerFailureSequence;
        private readonly object _sessionAffinityToken = new object();
        private bool disposedValue;

        public event Action<DbQueryEventArgs>? OnSqlGenerated;
        public event Action<DbQueryEventArgs>? OnSqlDispatch;
        public event Action<DbQuerySuccessEventArgs>? OnSuccess;
        public event Action<DbQueryFailureEventArgs>? OnError;

        /// <summary>
        /// Reports exactly one terminal outcome for each stream enumeration
        /// that begins execution, after its owned resources are released.
        /// </summary>
        public event Action<DbQueryStreamTerminalEventArgs>? OnStreamTerminal;
        public IDbConnectionFactory ConnectionFactory { get; }
        public IDbQueryTranslator Translator { get; }
        public DatabaseGatewayOptions Options { get; }
        public DbConnectionFactoryOwnership ConnectionFactoryOwnership { get; }
        internal object SessionAffinityToken => _sessionAffinityToken;

        public QueryExecutionContext(
            IDbConnectionFactory connectionFactory,
            IDbQueryTranslator queryTranslator,
            DatabaseGatewayOptions? options = null,
            DbConnectionFactoryOwnership connectionFactoryOwnership =
                DbConnectionFactoryOwnership.ContextOwned)
        {
            ConnectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            Translator = queryTranslator ?? throw new ArgumentNullException(nameof(queryTranslator));
            Options = options ?? new DatabaseGatewayOptions();
            if (!Enum.IsDefined(
                typeof(DbConnectionFactoryOwnership),
                connectionFactoryOwnership))
            {
                throw new ArgumentOutOfRangeException(nameof(connectionFactoryOwnership));
            }
            ConnectionFactoryOwnership = connectionFactoryOwnership;
            Options.Validate();
        }
        

        /// <summary>
        /// Returns a bounded snapshot of recent query-observer failures. The
        /// snapshot never contains SQL text or command results.
        /// </summary>
        public IReadOnlyList<DbQueryObserverFailure> GetObserverFailures()
        {
            lock (_observerFailureSync)
            {
                return _observerFailures.ToArray();
            }
        }

        internal void RaiseSqlGenerated(string sql)
        {
            Notify(
                OnSqlGenerated,
                new DbQueryEventArgs(GetEventSql(sql), DbQueryEventType.SqlGenerated));
        }
        internal void RaiseSqlDispatch(string sql)
        {
            Notify(
                OnSqlDispatch,
                new DbQueryEventArgs(GetEventSql(sql), DbQueryEventType.SqlDispatched));
        }
        internal void RaiseSuccess(string sql, object? result = null)
        {
            object? eventResult = Options.IncludeCommandResultInSuccessEvents ? result : null;
            Notify(
                OnSuccess,
                new DbQuerySuccessEventArgs(GetEventSql(sql), eventResult));
        }
        internal void RaiseError(string sql, Exception ex)
        {
            Notify(
                OnError,
                new DbQueryFailureEventArgs(GetEventSql(sql), ex));
        }
        internal void RaiseStreamTerminal(
            string sql,
            DbQueryStreamOutcome outcome,
            Exception? exception)
        {
            Notify(
                OnStreamTerminal,
                new DbQueryStreamTerminalEventArgs(
                    GetEventSql(sql),
                    outcome,
                    exception));
        }

        private void Notify<TEventArgs>(Action<TEventArgs>? handlers, TEventArgs args)
            where TEventArgs : DbQueryEventArgs
        {
            if (handlers == null)
                return;

            Delegate[] subscribers = handlers.GetInvocationList();
            for (int i = 0; i < subscribers.Length; i++)
            {
                try
                {
                    ((Action<TEventArgs>)subscribers[i])(args);
                }
                catch (Exception ex)
                {
                    CaptureObserverFailure(args.EventType, ex);
                }
            }
        }

        private void CaptureObserverFailure(DbQueryEventType eventType, Exception exception)
        {
            // Observer-failure capture is deliberately non-recursive and must
            // never alter the authoritative database outcome.
            try
            {
                lock (_observerFailureSync)
                {
                    _observerFailureSequence++;
                    _observerFailures.Enqueue(new DbQueryObserverFailure(
                        _observerFailureSequence,
                        eventType,
                        exception));

                    while (_observerFailures.Count > Options.MaxObserverFailures)
                        _observerFailures.Dequeue();
                }
            }
            catch
            {
            }
        }

        private string GetEventSql(string sql)
        {
            return Options.EmitRawSqlInEvents ? sql : RedactedSql;
        }

        protected virtual void Dispose(bool disposing)
        {
            if(!disposedValue)
            {
                if(disposing)
                {
                    if (ConnectionFactoryOwnership == DbConnectionFactoryOwnership.ContextOwned)
                        ConnectionFactory.Dispose();
                    // Break subscriber reference chains held by long-lived publishers.
                    if(Options.ClearEventsOnContextDispose)
                    {
                        OnSqlGenerated = null;
                        OnSqlDispatch = null;
                        OnSuccess = null;
                        OnError = null;
                        OnStreamTerminal = null;
                    }

                    lock (_observerFailureSync)
                    {
                        _observerFailures.Clear();
                    }
                }              
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Keep cleanup in Dispose(bool) so derived contexts preserve the pattern.
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Describes a query observer that failed while receiving a synchronous
    /// notification. SQL and command results are intentionally excluded.
    /// </summary>
    public sealed class DbQueryObserverFailure
    {
        public DbQueryObserverFailure(
            long sequence,
            DbQueryEventType eventType,
            Exception exception)
        {
            Sequence = sequence;
            EventType = eventType;
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        public long Sequence { get; }
        public DbQueryEventType EventType { get; }
        public Exception Exception { get; }
    }

    [Flags]
    public enum DbQueryEventType
    {
        SqlGenerated = 1,
        SqlDispatched = 2,
        Success = 4,
        Error = 8,
        StreamTerminal = 16
    }

    public enum DbQueryStreamOutcome
    {
        /// <summary>The provider was exhausted and cleanup succeeded.</summary>
        Completed = 0,

        /// <summary>Setup, reading, mapping, or cleanup failed.</summary>
        Failed = 1,

        /// <summary>The stream observed cancellation.</summary>
        Cancelled = 2,

        /// <summary>The consumer disposed an active stream before exhaustion.</summary>
        DisposedBeforeCompletion = 3
    }
    public class DbQueryEventArgs : EventArgs
    {
        public string RawSqlQuery { get; }
        public DbQueryEventType EventType { get; }
        public DbQueryEventArgs(string sql, DbQueryEventType type = DbQueryEventType.SqlGenerated)
        {
            EventType = type;
            RawSqlQuery = sql;
        }

    }
    public class DbQuerySuccessEventArgs : DbQueryEventArgs
    {
        public object? Result { get; }
        public DbQuerySuccessEventArgs(string sql) : base(sql, DbQueryEventType.SqlDispatched | DbQueryEventType.Success)
        { }
        public DbQuerySuccessEventArgs(string sql, object? result) : base(sql, DbQueryEventType.SqlDispatched | DbQueryEventType.Success)
        {
            Result = result;
        }
    }
    public class DbQueryFailureEventArgs : DbQueryEventArgs
    {
        public Exception Ex { get; }

        public DbQueryFailureEventArgs(string sql, Exception ex) : base(sql, DbQueryEventType.SqlDispatched | DbQueryEventType.Error)
        {
            Ex = ex;
        }
       

    }

    public sealed class DbQueryStreamTerminalEventArgs : DbQueryEventArgs
    {
        public DbQueryStreamTerminalEventArgs(
            string sql,
            DbQueryStreamOutcome outcome,
            Exception? exception)
            : base(sql, DbQueryEventType.StreamTerminal)
        {
            Outcome = outcome;
            Exception = exception;
        }

        public DbQueryStreamOutcome Outcome { get; }
        public Exception? Exception { get; }
    }

}
