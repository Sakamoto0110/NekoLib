using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NekoLib.Data
{
    public sealed class DbSession : IDisposable
    {
        public DbConnection Connection { get; }
        public DbTransaction? Transaction { get; private set; }

        private int _transactionDepth;
        private bool _disposed;
        private object? _affinityToken;

        /// <summary>
        /// Wraps an externally opened connection. The session is atomically
        /// affiliated with the first query context that uses it.
        /// </summary>
        public DbSession(DbConnection connection)
            : this(connection, null)
        {
        }

        internal DbSession(DbConnection connection, object? affinityToken)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _affinityToken = affinityToken;
        }

        public void BeginTransaction()
        {
            ThrowIfDisposed();

            if (_transactionDepth == 0)
                Transaction = Connection.BeginTransaction();

            _transactionDepth++;
        }

        /// <summary>
        /// Starts a transaction at the specified isolation level.
        /// This is useful for consistent reads spanning multiple operations,
        /// such as <see cref="IsolationLevel.Snapshot"/> or
        /// <see cref="IsolationLevel.RepeatableRead"/>. The isolation level is
        /// applied only when the provider transaction opens at depth zero;
        /// nested calls only increment the logical depth.
        /// </summary>
        public void BeginTransaction(IsolationLevel isolation)
        {
            ThrowIfDisposed();

            if (_transactionDepth == 0)
                Transaction = Connection.BeginTransaction(isolation);

            _transactionDepth++;
        }

        public void Commit()
        {
            ThrowIfDisposed();

            if (_transactionDepth == 0)
                throw new InvalidOperationException("No active transaction.");

            _transactionDepth--;

            if (_transactionDepth == 0)
            {
                try
                {
                    Transaction!.Commit();
                }
                finally
                {
                    try
                    {
                        Transaction?.Dispose();
                    }
                    finally
                    {
                        Transaction = null;
                    }
                }
            }
        }

        public void Rollback()
        {
            ThrowIfDisposed();

            if (Transaction == null)
                return;

            try
            {
                Transaction.Rollback();
            }
            finally
            {
                try
                {
                    Transaction.Dispose();
                }
                finally
                {
                    Transaction = null;
                    _transactionDepth = 0;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                if (Transaction != null)
                    Rollback();
            }
            finally
            {
                Connection.Dispose();
                _disposed = true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DbSession));
        }

        internal void ValidateFor(object affinityToken)
        {
            if (affinityToken == null)
                throw new ArgumentNullException(nameof(affinityToken));

            ThrowIfDisposed();
            if (Connection.State != ConnectionState.Open)
            {
                throw new InvalidOperationException(
                    "The session connection must be open before command creation.");
            }

            object? existing = Interlocked.CompareExchange(
                ref _affinityToken,
                affinityToken,
                null);
            if (existing != null && !ReferenceEquals(existing, affinityToken))
            {
                throw new InvalidOperationException(
                    "The session belongs to a different query execution context.");
            }
        }
    }

}
