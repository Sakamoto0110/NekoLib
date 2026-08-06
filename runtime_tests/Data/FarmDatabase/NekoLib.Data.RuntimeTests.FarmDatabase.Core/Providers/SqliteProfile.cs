#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NekoLib.Data.Connection;
using NekoLib.Data.Gateway;
using NekoLib.Data.Query;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.Core.Providers
{
    /// <summary>
    /// SQLite profile. Always available: the native payload travels with the app,
    /// so this is the provider that keeps the scenario runnable on any machine.
    /// </summary>
    public sealed class SqliteProfile : IFarmProviderProfile
    {
        public SqliteProfile(string databasePath)
        {
            DatabasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
        }

        public FarmProvider Provider => FarmProvider.Sqlite;

        public string DisplayName => "SQLite";

        public string DialectNotes =>
            "LIMIT n para limitar linhas - parâmetros nomeados @p1 - catálogo consultável via sqlite_master.";

        public string DatabasePath { get; }

        public string ConnectionString => "Data Source=" + DatabasePath;

        public ProviderAvailability Probe()
        {
            try
            {
                // Touching the connection type forces SQLitePCLRaw to resolve its
                // native provider. If the architecture-specific payload is missing
                // this throws here rather than at first query.
                using (var probe = new SqliteConnection("Data Source=:memory:"))
                {
                    probe.Open();
                }

                return ProviderAvailability.Available();
            }
            catch (Exception ex)
            {
                return ProviderAvailability.Unavailable(
                    "O nativo do SQLite não carregou: " + ex.Message,
                    "Verifique se o payload de SQLitePCLRaw foi copiado para a pasta de saída.");
            }
        }

        public void EnsureDatabaseFile()
        {
            string? dir = Path.GetDirectoryName(DatabasePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // SQLite materializes the file on first connect; no explicit creation.
            using (var conn = new SqliteConnection(ConnectionString))
            {
                conn.Open();
            }
        }

        public void DeleteDatabaseFile()
        {
            // Pooled handles keep the file locked, so drop them before deleting.
            SqliteConnection.ClearAllPools();
            if (File.Exists(DatabasePath))
                File.Delete(DatabasePath);
        }

        public IDbConnectionFactory CreateConnectionFactory() =>
            new DbConnectionAbstractFactory<SqliteConnection>(ConnectionString);

        public IDbQueryTranslator CreateTranslator() => new SqliteQueryTranslator();

        /// <summary>
        /// Bracket quoting, matching <see cref="AccessProfile"/>. SQLite accepts
        /// <c>[identifier]</c> for MS-compatibility, so both profiles quote the same
        /// way and the repositories never branch on dialect just to name a column.
        /// </summary>
        public string Quote(string identifier) => "[" + identifier + "]";

        public IReadOnlyList<string> SchemaDdl() => new[]
        {
            @"CREATE TABLE Roles (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                Title       TEXT    NOT NULL,
                BaseSalary  REAL    NOT NULL
              )",

            @"CREATE TABLE Employees (
                Id       INTEGER PRIMARY KEY AUTOINCREMENT,
                Name     TEXT    NOT NULL,
                Age      INTEGER NOT NULL,
                Cpf      TEXT    NOT NULL,
                Phone    TEXT    NOT NULL,
                RoleId   INTEGER NOT NULL
              )",

            @"CREATE TABLE Products (
                Id         INTEGER PRIMARY KEY AUTOINCREMENT,
                Name       TEXT    NOT NULL,
                Category   TEXT    NOT NULL,
                Unit       TEXT    NOT NULL,
                Quantity   INTEGER NOT NULL,
                UnitPrice  REAL    NOT NULL
              )",

            @"CREATE TABLE Animals (
                Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                Species   TEXT    NOT NULL,
                Tag       TEXT    NOT NULL,
                AgeYears  INTEGER NOT NULL,
                Gender    TEXT    NOT NULL,
                Notes     TEXT    NULL
              )",

            @"CREATE TABLE OperationLog (
                Id          INTEGER  PRIMARY KEY AUTOINCREMENT,
                OccurredAt  TEXT     NOT NULL,
                EntityKind  TEXT     NOT NULL,
                EntityId    INTEGER  NOT NULL,
                EntityName  TEXT     NOT NULL,
                Operation   TEXT     NOT NULL,
                Quantity    INTEGER  NOT NULL,
                Reason      TEXT     NULL
              )"
        };

        public async Task<IReadOnlyList<string>> ListTablesAsync(
            IDatabaseGateway gateway,
            DbSession session,
            CancellationToken ct)
        {
            var rows = await gateway.GetRaw(
                "SELECT name FROM sqlite_master " +
                "WHERE type = 'table' AND name NOT LIKE 'sqlite_%' " +
                "ORDER BY name",
                null,
                session,
                ct).ConfigureAwait(false);

            var tables = new List<string>(rows.Count);
            foreach (var row in rows)
                tables.Add(row["name"].ToString());

            return tables;
        }
    }
}
