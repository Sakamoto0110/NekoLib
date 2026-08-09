#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Data.Gateway;
using NekoLib.Data.Query;
using NekoLib.Data.RuntimeTests.SqlServer.Faults;
using NekoLib.RuntimeTests.Harness.Faults;

namespace NekoLib.Data.RuntimeTests.SqlServer.Schema
{
    /// <summary>
    /// Creates and fills the scenario's own tables.
    /// <para/>
    /// Nothing here depends on a tracked fixture: the schema is DDL in this
    /// file and every row is derived from the run's integer seed, so two runs
    /// of the same seed hold the same data and a digest comparison is
    /// meaningful. That is also what makes schema recreation a recovery step
    /// rather than a restore.
    /// <para/>
    /// Identity columns are used only where the scenario never needs the
    /// generated value. Warehouse and Part carry explicit keys because the
    /// gateway's <c>Insert</c> reports affected rows and nothing else, and a
    /// seed that had to read its own keys back would be neither deterministic
    /// nor a fair test of the insert path.
    /// </summary>
    internal static class ScenarioSchema
    {
        public const int WarehouseCount = 4;
        public const int PartCount = 24;
        public const int MovementCount = 60;

        public static readonly string[] Tables = { "Movement", "Part", "Warehouse" };

        private static readonly string[] Cities =
            { "Curitiba", "Joinville", "Maringá", "Londrina", "Cascavel", "Blumenau" };

        private static readonly string[] Kinds = { "Entrada", "Saida", "Ajuste", "Perda" };

        /// <summary>DDL in dependency order. Dropping runs in the reverse order.</summary>
        public static IReadOnlyList<string> CreateStatements()
        {
            return new[]
            {
                @"CREATE TABLE Warehouse (
                    Id        INT           NOT NULL PRIMARY KEY,
                    Code      NVARCHAR(16)  NOT NULL,
                    City      NVARCHAR(64)  NOT NULL,
                    OpenedOn  DATE          NOT NULL,
                    Active    BIT           NOT NULL
                  )",

                @"CREATE TABLE Part (
                    Id            INT             NOT NULL PRIMARY KEY,
                    WarehouseId   INT             NOT NULL,
                    Sku           NVARCHAR(32)    NOT NULL,
                    Description   NVARCHAR(200)   NULL,
                    Quantity      INT             NOT NULL,
                    UnitPrice     DECIMAL(12, 4)  NOT NULL,
                    Weight        FLOAT           NULL,
                    Serial        BIGINT          NOT NULL,
                    Discontinued  BIT             NOT NULL,
                    UpdatedAt     DATETIME2(3)    NOT NULL,
                    CONSTRAINT FK_Part_Warehouse FOREIGN KEY (WarehouseId) REFERENCES Warehouse (Id)
                  )",

                // Note is NOT NULL on purpose in one place only: it is the
                // constraint the rollback checks violate, and a real engine
                // rejection is worth more than a fabricated exception.
                @"CREATE TABLE Movement (
                    Id          BIGINT        IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    PartId      INT           NOT NULL,
                    OccurredAt  DATETIME2(3)  NOT NULL,
                    Kind        NVARCHAR(16)  NOT NULL,
                    Quantity    INT           NOT NULL,
                    Note        NVARCHAR(400) NULL,
                    CONSTRAINT FK_Movement_Part FOREIGN KEY (PartId) REFERENCES Part (Id)
                  )",

                "CREATE INDEX IX_Part_WarehouseId ON Part (WarehouseId)",
                "CREATE INDEX IX_Movement_PartId ON Movement (PartId)"
            };
        }

        public static IReadOnlyList<string> DropStatements()
        {
            List<string> statements = new List<string>();
            foreach (string table in Tables)
                statements.Add("IF OBJECT_ID(N'" + table + "', N'U') IS NOT NULL DROP TABLE " + table);

            return statements;
        }

        /// <summary>
        /// Creates the tables through the gateway rather than through the
        /// provider. DDL is not what the library is for, but running it here
        /// means the very first thing the run proves is that an ordinary
        /// gateway command reached the server.
        /// </summary>
        public static async Task CreateAsync(IDatabaseGateway gateway, CancellationToken ct)
        {
            foreach (string statement in DropStatements())
                await gateway.Update(statement, null, ct).ConfigureAwait(false);

            foreach (string statement in CreateStatements())
                await gateway.Update(statement, null, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Fills the tables inside one transaction, through the session-aware
        /// <c>QueryBuilder</c> DML overloads.
        /// <para/>
        /// One transaction for the whole seed is deliberate: it means a failed
        /// seed leaves no half-populated database for the next phase to
        /// misread.
        /// </summary>
        public static async Task SeedAsync(IDatabaseGateway gateway, int seed, CancellationToken ct)
        {
            DeterministicRandom random = new DeterministicRandom(unchecked((ulong)seed) ^ 0xD1B54A32D192ED03UL);
            DateTime epoch = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            using (DbSession session = await gateway.OpenSessionAsync(ct).ConfigureAwait(false))
            {
                session.BeginTransaction();
                try
                {
                    for (int i = 1; i <= WarehouseCount; i++)
                    {
                        Dictionary<string, object?> values = new Dictionary<string, object?>
                        {
                            ["Id"] = i,
                            ["Code"] = "WH-" + i.ToString("D2", CultureInfo.InvariantCulture),
                            ["City"] = Cities[(int)(random.NextDouble() * Cities.Length) % Cities.Length],
                            ["OpenedOn"] = epoch.AddDays(-(30 * i)),
                            ["Active"] = i != WarehouseCount
                        };

                        await gateway.Insert(
                            new QueryBuilder().InsertInto("Warehouse", values),
                            session,
                            ct).ConfigureAwait(false);
                    }

                    for (int i = 1; i <= PartCount; i++)
                    {
                        // Every fourth part has no description and no weight, so
                        // the nullable columns are exercised by data rather than
                        // by a special case.
                        bool sparse = i % 4 == 0;

                        Dictionary<string, object?> values = new Dictionary<string, object?>
                        {
                            ["Id"] = i,
                            ["WarehouseId"] = ((i - 1) % WarehouseCount) + 1,
                            ["Sku"] = "SKU-" + i.ToString("D4", CultureInfo.InvariantCulture),
                            ["Description"] = sparse ? null : "Peça de reposição " + i.ToString(CultureInfo.InvariantCulture),
                            ["Quantity"] = 10 + (int)(random.NextDouble() * 500),
                            ["UnitPrice"] = decimal.Round((decimal)(1.5 + (random.NextDouble() * 900.0)), 4),
                            ["Weight"] = sparse ? (object?)null : Math.Round(0.05 + (random.NextDouble() * 40.0), 3),
                            ["Serial"] = 4_000_000_000L + (i * 37L),
                            ["Discontinued"] = i % 7 == 0,
                            ["UpdatedAt"] = epoch.AddMinutes(i * 13)
                        };

                        await gateway.Insert(
                            new QueryBuilder().InsertInto("Part", values),
                            session,
                            ct).ConfigureAwait(false);
                    }

                    for (int i = 1; i <= MovementCount; i++)
                    {
                        Dictionary<string, object?> values = new Dictionary<string, object?>
                        {
                            ["PartId"] = ((i - 1) % PartCount) + 1,
                            ["OccurredAt"] = epoch.AddHours(i),
                            ["Kind"] = Kinds[i % Kinds.Length],
                            ["Quantity"] = 1 + (int)(random.NextDouble() * 40),
                            ["Note"] = i % 5 == 0 ? null : "movimento " + i.ToString(CultureInfo.InvariantCulture)
                        };

                        await gateway.Insert(
                            new QueryBuilder().InsertInto("Movement", values),
                            session,
                            ct).ConfigureAwait(false);
                    }

                    session.Commit();
                }
                catch
                {
                    session.Rollback();
                    throw;
                }
            }
        }

        /// <summary>
        /// A single value covering every seeded table, used to prove that a
        /// recovery left the data exactly as it was. Rounding the money keeps
        /// the digest stable across the provider's decimal handling.
        /// </summary>
        public static async Task<string> DigestAsync(
            IDatabaseGateway gateway,
            CancellationToken ct)
        {
            List<Dictionary<string, RecordItem>> rows = await gateway.GetRaw(
                "SELECT " +
                "  (SELECT COUNT(*) FROM Warehouse) AS Warehouses, " +
                "  (SELECT COUNT(*) FROM Part) AS Parts, " +
                "  (SELECT COUNT(*) FROM Movement) AS Movements, " +
                "  (SELECT ISNULL(SUM(CONVERT(bigint, Quantity)), 0) FROM Part) AS PartQuantity, " +
                "  (SELECT CONVERT(decimal(18,2), ISNULL(SUM(UnitPrice), 0)) FROM Part) AS PartValue, " +
                "  (SELECT ISNULL(SUM(CONVERT(bigint, Quantity)), 0) FROM Movement) AS MovementQuantity",
                null,
                ct).ConfigureAwait(false);

            if (rows.Count != 1)
                return "no-digest";

            Dictionary<string, RecordItem> row = rows[0];
            return "w" + row["Warehouses"] +
                   " p" + row["Parts"] +
                   " m" + row["Movements"] +
                   " pq" + row["PartQuantity"] +
                   " pv" + row["PartValue"] +
                   " mq" + row["MovementQuantity"];
        }
    }
}
