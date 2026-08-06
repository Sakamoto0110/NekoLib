#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Data.Connection;
using NekoLib.Data.Gateway;
using NekoLib.Data.Query;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.Core.Providers
{
    /// <summary>
    /// Everything that varies per database engine, bundled into one object.
    /// <para/>
    /// This is deliberately the shape <c>NekoLib.Data</c> does not have today: the
    /// library asks callers to hand <see cref="QueryExecutionContext"/> a connection
    /// factory and a translator as two independent arguments, with nothing enforcing
    /// that they describe the same engine. Pairing them here is what keeps this
    /// scenario honest, and it is a live sketch of an <c>IDbProvider</c> seam.
    /// </summary>
    public interface IFarmProviderProfile
    {
        FarmProvider Provider { get; }

        /// <summary>Label shown in the UI.</summary>
        string DisplayName { get; }

        /// <summary>Short description of the engine's dialect quirks, shown in the UI.</summary>
        string DialectNotes { get; }

        /// <summary>Absolute path of the database file this profile operates on.</summary>
        string DatabasePath { get; }

        /// <summary>The connection string derived from <see cref="DatabasePath"/>.</summary>
        string ConnectionString { get; }

        /// <summary>
        /// Probes the machine for the driver this profile needs. SQLite is always
        /// available (the native payload ships with the app); Access depends on an
        /// ACE registration at the process bitness.
        /// </summary>
        ProviderAvailability Probe();

        /// <summary>
        /// Creates the physical database file when it does not exist yet. SQLite
        /// creates on first connect; Access needs an explicit catalog creation
        /// because OleDb has no <c>CREATE DATABASE</c>.
        /// </summary>
        void EnsureDatabaseFile();

        /// <summary>Deletes the database file so the next run reseeds from scratch.</summary>
        void DeleteDatabaseFile();

        IDbConnectionFactory CreateConnectionFactory();

        IDbQueryTranslator CreateTranslator();

        /// <summary>
        /// DDL statements that create the farm schema in this engine's dialect,
        /// in dependency order.
        /// </summary>
        IReadOnlyList<string> SchemaDdl();

        /// <summary>Wraps an identifier so reserved words survive.</summary>
        string Quote(string identifier);

        /// <summary>
        /// Returns the user tables in this database. The two engines need entirely
        /// different mechanisms: SQLite has a queryable catalog, Access only exposes
        /// its schema through the OleDb schema rowset.
        /// </summary>
        Task<IReadOnlyList<string>> ListTablesAsync(
            IDatabaseGateway gateway,
            DbSession session,
            CancellationToken ct);
    }
}
