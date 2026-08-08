#nullable enable
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using NekoLib.Data.Connection;

namespace NekoLib.Data.RuntimeTests.SqlServer.Server
{
    /// <summary>
    /// The scenario's <see cref="IDbConnectionFactory"/>: the one place where a
    /// concrete provider type meets the gateway.
    /// <para/>
    /// This is what keeps <c>Microsoft.Data.SqlClient</c> out of
    /// <c>NekoLib.Data</c>. The gateway receives a closed
    /// <see cref="DbConnection"/> and never learns which provider produced it,
    /// which is the seam the whole Data module is built around and the reason
    /// this scenario needs no library change to reach SQL Server at all.
    /// <para/>
    /// It also counts. A factory that records how many connections it was asked
    /// for, and how many times it was disposed, turns two otherwise
    /// unobservable claims into measurements: whether pooling is really
    /// reusing, and whether the query context honoured
    /// <see cref="DbConnectionFactoryOwnership"/>.
    /// </summary>
    internal sealed class SqlServerConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;
        private long _created;
        private int _disposeCount;

        public SqlServerConnectionFactory(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        /// <summary>How many closed connections the gateway asked this factory to build.</summary>
        public long Created => Interlocked.Read(ref _created);

        /// <summary>
        /// How many times the factory was disposed. Zero after disposing an
        /// externally owned context; one after disposing a context that owns it.
        /// </summary>
        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public Task<DbConnection> Create()
        {
            Interlocked.Increment(ref _created);
            return Task.FromResult<DbConnection>(new SqlConnection(_connectionString));
        }

        public void Dispose()
        {
            // Stateless by contract: nothing is held, so this only records that
            // the call happened. Counting it is the point.
            Interlocked.Increment(ref _disposeCount);
        }

        /// <summary>
        /// Empties the provider's pool for this connection string.
        /// <para/>
        /// Pool control belongs to the scenario and to the provider, never to
        /// <c>NekoLib.Data</c>. It is used only where the specification allows
        /// it: cleanup, and proving that a stale pooled connection is the
        /// caller's problem to solve after a transport loss.
        /// </summary>
        public void ClearProviderPool()
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                SqlConnection.ClearPool(connection);
            }
        }
    }
}
