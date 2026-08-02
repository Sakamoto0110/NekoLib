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
        private readonly bool _useSynchronousOpenFallback;
        private readonly Action _beforeOpenAsyncNotSupported;

        public FakeNonQueryConnectionFactory(
            Func<FakeNonQueryCommand> commandFactory,
            bool useSynchronousOpenFallback = false,
            Action beforeOpenAsyncNotSupported = null)
        {
            _commandFactory = commandFactory;
            _useSynchronousOpenFallback = useSynchronousOpenFallback;
            _beforeOpenAsyncNotSupported = beforeOpenAsyncNotSupported;
        }

        public FakeNonQueryConnection LastConnection { get; private set; }
        public int CreateCalls { get; private set; }

        public Task<DbConnection> Create()
        {
            CreateCalls++;
            LastConnection = new FakeNonQueryConnection(
                _commandFactory,
                _useSynchronousOpenFallback,
                _beforeOpenAsyncNotSupported);
            return Task.FromResult<DbConnection>(LastConnection);
        }

        public void Dispose()
        {
        }
    }

    internal sealed class FakeNonQueryConnection : DbConnection
    {
        private readonly Func<FakeNonQueryCommand> _commandFactory;
        private readonly bool _useSynchronousOpenFallback;
        private readonly Action _beforeOpenAsyncNotSupported;
        private ConnectionState _state;

        public FakeNonQueryConnection(
            Func<FakeNonQueryCommand> commandFactory,
            bool useSynchronousOpenFallback = false,
            Action beforeOpenAsyncNotSupported = null)
        {
            _commandFactory = commandFactory;
            _useSynchronousOpenFallback = useSynchronousOpenFallback;
            _beforeOpenAsyncNotSupported = beforeOpenAsyncNotSupported;
        }

        public override string ConnectionString { get; set; }
        public override string Database => "Fake";
        public override string DataSource => "Fake";
        public override string ServerVersion => "1";
        public override ConnectionState State => _state;
        public bool WasDisposed { get; private set; }
        public FakeNonQueryCommand LastCommand { get; private set; }
        public int OpenCalls { get; private set; }
        public int OpenAsyncCalls { get; private set; }

        public override void Open()
        {
            OpenCalls++;
            _state = ConnectionState.Open;
        }

        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            OpenAsyncCalls++;
            if (_useSynchronousOpenFallback)
            {
                _beforeOpenAsyncNotSupported?.Invoke();
                return Task.FromException(new NotSupportedException("Async open is not supported."));
            }
            cancellationToken.ThrowIfCancellationRequested();
            _state = ConnectionState.Open;
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
            LastCommand = command;
            return command;
        }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            _state = ConnectionState.Closed;
            base.Dispose(disposing);
        }
    }

    internal sealed class FakeNonQueryCommand : DbCommand
    {
        private readonly FakeParameterCollection _parameters = new FakeParameterCollection();

        public int Result { get; set; }
        public Exception ExecuteException { get; set; }
        public DbDataReader Reader { get; set; }
        public bool WasDisposed { get; private set; }
        public bool UseSynchronousNonQueryFallback { get; set; }
        public bool UseSynchronousReaderFallback { get; set; }
        public Action BeforeAsyncNotSupported { get; set; }
        public int ExecuteNonQueryCalls { get; private set; }
        public int ExecuteNonQueryAsyncCalls { get; private set; }
        public int ExecuteReaderCalls { get; private set; }
        public int ExecuteReaderAsyncCalls { get; private set; }
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
            ExecuteNonQueryCalls++;
            if (ExecuteException != null)
                throw ExecuteException;
            return Result;
        }

        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            ExecuteNonQueryAsyncCalls++;
            if (UseSynchronousNonQueryFallback)
            {
                BeforeAsyncNotSupported?.Invoke();
                return Task.FromException<int>(
                    new NotSupportedException("Async non-query execution is not supported."));
            }
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
            ExecuteReaderCalls++;
            if (ExecuteException != null)
                throw ExecuteException;
            if (Reader == null)
                throw new InvalidOperationException("No fake reader was configured.");
            return Reader;
        }

        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(
            CommandBehavior behavior,
            CancellationToken cancellationToken)
        {
            ExecuteReaderAsyncCalls++;
            if (UseSynchronousReaderFallback)
            {
                BeforeAsyncNotSupported?.Invoke();
                return Task.FromException<DbDataReader>(
                    new NotSupportedException("Async reader execution is not supported."));
            }
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return Task.FromResult(ExecuteDbDataReader(behavior));
            }
            catch (Exception ex)
            {
                return Task.FromException<DbDataReader>(ex);
            }
        }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }

    internal sealed class FakeDataReader : DbDataReader
    {
        private readonly string[] _names;
        private readonly Type[] _types;
        private readonly object[][] _rows;
        private int _rowIndex = -1;
        private bool _closed;

        public FakeDataReader(string[] names, Type[] types, params object[][] rows)
        {
            if (names == null) throw new ArgumentNullException(nameof(names));
            if (types == null) throw new ArgumentNullException(nameof(types));
            if (names.Length != types.Length)
                throw new ArgumentException("Fake schema names and types must have equal lengths.");

            _names = names;
            _types = types;
            _rows = rows ?? Array.Empty<object[]>();
            for (int index = 0; index < _rows.Length; index++)
            {
                if (_rows[index].Length != _names.Length)
                    throw new ArgumentException("Every fake row must match the schema width.");
            }
        }

        public bool WasDisposed { get; private set; }
        public bool UseSynchronousReadFallback { get; set; }
        public Action BeforeAsyncNotSupported { get; set; }
        public int ReadCalls { get; private set; }
        public int ReadAsyncCalls { get; private set; }
        public override int Depth => 0;
        public override int FieldCount => _names.Length;
        public override bool HasRows => _rows.Length > 0;
        public override bool IsClosed => _closed;
        public override int RecordsAffected => -1;
        public override object this[int ordinal] => GetValue(ordinal);
        public override object this[string name] => GetValue(GetOrdinal(name));

        public override bool Read()
        {
            ReadCalls++;
            if (_closed)
                throw new InvalidOperationException("Reader is closed.");
            if (_rowIndex + 1 >= _rows.Length)
                return false;
            _rowIndex++;
            return true;
        }

        public override Task<bool> ReadAsync(CancellationToken cancellationToken)
        {
            ReadAsyncCalls++;
            if (UseSynchronousReadFallback)
            {
                BeforeAsyncNotSupported?.Invoke();
                return Task.FromException<bool>(
                    new NotSupportedException("Async row reads are not supported."));
            }
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Read());
        }

        public override bool NextResult() => false;
        public override string GetName(int ordinal) => _names[ordinal];
        public override string GetDataTypeName(int ordinal) => _types[ordinal].Name;
        public override Type GetFieldType(int ordinal) => _types[ordinal];
        public override object GetValue(int ordinal)
        {
            if (_rowIndex < 0 || _rowIndex >= _rows.Length)
                throw new InvalidOperationException("Reader is not positioned on a row.");
            return _rows[_rowIndex][ordinal];
        }
        public override int GetValues(object[] values)
        {
            int count = Math.Min(values.Length, FieldCount);
            for (int index = 0; index < count; index++)
                values[index] = GetValue(index);
            return count;
        }
        public override int GetOrdinal(string name)
        {
            for (int index = 0; index < _names.Length; index++)
            {
                if (string.Equals(_names[index], name, StringComparison.OrdinalIgnoreCase))
                    return index;
            }
            throw new IndexOutOfRangeException(name);
        }
        public override bool IsDBNull(int ordinal) => GetValue(ordinal) is DBNull;
        public override bool GetBoolean(int ordinal) => (bool)GetValue(ordinal);
        public override byte GetByte(int ordinal) => (byte)GetValue(ordinal);
        public override char GetChar(int ordinal) => (char)GetValue(ordinal);
        public override DateTime GetDateTime(int ordinal) => (DateTime)GetValue(ordinal);
        public override decimal GetDecimal(int ordinal) => (decimal)GetValue(ordinal);
        public override double GetDouble(int ordinal) => (double)GetValue(ordinal);
        public override float GetFloat(int ordinal) => (float)GetValue(ordinal);
        public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);
        public override short GetInt16(int ordinal) => (short)GetValue(ordinal);
        public override int GetInt32(int ordinal) => (int)GetValue(ordinal);
        public override long GetInt64(int ordinal) => (long)GetValue(ordinal);
        public override string GetString(int ordinal) => (string)GetValue(ordinal);
        public override long GetBytes(
            int ordinal,
            long dataOffset,
            byte[] buffer,
            int bufferOffset,
            int length)
        {
            byte[] source = (byte[])GetValue(ordinal);
            if (buffer == null)
                return source.Length;
            int count = Math.Min(length, source.Length - (int)dataOffset);
            Array.Copy(source, (int)dataOffset, buffer, bufferOffset, count);
            return count;
        }
        public override long GetChars(
            int ordinal,
            long dataOffset,
            char[] buffer,
            int bufferOffset,
            int length)
        {
            char[] source = GetString(ordinal).ToCharArray();
            if (buffer == null)
                return source.Length;
            int count = Math.Min(length, source.Length - (int)dataOffset);
            Array.Copy(source, (int)dataOffset, buffer, bufferOffset, count);
            return count;
        }
        public override IEnumerator GetEnumerator() => new DbEnumerator(this, false);
        public override void Close()
        {
            _closed = true;
        }
        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            _closed = true;
            base.Dispose(disposing);
        }
    }

    internal sealed class FakeParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; }
        public override bool IsNullable { get; set; }
        public override string ParameterName { get; set; }
        public override int Size { get; set; }
        public override byte Precision { get; set; }
        public override byte Scale { get; set; }
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
