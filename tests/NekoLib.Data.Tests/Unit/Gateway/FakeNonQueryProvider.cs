using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Data.Connection;

namespace NekoLib.Data.Tests.Unit.Gateway
{
    internal sealed class FakeNonQueryConnectionFactory : IDbConnectionFactory
    {
        private readonly Func<FakeNonQueryCommand> _commandFactory;

        public FakeNonQueryConnectionFactory(Func<FakeNonQueryCommand> commandFactory)
        {
            _commandFactory = commandFactory;
        }

        public FakeNonQueryConnection LastConnection { get; private set; }

        public Task<DbConnection> Create()
        {
            LastConnection = new FakeNonQueryConnection(_commandFactory);
            return Task.FromResult<DbConnection>(LastConnection);
        }

        public void Dispose()
        {
        }
    }

    internal sealed class FakeNonQueryConnection : DbConnection
    {
        private readonly Func<FakeNonQueryCommand> _commandFactory;
        private ConnectionState _state;

        public FakeNonQueryConnection(Func<FakeNonQueryCommand> commandFactory)
        {
            _commandFactory = commandFactory;
        }

        public override string ConnectionString { get; set; }
        public override string Database => "Fake";
        public override string DataSource => "Fake";
        public override string ServerVersion => "1";
        public override ConnectionState State => _state;

        public override void Open()
        {
            _state = ConnectionState.Open;
        }

        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Open();
            return Task.CompletedTask;
        }

        public override void Close()
        {
            _state = ConnectionState.Closed;
        }

        public override void ChangeDatabase(string databaseName)
        {
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            throw new NotSupportedException();
        }

        protected override DbCommand CreateDbCommand()
        {
            FakeNonQueryCommand command = _commandFactory();
            command.Connection = this;
            return command;
        }
    }

    internal sealed class FakeNonQueryCommand : DbCommand
    {
        private readonly FakeParameterCollection _parameters = new FakeParameterCollection();

        public int Result { get; set; }
        public Exception ExecuteException { get; set; }
        public override string CommandText { get; set; }
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection DbConnection { get; set; }
        protected override DbParameterCollection DbParameterCollection => _parameters;
        protected override DbTransaction DbTransaction { get; set; }

        public new DbConnection Connection
        {
            get => DbConnection;
            set => DbConnection = value;
        }

        public override void Cancel()
        {
        }

        public override int ExecuteNonQuery()
        {
            if (ExecuteException != null)
                throw ExecuteException;
            return Result;
        }

        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ExecuteException != null)
                return Task.FromException<int>(ExecuteException);
            return Task.FromResult(Result);
        }

        public override object ExecuteScalar()
        {
            throw new NotSupportedException();
        }

        public override void Prepare()
        {
        }

        protected override DbParameter CreateDbParameter()
        {
            return new FakeParameter();
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            throw new NotSupportedException();
        }
    }

    internal sealed class FakeParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; }
        public override bool IsNullable { get; set; }
        public override string ParameterName { get; set; }
        public override int Size { get; set; }
        public override string SourceColumn { get; set; }
        public override bool SourceColumnNullMapping { get; set; }
        public override object Value { get; set; }
        public override DataRowVersion SourceVersion { get; set; }

        public override void ResetDbType()
        {
        }
    }

    internal sealed class FakeParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _items = new List<DbParameter>();

        public override int Count => _items.Count;
        public override object SyncRoot => ((ICollection)_items).SyncRoot;
        public override int Add(object value)
        {
            _items.Add((DbParameter)value);
            return _items.Count - 1;
        }
        public override void AddRange(Array values)
        {
            foreach (object value in values)
                Add(value);
        }
        public override void Clear() => _items.Clear();
        public override bool Contains(object value) => _items.Contains((DbParameter)value);
        public override bool Contains(string value) => IndexOf(value) >= 0;
        public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
        public override IEnumerator GetEnumerator() => _items.GetEnumerator();
        public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);
        public override int IndexOf(string parameterName) =>
            _items.FindIndex(item => string.Equals(item.ParameterName, parameterName, StringComparison.Ordinal));
        public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);
        public override void Remove(object value) => _items.Remove((DbParameter)value);
        public override void RemoveAt(int index) => _items.RemoveAt(index);
        public override void RemoveAt(string parameterName) => RemoveAt(IndexOf(parameterName));
        protected override DbParameter GetParameter(int index) => _items[index];
        protected override DbParameter GetParameter(string parameterName) => _items[IndexOf(parameterName)];
        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value)
        {
            int index = IndexOf(parameterName);
            if (index < 0)
                _items.Add(value);
            else
                _items[index] = value;
        }
    }
}
