#nullable enable
using System;
using NekoLib.Data.Connection;

namespace NekoLib.Data.Query
{
    public class QueryExecutionContext : IDisposable
    {
        private const string RedactedSql = "[SQL redacted]";
        private bool disposedValue;

        public event Action<DbQueryEventArgs>? OnSqlGenerated;
        public event Action<DbQueryEventArgs>? OnSqlDispatch;
        public event Action<DbQuerySuccessEventArgs>? OnSuccess;
        public event Action<DbQueryFailureEventArgs>? OnError;
        public IDbConnectionFactory ConnectionFactory { get; }
        public IDbQueryTranslator Translator { get; }
        public DatabaseGatewayOptions Options { get; }
        public QueryExecutionContext(IDbConnectionFactory connectionFactory, IDbQueryTranslator queryTranslator, DatabaseGatewayOptions? options = null)
        {
            ConnectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            Translator = queryTranslator ?? throw new ArgumentNullException(nameof(queryTranslator));
            Options = options ?? new DatabaseGatewayOptions();
            Options.Validate();
        }
        

        internal void RaiseSqlGenerated(string sql)
        {
            OnSqlGenerated?.Invoke(new DbQueryEventArgs(GetEventSql(sql), DbQueryEventType.SqlGenerated));
        }
        internal void RaiseSqlDispatch(string sql)=> OnSqlDispatch?.Invoke(new DbQueryEventArgs(GetEventSql(sql), DbQueryEventType.SqlDispatched));
        internal void RaiseSuccess(string sql, object? result = null)
        {
            object? eventResult = Options.IncludeCommandResultInSuccessEvents ? result : null;
            OnSuccess?.Invoke(new DbQuerySuccessEventArgs(GetEventSql(sql), eventResult));
        }
        internal void RaiseError(string sql, Exception ex)=> OnError?.Invoke(new DbQueryFailureEventArgs(GetEventSql(sql), ex));

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
                    ConnectionFactory?.Dispose();
                    // Quebra cadeias de referência (evita leaks por subscribers long-lived)
                    if(Options.ClearEventsOnContextDispose)
                    {
                        OnSqlGenerated = null;
                        OnSqlDispatch = null;
                        OnSuccess = null;
                        OnError = null;
                    }
                }              
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Não altere este código. Coloque o código de limpeza no método 'Dispose(bool disposing)'
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
    [Flags]
    public enum DbQueryEventType { SqlGenerated=1, SqlDispatched=2, Success=4, Error=8}
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

}
