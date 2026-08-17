#nullable enable
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Data.Gateway;
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
    public sealed partial class FarmDb : IDisposable
    {
        /// <summary>How often a new arrival gets its parentage recorded.</summary>
        private const double ParentageChance = 0.6;

        private static readonly Random _parentage = new Random();

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
                else
                {
                    await db.EnsureTagSequenceAsync(ct).ConfigureAwait(false);
                    await db.EnsureSimSchemaAsync(ct).ConfigureAwait(false);
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

            await SeedTagSequenceAsync(ct).ConfigureAwait(false);
        }

        private async Task SeedTagSequenceAsync(CancellationToken ct, DbSession? session = null)
        {
            foreach (KeyValuePair<string, int> entry in FarmSeed.InitialTagNumbers())
            {
                await InsertRowAsync("TagSequence", new Dictionary<string, object?>
                {
                    ["Prefix"] = entry.Key,
                    ["LastNumber"] = entry.Value
                }, ct, session).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Creates and seeds <c>TagSequence</c> when an existing database predates it.
        /// The scenario's databases are disposable, but failing to connect to one made
        /// earlier is a worse first impression than a silent forward step.
        /// </summary>
        private async Task EnsureTagSequenceAsync(CancellationToken ct)
        {
            IReadOnlyList<string> tables = await ListTablesAsync(ct).ConfigureAwait(false);

            foreach (string table in tables)
                if (string.Equals(table, "TagSequence", StringComparison.OrdinalIgnoreCase))
                    return;

            foreach (string ddl in Profile.SchemaDdl())
                if (ddl.IndexOf("TagSequence", StringComparison.Ordinal) >= 0)
                    await Gateway.Insert(ddl, null, ct).ConfigureAwait(false);

            await SeedTagSequenceAsync(ct).ConfigureAwait(false);
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

        /// <summary>
        /// Runs a <see cref="QueryBuilder"/> and hands back the raw rows.
        /// <para/>
        /// Exists so the builder itself can be exercised. Everything interesting in the
        /// scenario's own SQL is hand-written, which leaves the builder's per-dialect
        /// translation — <c>TOP</c> against <c>LIMIT</c>, <c>DISTINCT</c> placement,
        /// joins, subquery parameter renaming — with no coverage at all.
        /// </summary>
        public Task<List<Dictionary<string, RecordItem>>> QueryAsync(
            QueryBuilder builder,
            CancellationToken ct = default) =>
            Gateway.GetRaw(builder, ct);

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

#if NET6_0_OR_GREATER
        /// <summary>
        /// Reads the operation log one row at a time instead of materializing it.
        /// <para/>
        /// The guard is the point as much as the method is. <c>IDatabaseGateway</c>
        /// composes <c>IDqlStreamingGateway</c> only on net6 and later — on net481 the
        /// streaming interface and its members are absent. So this is the one place in the
        /// scenario where the two targets genuinely differ, and the net481 build
        /// passing is what proves the guard holds.
        /// <para/>
        /// It is also the only low-memory pull path the library offers, and the
        /// operation log of a long run is its natural subject: a simulation left going
        /// for hours produces a table that <see cref="GetOperationLogAsync"/> would
        /// bring into memory whole.
        /// </summary>
        public async Task<int> StreamOperationLogAsync(
            Action<OperationLogEntry>? onEntry = null,
            CancellationToken ct = default)
        {
            var builder = new QueryBuilder()
                .Select("*")
                .From(Profile.Quote("OperationLog"));

            int seen = 0;
            await foreach (OperationLogEntry entry in
                Gateway.StreamDto<OperationLogEntry>(builder, ct).ConfigureAwait(false))
            {
                onEntry?.Invoke(entry);
                seen++;
            }

            return seen;
        }
#endif

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
        /// Registers a new animal and returns it with the tag the database assigned.
        /// <para/>
        /// The tag comes from a persisted per-prefix counter that is read, incremented
        /// and written inside the same transaction as the insert, so numbers are never
        /// reused: a herd showing <c>BV-002, BV-004, BV-006</c> is telling you what
        /// happened to <c>BV-001</c>, <c>BV-003</c> and <c>BV-005</c>. Deriving the
        /// next number from the surviving rows could not do that, because a hard
        /// delete takes its own evidence with it.
        /// </para>
        /// </summary>
        public async Task<Animal> AddAnimalAsync(
            NewAnimalRequest request,
            CancellationToken ct = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.Species))
                throw new ArgumentException("Espécie é obrigatória.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.Gender))
                throw new ArgumentException("Gênero é obrigatório.", nameof(request));
            if (request.AgeYears < 0)
                throw new ArgumentException("Idade não pode ser negativa.", nameof(request));

            string prefix = FarmSeed.PrefixFor(request.Species);
            var animal = new Animal();

            using (DbSession session = await Gateway.OpenSessionAsync(ct).ConfigureAwait(false))
            {
                session.BeginTransaction();
                try
                {
                    // Read-modify-write on the counter, inside the transaction. Two
                    // concurrent registrations would otherwise be able to agree on the
                    // same number.
                    List<Dictionary<string, RecordItem>> counter = await Gateway.GetRaw(
                        "SELECT [LastNumber] FROM [TagSequence] WHERE [Prefix] = @p1",
                        new Dictionary<string, object?> { ["@p1"] = prefix },
                        session,
                        ct).ConfigureAwait(false);

                    if (counter.Count == 0)
                    {
                        throw new InvalidOperationException(
                            "Nenhum contador de tag para o prefixo '" + prefix + "'.");
                    }

                    int next = counter[0]["LastNumber"].As<int>() + 1;
                    string tag = FarmSeed.FormatTag(prefix, next);

                    string? notes = request.Notes;
                    if (string.IsNullOrWhiteSpace(notes))
                    {
                        notes = await TryDescribeParentageAsync(request, session, ct)
                            .ConfigureAwait(false);
                    }

                    var bump = new QueryBuilder()
                        .Update("TagSequence", new Dictionary<string, object?> { ["LastNumber"] = next })
                        .Where("[Prefix] = @p1", prefix);

                    await Gateway.Update(bump, session, ct).ConfigureAwait(false);

                    await InsertRowAsync("Animals", new Dictionary<string, object?>
                    {
                        ["Species"] = request.Species,
                        ["Tag"] = tag,
                        ["AgeYears"] = request.AgeYears,
                        ["Gender"] = request.Gender,
                        ["Notes"] = notes
                    }, ct, session).ConfigureAwait(false);

                    // The gateway has no way to return an inserted identity - Insert
                    // reports affected rows only - so the row is read back by its tag,
                    // which is unique and was just assigned above.
                    List<Dictionary<string, RecordItem>> inserted = await Gateway.GetRaw(
                        "SELECT [Id] FROM [Animals] WHERE [Tag] = @p1",
                        new Dictionary<string, object?> { ["@p1"] = tag },
                        session,
                        ct).ConfigureAwait(false);

                    animal.Id = inserted.Count > 0 ? inserted[0]["Id"].As<int>() : 0;
                    animal.Species = request.Species;
                    animal.Tag = tag;
                    animal.AgeYears = request.AgeYears;
                    animal.Gender = request.Gender;
                    animal.Notes = notes;

                    await WriteLogAsync(
                        session,
                        EntityKinds.Animal,
                        animal.Id,
                        tag + " (" + request.Species + ")",
                        Operations.Add,
                        1,
                        notes,
                        ct).ConfigureAwait(false);

                    session.Commit();
                }
                catch
                {
                    session.Rollback();
                    throw;
                }
            }

            return animal;
        }

        /// <summary>
        /// Sometimes records the new arrival as the offspring of a living female of
        /// the same species, for the herd book.
        /// <para/>
        /// Runs on the transaction's own connection, so the mother it names is one
        /// that exists at the instant the calf is inserted — an animal removed a
        /// moment earlier can never be credited. Candidates must be older than the
        /// newborn, which is both sensible and what keeps a one-year-old from
        /// mothering herself into the record.
        /// <para/>
        /// This is decorative, and the only non-deterministic thing in the scenario:
        /// the seed never varies, but two registrations of the same animal can produce
        /// different notes. Nothing verified by the procedure depends on it.
        /// </para>
        /// </summary>
        private async Task<string?> TryDescribeParentageAsync(
            NewAnimalRequest request,
            DbSession session,
            CancellationToken ct)
        {
            if (_parentage.NextDouble() > ParentageChance)
                return null;

            List<Dictionary<string, RecordItem>> mothers = await Gateway.GetRaw(
                "SELECT [Tag] FROM [Animals] " +
                "WHERE [Species] = @p1 AND [Gender] = @p2 AND [AgeYears] > @p3 " +
                "ORDER BY [Tag]",
                new Dictionary<string, object?>
                {
                    ["@p1"] = request.Species,
                    ["@p2"] = Genders.Female,
                    ["@p3"] = request.AgeYears
                },
                session,
                ct).ConfigureAwait(false);

            if (mothers.Count == 0)
                return null;

            string mother = mothers[_parentage.Next(mothers.Count)]["Tag"].ToString();
            string relation = request.Gender == Genders.Male ? "Filho" : "Filha";

            return relation + " de " + mother;
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
                    await Gateway.Delete(
                        new QueryBuilder()
                            .DeleteFrom("[Animals]")
                            .Where("[Id] = @p1", animal.Id),
                        session,
                        ct).ConfigureAwait(false);

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
