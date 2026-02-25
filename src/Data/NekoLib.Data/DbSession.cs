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

        public DbSession(DbConnection connection)
        {
            Connection = connection;
        }

        public void BeginTransaction()
        {
            if (_rolledBack)
                throw new InvalidOperationException("Transaction already rolled back.");

            if (_transactionDepth == 0)
                Transaction = Connection.BeginTransaction();

            _transactionDepth++;
        }

        public void Commit()
        {
            if (_transactionDepth == 0)
                throw new InvalidOperationException("No active transaction.");

            if (_rolledBack)
                throw new InvalidOperationException("Transaction already rolled back.");

            _transactionDepth--;

            if (_transactionDepth == 0)
            {
                Transaction!.Commit();
                Transaction.Dispose();
                Transaction = null;
            }
        }

        public void Rollback()
        {
            if (Transaction == null)
                return;

            Transaction.Rollback();
            Transaction.Dispose();
            Transaction = null;

            _transactionDepth = 0;
            _rolledBack = true;
        }

        public void Dispose()
        {
            if (Transaction != null && !_rolledBack)
                Rollback();

            Connection.Dispose();
        }
    }

}