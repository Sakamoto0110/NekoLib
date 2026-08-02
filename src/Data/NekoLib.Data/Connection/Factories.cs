#nullable enable
using NekoLib;
using System;
using System.Data.Common;

using System.Threading.Tasks;




namespace NekoLib.Data.Connection
{
    /// <summary>Controls whether a query context disposes its connection factory.</summary>
    public enum DbConnectionFactoryOwnership
    {
        /// <summary>The context owns and disposes the factory.</summary>
        ContextOwned = 0,

        /// <summary>The caller retains ownership of the factory.</summary>
        External = 1
    }

    /// <summary>
    /// Creates connections for the database gateway. Implementations must be
    /// stateless: each <see cref="IDbConnectionFactory.Create"/> call returns a
    /// new closed connection.
    /// </summary>
    public interface IDbConnectionFactory : IDisposable
    {
        /// <summary>
        /// Creates a new closed <see cref="DbConnection"/> instance.
        /// </summary>
        Task<DbConnection> Create();
    }

    /// <summary>
    /// Generic <see cref="IDbConnectionFactory"/> implementation that uses
    /// <see cref="Activator"/> with a connection-string constructor.
    /// <para/>
    /// This factory is deliberately stateless. It does not retain, close, or
    /// dispose created connections; the caller owns each returned connection.
    /// </summary>
    public class DbConnectionAbstractFactory<T> : IDbConnectionFactory where T : DbConnection
    {
        private readonly string _connectionString;

        public DbConnectionAbstractFactory(string connectionString)
        {
            if(connectionString is null) throw new ArgumentNullException(nameof(connectionString));
            _connectionString = connectionString;
        }

        public Task<DbConnection> Create()
        {
            var conn = (DbConnection?)Activator.CreateInstance(typeof(T), _connectionString);
            if(conn is null)
                throw new InvalidOperationException($"Failed to create connection of type {typeof(T).FullName}.");
            return Task.FromResult(conn);
        }

        public void Dispose()
        {
            // Stateless: nothing to dispose.
        }
    }

 

}
