#nullable enable
using System;
using System.Data.Common;

using System.Threading.Tasks;

#if NET481
using System.Data.OleDb;
 
using NekoLib.Data.Internal.Gateway.Connection;

#endif
#if NETFRAMEWORK
using System.Data.SqlClient;
#else
using Microsoft.Data.SqlClient;
using NekoLib.Data.Internal.Gateway.Connection;

 
#endif

namespace NekoLib.Data.Internal.Gateway.Connection
{
    /// <summary>
    /// Abstração de fábrica de conexões para o DatabaseGateway.
    /// Implementações devem ser <b>stateless</b>: Create() sempre retorna uma NOVA conexão fechada.
    /// </summary>
    public interface IDbConnectionFactory : IDisposable
    {
        /// <summary>
        /// Cria uma nova instância de <see cref="DbConnection"/> ainda fechada.
        /// </summary>
        Task<DbConnection> Create();
    }

    /// <summary>
    /// Implementação genérica de <see cref="IDbConnectionFactory"/> utilizando <see cref="Activator"/>.
    /// <para/>
    /// ⚠️ Produção: esta fábrica é propositalmente <b>stateless</b>.
    /// Ela NÃO mantém referência e NÃO tenta fechar/dispensar conexões criadas.
    /// Quem chama é responsável por <c>Dispose()</c> da conexão.
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

#if NET481  


    /// <summary>
    /// Fábrica de conexões para Access / OleDb.
    /// </summary>
    public sealed class AccessConnectionFactory : DbConnectionAbstractFactory<OleDbConnection>
    {
        public AccessConnectionFactory(string connectionString) : base(connectionString) { }
    }
#endif
    /// <summary>
    /// Fábrica de conexões para SQL Server.
    /// <para/>
    /// NETFRAMEWORK: System.Data.SqlClient
    /// Modern: Microsoft.Data.SqlClient
    /// </summary>
    public sealed class SqlConnectionFactory : DbConnectionAbstractFactory<SqlConnection>
    {
        public SqlConnectionFactory(string connectionString) : base(connectionString) { }
    }

}
