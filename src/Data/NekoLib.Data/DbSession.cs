using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NekoLib.Data
{
    public sealed class DbSession : IDisposable
    {
        public DbConnection Connection { get; }
        public DbTransaction? Transaction { get; private set; }

        private int _transactionDepth;
        private bool _rolledBack;
        private bool _disposed;

        public DbSession(DbConnection connection)
        {
            Connection = connection;
        }

        public void BeginTransaction()
        {
            ThrowIfDisposed();

            if (_rolledBack)
                throw new InvalidOperationException("Transaction already rolled back.");

            if (_transactionDepth == 0)
                Transaction = Connection.BeginTransaction();

            _transactionDepth++;
        }

        public void Commit()
        {
            ThrowIfDisposed();

            if (_transactionDepth == 0)
                throw new InvalidOperationException("No active transaction.");

            if (_rolledBack)
                throw new InvalidOperationException("Transaction already rolled back.");

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
                    _rolledBack = true;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                if (Transaction != null && !_rolledBack)
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
    }

}
