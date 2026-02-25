#nullable enable
using NekoLib;
using System;
using System.Data.Common;

using System.Threading.Tasks;




namespace NekoLib.Data.Connection
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

 

}
