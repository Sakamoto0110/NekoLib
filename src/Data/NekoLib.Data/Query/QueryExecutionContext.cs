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
    public sealed class QueryExecutionContext : IDisposable
    {
        private const string RedactedSql = "[SQL redacted]";
        private readonly object _observerFailureSync = new object();
        private readonly Queue<DbQueryObserverFailure> _observerFailures =
            new Queue<DbQueryObserverFailure>();
        private long _observerFailureSequence;
        private readonly object _sessionAffinityToken = new object();
        private bool disposedValue;

        /// <summary>Occurs after provider SQL is generated and before a connection is opened.</summary>
        public event Action<DbQueryEventArgs>? OnSqlGenerated;
        /// <summary>Occurs immediately before provider execution.</summary>
        public event Action<DbQueryEventArgs>? OnSqlDispatch;
        /// <summary>
        /// Occurs after successful execution. Streaming operations defer this
        /// notification until owned resources have been cleaned up.
        /// </summary>
        public event Action<DbQuerySuccessEventArgs>? OnSuccess;
        /// <summary>Occurs when execution or cleanup fails.</summary>
        public event Action<DbQueryFailureEventArgs>? OnError;

        /// <summary>
        /// Reports exactly one terminal outcome for each stream enumeration
        /// that begins execution, after its owned resources are released.
        /// </summary>
        public event Action<DbQueryStreamTerminalEventArgs>? OnStreamTerminal;
        /// <summary>Gets the factory used to create closed connections for owned operations.</summary>
        public IDbConnectionFactory ConnectionFactory { get; }
        /// <summary>Gets the synchronous provider-specific SQL translator.</summary>
        public IDbQueryTranslator Translator { get; }
        /// <summary>Gets the validated behavior and adaptation options.</summary>
        public DatabaseGatewayOptions Options { get; }
        /// <summary>Gets whether this context disposes the supplied factory.</summary>
        public DbConnectionFactoryOwnership ConnectionFactoryOwnership { get; }
        internal object SessionAffinityToken => _sessionAffinityToken;

        /// <summary>Creates an execution context from explicit connection and translation policies.</summary>
        /// <param name="connectionFactory">A factory that returns a new closed connection per call.</param>
        /// <param name="queryTranslator">The synchronous SQL-shaping translator.</param>
        /// <param name="options">Optional context behavior; defaults are used when null.</param>
        /// <param name="connectionFactoryOwnership">Whether the context disposes the supplied factory.</param>
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

        private void Dispose(bool disposing)
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

        /// <summary>
        /// Disposes a context-owned factory, optionally clears subscribers, and
        /// releases retained observer-failure evidence.
        /// </summary>
        public void Dispose()
        {
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
        /// <summary>Creates a value-free record of an isolated observer failure.</summary>
        /// <param name="sequence">The context-local monotonic sequence number.</param>
        /// <param name="eventType">The notification being delivered.</param>
        /// <param name="exception">The subscriber exception.</param>
        public DbQueryObserverFailure(
            long sequence,
            DbQueryEventType eventType,
            Exception exception)
        {
            Sequence = sequence;
            EventType = eventType;
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        /// <summary>Gets the context-local monotonic sequence number.</summary>
        public long Sequence { get; }
        /// <summary>Gets the notification type that failed.</summary>
        public DbQueryEventType EventType { get; }
        /// <summary>Gets the subscriber exception.</summary>
        public Exception Exception { get; }
    }

    /// <summary>Identifies synchronous query lifecycle notifications.</summary>
    [Flags]
    public enum DbQueryEventType
    {
        /// <summary>Provider SQL was generated.</summary>
        SqlGenerated = 1,
        /// <summary>The command was about to be dispatched.</summary>
        SqlDispatched = 2,
        /// <summary>The command completed successfully.</summary>
        Success = 4,
        /// <summary>The command or cleanup failed.</summary>
        Error = 8,
        /// <summary>A streamed enumeration reached its terminal outcome.</summary>
        StreamTerminal = 16
    }

    /// <summary>Identifies the terminal outcome of one started stream enumeration.</summary>
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
    /// <summary>Describes one synchronous query lifecycle notification.</summary>
    public class DbQueryEventArgs : EventArgs
    {
        /// <summary>Gets provider SQL or the redaction marker configured by the context.</summary>
        public string RawSqlQuery { get; }
        /// <summary>Gets the lifecycle notification type.</summary>
        public DbQueryEventType EventType { get; }
        /// <summary>Creates query event data.</summary>
        /// <param name="sql">Provider SQL or the configured redaction marker.</param>
        /// <param name="type">The lifecycle notification type.</param>
        public DbQueryEventArgs(string sql, DbQueryEventType type = DbQueryEventType.SqlGenerated)
        {
            EventType = type;
            RawSqlQuery = sql;
        }

    }
    /// <summary>Describes successful query execution.</summary>
    public class DbQuerySuccessEventArgs : DbQueryEventArgs
    {
        /// <summary>Gets the optional command result when explicitly enabled.</summary>
        public object? Result { get; }
        /// <summary>Creates a success notification without a command result.</summary>
        /// <param name="sql">Provider SQL or the configured redaction marker.</param>
        public DbQuerySuccessEventArgs(string sql) : base(sql, DbQueryEventType.SqlDispatched | DbQueryEventType.Success)
        { }
        /// <summary>Creates a success notification with an optional command result.</summary>
        /// <param name="sql">Provider SQL or the configured redaction marker.</param>
        /// <param name="result">The result permitted by context options.</param>
        public DbQuerySuccessEventArgs(string sql, object? result) : base(sql, DbQueryEventType.SqlDispatched | DbQueryEventType.Success)
        {
            Result = result;
        }
    }
    /// <summary>Describes failed query execution or cleanup.</summary>
    public class DbQueryFailureEventArgs : DbQueryEventArgs
    {
        /// <summary>Gets the authoritative execution or cleanup exception.</summary>
        public Exception Ex { get; }

        /// <summary>Creates a failure notification.</summary>
        /// <param name="sql">Provider SQL or the configured redaction marker.</param>
        /// <param name="ex">The authoritative failure.</param>
        public DbQueryFailureEventArgs(string sql, Exception ex) : base(sql, DbQueryEventType.SqlDispatched | DbQueryEventType.Error)
        {
            Ex = ex;
        }
       

    }

    /// <summary>Describes the terminal state reported after stream cleanup.</summary>
    public sealed class DbQueryStreamTerminalEventArgs : DbQueryEventArgs
    {
        /// <summary>Creates a stream terminal notification.</summary>
        /// <param name="sql">Provider SQL or the configured redaction marker.</param>
        /// <param name="outcome">The terminal outcome.</param>
        /// <param name="exception">The failure or cancellation exception, if applicable.</param>
        public DbQueryStreamTerminalEventArgs(
            string sql,
            DbQueryStreamOutcome outcome,
            Exception? exception)
            : base(sql, DbQueryEventType.StreamTerminal)
        {
            Outcome = outcome;
            Exception = exception;
        }

        /// <summary>Gets the terminal outcome.</summary>
        public DbQueryStreamOutcome Outcome { get; }
        /// <summary>Gets the failure or cancellation exception, if applicable.</summary>
        public Exception? Exception { get; }
    }

}
