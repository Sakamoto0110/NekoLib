#nullable enable
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Data.Gateway;
// The concrete gateway lives under an "Internal" namespace even though the type is
// public, so constructing the module's primary entry point requires importing it.
using NekoLib.Data.Internal.Gateway;
using NekoLib.Data.Query;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.Model;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.Providers;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.Schema;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.Core
{
    /// <summary>
    /// One open farm database, bound to a single provider profile.
    /// <para/>
    /// Owns the <see cref="QueryExecutionContext"/> - which the library documents as
    /// caller-disposed - and exposes the gateway through the interface surface so the
    /// scenario exercises the contracts rather than the concrete class.
    /// </summary>
    public sealed class FarmDb : IDisposable
    {
        private readonly QueryExecutionContext _ctx;
        private readonly DatabaseGateway _gateway;
        private bool _disposed;

        /// <summary>Raised for every SQL statement the gateway generates or dispatches.</summary>
        public event Action<string>? SqlTraced;

        public IFarmProviderProfile Profile { get; }

        private FarmDb(IFarmProviderProfile profile, QueryExecutionContext ctx)
        {
            Profile = profile;
            _ctx = ctx;
            _gateway = new DatabaseGateway(ctx);

            // The context raises SQL notifications synchronously, in subscription
            // order, inside the database call. Keep these handlers trivial: a slow
            // subscriber directly slows every query.
            _ctx.OnSqlGenerated += e => SqlTraced?.Invoke("gerado    " + e.RawSqlQuery);
            _ctx.OnSqlDispatch += e => SqlTraced?.Invoke("despachado " + e.RawSqlQuery);
        }

        private IDatabaseGateway Gateway => _gateway;

        // -----------------------------------------------------------------
        // Lifecycle
        // -----------------------------------------------------------------

        /// <summary>
        /// Opens the database for a profile, creating and seeding it when needed.
        /// </summary>
        /// <param name="recreate">Deletes the existing file first, forcing a reseed.</param>
        public static async Task<FarmDb> OpenAsync(
            IFarmProviderProfile profile,
            bool recreate,
            CancellationToken ct = default)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            ProviderAvailability availability = profile.Probe();
            if (!availability.IsAvailable)
            {
                throw new InvalidOperationException(
                    availability.Reason +
                    (availability.Remedy == null ? string.Empty : " " + availability.Remedy));
            }

            if (recreate)
                profile.DeleteDatabaseFile();

            bool freshFile = !System.IO.File.Exists(profile.DatabasePath);
            profile.EnsureDatabaseFile();

            // EmitRawSqlInEvents defaults to false - the library redacts SQL in its
            // events so literals never reach a log by accident. This scenario opts in
            // deliberately: the SQL console is the point, and the database is a
            // throwaway local file with fabricated data.
            var options = new DatabaseGatewayOptions
            {
                EmitRawSqlInEvents = true
            };

            var ctx = new QueryExecutionContext(
                profile.CreateConnectionFactory(),
                profile.CreateTranslator(),
                options);

            var db = new FarmDb(profile, ctx);
            try
            {
                if (freshFile)
                {
                    await db.CreateSchemaAsync(ct).ConfigureAwait(false);
                    await db.SeedAsync(ct).ConfigureAwait(false);
                }

                return db;
            }
            catch
            {
                db.Dispose();
                throw;
            }
        }

        private async Task CreateSchemaAsync(CancellationToken ct)
        {
            foreach (string ddl in Profile.SchemaDdl())
            {
                // DDL goes through the DML path because the gateway has no dedicated
                // schema surface: Insert() is just "execute non-query" here.
                await Gateway.Insert(ddl, null, ct).ConfigureAwait(false);
            }
        }

        private async Task SeedAsync(CancellationToken ct)
        {
            foreach (Role role in FarmSeed.Roles)
            {
                await InsertRowAsync("Roles", new Dictionary<string, object?>
                {
                    ["Title"] = role.Title,
                    ["BaseSalary"] = role.BaseSalary
                }, ct).ConfigureAwait(false);
            }

            foreach (Employee employee in FarmSeed.Employees)
            {
                await InsertRowAsync("Employees", new Dictionary<string, object?>
                {
                    ["Name"] = employee.Name,
                    ["Age"] = employee.Age,
                    ["Cpf"] = employee.Cpf,
                    ["Phone"] = employee.Phone,
                    ["RoleId"] = employee.RoleId
                }, ct).ConfigureAwait(false);
            }

            foreach (Product product in FarmSeed.Products)
            {
                await InsertRowAsync("Products", new Dictionary<string, object?>
                {
                    ["Name"] = product.Name,
                    ["Category"] = product.Category,
                    ["Unit"] = product.Unit,
                    ["Quantity"] = product.Quantity,
                    ["UnitPrice"] = product.UnitPrice
                }, ct).ConfigureAwait(false);
            }

            foreach (Animal animal in FarmSeed.Animals)
            {
                await InsertRowAsync("Animals", new Dictionary<string, object?>
                {
                    ["Species"] = animal.Species,
                    ["Tag"] = animal.Tag,
                    ["AgeYears"] = animal.AgeYears,
                    ["Gender"] = animal.Gender,
                    ["Notes"] = animal.Notes
                }, ct).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// A fresh <see cref="QueryBuilder"/> per insert, deliberately. Reusing one
        /// after <c>Build()</c> accumulates parameters - the builder is not idempotent
        /// for DML.
        /// </summary>
        private Task<int> InsertRowAsync(
            string table,
            Dictionary<string, object?> values,
            CancellationToken ct,
            DbSession? session = null)
        {
            var builder = new QueryBuilder().InsertInto(table, values);
            return session == null
                ? Gateway.Insert(builder, ct)
                : Gateway.Insert(builder, session, ct);
        }

        // -----------------------------------------------------------------
        // Catalog and free-form reads
        // -----------------------------------------------------------------

        /// <summary>Lists user tables using whatever mechanism the engine offers.</summary>
        public async Task<IReadOnlyList<string>> ListTablesAsync(CancellationToken ct = default)
        {
            using (DbSession session = await Gateway.OpenSessionAsync(ct).ConfigureAwait(false))
            {
                return await Profile
                    .ListTablesAsync(Gateway, session, ct)
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Reads a whole table into a <see cref="DataTable"/> for display. Goes
        /// through the raw path, so the column set is discovered from the reader
        /// rather than from a DTO.
        /// </summary>
        public Task<DataTable> ReadTableAsync(string table, int? top = null, CancellationToken ct = default)
        {
            var builder = new QueryBuilder().Select("*").From(Profile.Quote(table));
            if (top.HasValue)
                builder = builder.Top(top.Value);

            return ReadIntoTableAsync(builder, ct);
        }

        private async Task<DataTable> ReadIntoTableAsync(QueryBuilder builder, CancellationToken ct)
        {
            List<Dictionary<string, RecordItem>> rows =
                await Gateway.GetRaw(builder, ct).ConfigureAwait(false);

            return ToDataTable(rows);
        }

        /// <summary>
        /// Runs a hand-written statement. Reads come back as a grid; anything else
        /// reports the affected row count. The distinction is made on the statement
        /// itself because ADO.NET needs to know before executing.
        /// </summary>
        public async Task<RawQueryResult> ExecuteRawAsync(string sql, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentException("Consulta vazia.", nameof(sql));

            string trimmed = sql.TrimStart();
            bool isRead =
                trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("PRAGMA", StringComparison.OrdinalIgnoreCase);

            if (isRead)
            {
                List<Dictionary<string, RecordItem>> rows =
                    await Gateway.GetRaw(sql, null, ct).ConfigureAwait(false);
                return RawQueryResult.FromRows(ToDataTable(rows), rows.Count);
            }

            int affected = await Gateway.Insert(sql, null, ct).ConfigureAwait(false);
            return RawQueryResult.FromAffected(affected);
        }

        private static DataTable ToDataTable(List<Dictionary<string, RecordItem>> rows)
        {
            var table = new DataTable();

            if (rows.Count == 0)
                return table;

            foreach (string column in rows[0].Keys)
                table.Columns.Add(column, typeof(string));

            foreach (Dictionary<string, RecordItem> row in rows)
            {
                DataRow dataRow = table.NewRow();
                foreach (KeyValuePair<string, RecordItem> cell in row)
                {
                    // Raw mode already normalized every value to an invariant-culture
                    // string, so null and empty are indistinguishable here by design.
                    dataRow[cell.Key] = cell.Value.Value;
                }
                table.Rows.Add(dataRow);
            }

            return table;
        }

        // -----------------------------------------------------------------
        // Typed reads
        // -----------------------------------------------------------------

        public Task<List<Product>> GetProductsAsync(CancellationToken ct = default) =>
            Gateway.GetDto<Product>(
                "SELECT [Id], [Name], [Category], [Unit], [Quantity], [UnitPrice] " +
                "FROM [Products] ORDER BY [Category], [Name]",
                null,
                ct);

        public Task<List<Animal>> GetAnimalsAsync(CancellationToken ct = default) =>
            Gateway.GetDto<Animal>(
                "SELECT [Id], [Species], [Tag], [AgeYears], [Gender], [Notes] " +
                "FROM [Animals] ORDER BY [Species], [Tag]",
                null,
                ct);

        public Task<List<OperationLogEntry>> GetOperationLogAsync(CancellationToken ct = default) =>
            Gateway.GetDto<OperationLogEntry>(
                "SELECT [Id], [OccurredAt], [EntityKind], [EntityId], [EntityName], " +
                "[Operation], [Quantity], [Reason] " +
                "FROM [OperationLog] ORDER BY [Id] DESC",
                null,
                ct);

        // -----------------------------------------------------------------
        // Audited stock movements
        // -----------------------------------------------------------------

        /// <summary>
        /// Applies a stock delta and records it, both inside one transaction. This is
        /// the scenario's reason to exist: the quantity update and its audit row must
        /// either both land or neither does.
        /// </summary>
        public async Task ChangeProductQuantityAsync(
            Product product,
            int delta,
            string? reason,
            CancellationToken ct = default)
        {
            if (product == null) throw new ArgumentNullException(nameof(product));
            if (delta == 0) throw new ArgumentException("Delta não pode ser zero.", nameof(delta));

            int updated = product.Quantity + delta;
            if (updated < 0)
            {
                throw new InvalidOperationException(
                    "Estoque de " + product.Name + " não pode ficar negativo " +
                    "(atual " + product.Quantity + ", pedido " + delta + ").");
            }

            using (DbSession session = await Gateway.OpenSessionAsync(ct).ConfigureAwait(false))
            {
                session.BeginTransaction();
                try
                {
                    var update = new QueryBuilder()
                        .Update("Products", new Dictionary<string, object?> { ["Quantity"] = updated })
                        .Where("[Id] = @p1", product.Id);

                    await Gateway.Update(update, session, ct).ConfigureAwait(false);

                    await WriteLogAsync(
                        session,
                        EntityKinds.Product,
                        product.Id,
                        product.Name,
                        delta > 0 ? Operations.Add : Operations.Remove,
                        Math.Abs(delta),
                        reason,
                        ct).ConfigureAwait(false);

                    session.Commit();
                }
                catch
                {
                    session.Rollback();
                    throw;
                }
            }

            product.Quantity = updated;
        }

        /// <summary>
        /// Removes an animal from the herd. The reason is mandatory here - the caller
        /// is expected to have collected it from the user - and is persisted with the
        /// audit row, again transactionally with the delete.
        /// </summary>
        public async Task RemoveAnimalAsync(
            Animal animal,
            string reason,
            CancellationToken ct = default)
        {
            if (animal == null) throw new ArgumentNullException(nameof(animal));
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("A remoção de um animal exige um motivo.", nameof(reason));

            using (DbSession session = await Gateway.OpenSessionAsync(ct).ConfigureAwait(false))
            {
                session.BeginTransaction();
                try
                {
                    // Delete has no QueryBuilder overload - only the raw-SQL one - so
                    // this half of the operation is hand-written while the log insert
                    // beside it is built. Same transaction either way.
                    await Gateway.Delete(
                        "DELETE FROM [Animals] WHERE [Id] = @p1",
                        new Dictionary<string, object?> { ["@p1"] = animal.Id },
                        ct,
                        session).ConfigureAwait(false);

                    await WriteLogAsync(
                        session,
                        EntityKinds.Animal,
                        animal.Id,
                        animal.Tag + " (" + animal.Species + ")",
                        Operations.Remove,
                        1,
                        reason,
                        ct).ConfigureAwait(false);

                    session.Commit();
                }
                catch
                {
                    session.Rollback();
                    throw;
                }
            }
        }

        private Task<int> WriteLogAsync(
            DbSession session,
            string entityKind,
            int entityId,
            string entityName,
            string operation,
            int quantity,
            string? reason,
            CancellationToken ct)
        {
            return InsertRowAsync("OperationLog", new Dictionary<string, object?>
            {
                ["OccurredAt"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                ["EntityKind"] = entityKind,
                ["EntityId"] = entityId,
                ["EntityName"] = entityName,
                ["Operation"] = operation,
                ["Quantity"] = quantity,
                ["Reason"] = reason
            }, ct, session);
        }

        // -----------------------------------------------------------------

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _ctx.Dispose();
        }
    }

    /// <summary>Outcome of a hand-written statement.</summary>
    public sealed class RawQueryResult
    {
        public DataTable? Rows { get; }
        public int RowCount { get; }
        public int AffectedRows { get; }
        public bool IsRead { get; }

        private RawQueryResult(DataTable? rows, int rowCount, int affected, bool isRead)
        {
            Rows = rows;
            RowCount = rowCount;
            AffectedRows = affected;
            IsRead = isRead;
        }

        public static RawQueryResult FromRows(DataTable rows, int count) =>
            new RawQueryResult(rows, count, 0, true);

        public static RawQueryResult FromAffected(int affected) =>
            new RawQueryResult(null, 0, affected, false);

        public string Describe() => IsRead
            ? RowCount + " linha(s) retornada(s)"
            : AffectedRows + " linha(s) afetada(s)";
    }
}
