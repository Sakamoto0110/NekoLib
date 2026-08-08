#nullable enable
using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace NekoLib.Data.RuntimeTests.SqlServer.Workload
{
    /// <summary>
    /// An exclusive row lock held by a control connection, used to keep a
    /// gateway command running without finishing.
    /// <para/>
    /// <c>WAITFOR DELAY</c> is the simpler way to hold a batch open, but it can
    /// only be written into raw SQL, and <c>QueryBuilder</c> has no clause for
    /// it. A lock works for any query at all, and it is what a stalled command
    /// looks like in production: the statement is perfectly ordinary and the
    /// server is simply not able to answer it yet.
    /// <para/>
    /// The lock is taken outside the gateway on purpose. Injecting the stall
    /// through the library under test would make the measurement circular.
    /// </summary>
    internal sealed class BlockingLock
    {
        /// <summary>The row every blocking check contends on.</summary>
        public const int LockedPartId = 1;

        private readonly SqlConnection _connection;
        private readonly SqlTransaction _transaction;
        private bool _released;

        private BlockingLock(SqlConnection connection, SqlTransaction transaction)
        {
            _connection = connection;
            _transaction = transaction;
        }

        public static async Task<BlockingLock> TakeAsync(PhaseContext context)
        {
            string connectionString = context.Endpoint.BuildConnectionString(
                context.DatabaseName,
                pooling: false,
                connectTimeoutSeconds: 60,
                applicationName: "NekoLib.E4-SQL.lock");

            SqlConnection connection = new SqlConnection(connectionString);
            await connection.OpenAsync(context.Ct).ConfigureAwait(false);

            SqlTransaction transaction = connection.BeginTransaction();

            try
            {
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    // A real modification, because a lock alone is not enough.
                    //
                    // Measured against this exact server, three ways: an
                    // XLOCK+ROWLOCK+HOLDLOCK hint on a SELECT does appear in
                    // sys.dm_tran_locks as X on the row's key, and a reader in
                    // READ COMMITTED still sails straight past it and returns
                    // in about two milliseconds. Locking the row without
                    // changing it leaves nothing for the reader to wait for. An
                    // UPDATE that genuinely writes blocks the same reader until
                    // its transaction ends, and so, for what it is worth, does
                    // an UPDATE that writes a column back to its own value.
                    //
                    // The value is put back by the rollback in ReleaseAsync,
                    // which every path takes, so the seeded data is unchanged
                    // and the check can run repeatedly.
                    command.CommandText =
                        "UPDATE Part SET Quantity = Quantity + 1 WHERE Id = " +
                        LockedPartId.ToString(System.Globalization.CultureInfo.InvariantCulture);

                    int affected = await command.ExecuteNonQueryAsync(context.Ct).ConfigureAwait(false);
                    if (affected != 1)
                    {
                        throw new InvalidOperationException(
                            "the blocking lock expected to modify exactly one row, modified " + affected);
                    }
                }

                return new BlockingLock(connection, transaction);
            }
            catch
            {
                try { transaction.Rollback(); } catch { /* the connection is going away anyway */ }
                transaction.Dispose();
                connection.Dispose();
                throw;
            }
        }

        public Task ReleaseAsync()
        {
            if (_released) return CompletedTask;
            _released = true;

            try
            {
                _transaction.Rollback();
            }
            catch (Exception)
            {
                // A rolled-back or broken transaction is already released; the
                // connection dispose below is what actually frees the lock.
            }
            finally
            {
                _transaction.Dispose();
                _connection.Dispose();
            }

            return CompletedTask;
        }

        private static Task CompletedTask
        {
            get
            {
#if NET6_0_OR_GREATER
                return Task.CompletedTask;
#else
                return Task.FromResult(0);
#endif
            }
        }
    }
}
