#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using NekoLib.Data.Dynamic;
using NekoLib.Data.Query;
using NekoLib.Data.RuntimeTests.SqlServer.Model;

namespace NekoLib.Data.RuntimeTests.SqlServer.Workload
{
    /// <summary>
    /// Every way of reading the same rows, the value round-trip for every
    /// column type in the schema, and the builder clauses the SQL Server
    /// translator has to render.
    /// <para/>
    /// The read shapes are pointed at one query and required to agree, which is
    /// the only assertion that catches a shape that maps differently or loses
    /// rows. Testing each in isolation would prove that each returned
    /// something.
    /// </summary>
    internal static class ReadMatrix
    {
        private const string Phase = "read";
        private const int TargetWarehouse = 1;

        private const string SelectColumns =
            "Id, WarehouseId, Sku, Description, Quantity, UnitPrice, Weight, Serial, Discontinued, UpdatedAt";

        private const string ParameterizedSql =
            "SELECT " + SelectColumns + " FROM Part WHERE WarehouseId = @p1";

        public static async Task RunAsync(PhaseContext context)
        {
            await ReadShapesAgree(context).ConfigureAwait(false);
            await TypeRoundTrip(context).ConfigureAwait(false);
            await BuilderClauses(context).ConfigureAwait(false);
            await ReaderFailureAndEarlyExit(context).ConfigureAwait(false);
        }

        private static QueryBuilder Builder()
        {
            return new QueryBuilder()
                .Select("Id", "WarehouseId", "Sku", "Description", "Quantity",
                        "UnitPrice", "Weight", "Serial", "Discontinued", "UpdatedAt")
                .From("Part")
                .Where("WarehouseId = @p1", TargetWarehouse);
        }

        private static Dictionary<string, object?> Parameters()
        {
            return new Dictionary<string, object?> { ["@p1"] = TargetWarehouse };
        }

        private static Task ReadShapesAgree(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "read-shapes-agree",
                "every read shape returns the same rows for the same query",
                async check =>
                {
                    List<KeyValuePair<string, int>> shapes = new List<KeyValuePair<string, int>>();
                    List<long> typedSums = new List<long>();
                    var gateway = context.Workspace.Gateway;

                    shapes.Add(Pair("GetRaw(sql)", (await gateway
                        .GetRaw(ParameterizedSql, Parameters(), context.Ct).ConfigureAwait(false)).Count));

                    shapes.Add(Pair("GetRaw(builder)", (await gateway
                        .GetRaw(Builder(), context.Ct).ConfigureAwait(false)).Count));

                    int rawCallbackRows = 0;
                    await gateway.ReadRaw(
                        "SELECT " + SelectColumns + " FROM Part WHERE WarehouseId = " +
                        TargetWarehouse.ToString(CultureInfo.InvariantCulture),
                        _ => rawCallbackRows++,
                        context.Ct).ConfigureAwait(false);
                    shapes.Add(Pair("ReadRaw(sql)", rawCallbackRows));

                    int rawBuilderRows = 0;
                    await gateway.ReadRaw(Builder(), _ => rawBuilderRows++, context.Ct).ConfigureAwait(false);
                    shapes.Add(Pair("ReadRaw(builder)", rawBuilderRows));

                    List<PartRow> dtoFromSql = await gateway
                        .GetDto<PartRow>(ParameterizedSql, Parameters(), context.Ct).ConfigureAwait(false);
                    shapes.Add(Pair("GetDto(sql)", dtoFromSql.Count));
                    typedSums.Add(SumQuantity(dtoFromSql));

                    List<PartRow> dtoFromBuilder = await gateway
                        .GetDto<PartRow>(Builder(), context.Ct).ConfigureAwait(false);
                    shapes.Add(Pair("GetDto(builder)", dtoFromBuilder.Count));
                    typedSums.Add(SumQuantity(dtoFromBuilder));

                    List<PartRow> dtoCallback = new List<PartRow>();
                    await gateway.ReadDto<PartRow>(Builder(), dtoCallback.Add, context.Ct).ConfigureAwait(false);
                    shapes.Add(Pair("ReadDto(builder)", dtoCallback.Count));
                    typedSums.Add(SumQuantity(dtoCallback));

                    List<DynamicRow> dynamicRows = await gateway
                        .GetDynamic(Builder(), context.Ct).ConfigureAwait(false);
                    shapes.Add(Pair("GetDynamic", dynamicRows.Count));

                    int dynamicCallbackRows = 0;
                    await gateway.ReadDynamic(Builder(), _ => dynamicCallbackRows++, context.Ct).ConfigureAwait(false);
                    shapes.Add(Pair("ReadDynamic", dynamicCallbackRows));

                    List<PartRow> universal = await gateway
                        .Get<SqlServerQueryTranslator, PartRow>(Builder(), context.Ct).ConfigureAwait(false);
                    shapes.Add(Pair("Get<TTranslator,T>", universal.Count));
                    typedSums.Add(SumQuantity(universal));

                    List<PartRow> universalTyped = new List<PartRow>();
                    await gateway.Read<PartRow>(Builder(), universalTyped.Add, context.Ct).ConfigureAwait(false);
                    shapes.Add(Pair("Read<T>", universalTyped.Count));
                    typedSums.Add(SumQuantity(universalTyped));

                    List<PartRow> universalDelegate = new List<PartRow>();
                    await gateway.Read(Builder(), (Action<PartRow>)universalDelegate.Add, context.Ct)
                        .ConfigureAwait(false);
                    shapes.Add(Pair("Read(delegate)", universalDelegate.Count));
                    typedSums.Add(SumQuantity(universalDelegate));

                    using (DbSession session = await gateway.OpenSessionAsync(context.Ct).ConfigureAwait(false))
                    {
                        shapes.Add(Pair("GetRaw(session)", (await gateway
                            .GetRaw(ParameterizedSql, Parameters(), session, context.Ct).ConfigureAwait(false)).Count));

                        List<PartRow> sessionDto = await gateway
                            .GetDto<PartRow>(Builder(), session, context.Ct).ConfigureAwait(false);
                        shapes.Add(Pair("GetDto(session)", sessionDto.Count));
                        typedSums.Add(SumQuantity(sessionDto));

                        shapes.Add(Pair("GetDynamic(session)", (await gateway
                            .GetDynamic(Builder(), session, context.Ct).ConfigureAwait(false)).Count));
                    }

#if NET6_0_OR_GREATER
                    int streamedRaw = 0;
                    await foreach (Dictionary<string, RecordItem> _ in gateway
                        .StreamRaw(ParameterizedSql, Parameters(), context.Ct).ConfigureAwait(false))
                        streamedRaw++;
                    shapes.Add(Pair("StreamRaw", streamedRaw));

                    List<PartRow> streamedDto = new List<PartRow>();
                    await foreach (PartRow row in gateway.StreamDto<PartRow>(Builder(), context.Ct).ConfigureAwait(false))
                        streamedDto.Add(row);
                    shapes.Add(Pair("StreamDto", streamedDto.Count));
                    typedSums.Add(SumQuantity(streamedDto));

                    int streamedDynamic = 0;
                    await foreach (DynamicRow _ in gateway.StreamDynamic(Builder(), context.Ct).ConfigureAwait(false))
                        streamedDynamic++;
                    shapes.Add(Pair("StreamDynamic", streamedDynamic));
#else
                    check.Note("streaming shapes are absent on net481 by design: IDqlStreamingGateway carries " +
                               "[Obsolete(error: true)] below net6, so referencing them would not compile");
#endif

                    bool populated = await gateway.ContainsData(
                        "SELECT 1 FROM Part WHERE WarehouseId = " +
                        TargetWarehouse.ToString(CultureInfo.InvariantCulture),
                        context.Ct).ConfigureAwait(false);

                    bool empty = await gateway.ContainsData(
                        "SELECT 1 FROM Part WHERE WarehouseId = 999", context.Ct).ConfigureAwait(false);

                    check.That(populated, "ContainsData reported no rows for a populated query");
                    check.That(!empty, "ContainsData reported rows for an empty query");

                    int expected = ScenarioSchemaRowsPerWarehouse();
                    List<string> disagreed = new List<string>();

                    foreach (KeyValuePair<string, int> shape in shapes)
                    {
                        if (shape.Value != expected)
                            disagreed.Add(shape.Key + "=" + shape.Value);
                    }

                    check.Note(shapes.Count + " read shapes, all asked for warehouse " + TargetWarehouse);
                    check.That(disagreed.Count == 0,
                        "expected every shape to return " + expected + " rows; these disagreed: " +
                        string.Join(", ", disagreed.ToArray()));

                    foreach (long sum in typedSums)
                    {
                        check.Equal(typedSums[0], sum,
                            "typed shapes disagreed on the summed quantity");
                    }

                    check.Note("the typed shapes also agree on values, not only counts: summed quantity " +
                               typedSums[0].ToString(CultureInfo.InvariantCulture));

                    for (int i = 0; i < shapes.Count; i++) context.Counters.Success();
                });
        }

        /// <summary>
        /// Reads two rows through the gateway and through the provider directly,
        /// and requires them to match value by value.
        /// <para/>
        /// The direct read is the oracle. Asserting the mapped values against
        /// constants would only prove the seed; asserting them against what the
        /// provider returned proves the mapping.
        /// </summary>
        private static Task TypeRoundTrip(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "type-round-trip",
                "every column type maps to the DTO with the value the provider returned",
                async check =>
                {
                    // Part 1 has every column populated; part 4 is one of the
                    // rows the seed leaves sparse, so the nullable columns are
                    // exercised as data rather than as a special case.
                    foreach (int id in new[] { 1, 4 })
                    {
                        List<PartRow> mapped = await context.Workspace.Gateway.GetDto<PartRow>(
                            "SELECT " + SelectColumns + " FROM Part WHERE Id = @p1",
                            new Dictionary<string, object?> { ["@p1"] = id },
                            context.Ct).ConfigureAwait(false);

                        check.Equal(1, mapped.Count, "rows for part " + id);
                        PartRow row = mapped[0];
                        context.Counters.Success();

                        await ReadDirectlyAsync(context, id, direct =>
                        {
                            check.Equal(direct.Id, row.Id, "Id");
                            check.Equal(direct.WarehouseId, row.WarehouseId, "WarehouseId");
                            check.Equal(direct.Sku, row.Sku, "Sku");
                            check.Equal(direct.Description, row.Description, "Description");
                            check.Equal(direct.Quantity, row.Quantity, "Quantity");
                            check.Equal(direct.Serial, row.Serial, "Serial");

                            check.That(direct.UnitPrice == row.UnitPrice,
                                "UnitPrice: provider returned " + direct.UnitPrice + ", mapper produced " + row.UnitPrice);

                            check.That(Nullable.Equals(direct.Weight, row.Weight),
                                "Weight: provider returned " + Describe(direct.Weight) +
                                ", mapper produced " + Describe(row.Weight));

                            check.That(direct.Discontinued == row.Discontinued, "Discontinued");
                            check.That(direct.UpdatedAt == row.UpdatedAt,
                                "UpdatedAt: provider returned " + direct.UpdatedAt.ToString("O", CultureInfo.InvariantCulture) +
                                ", mapper produced " + row.UpdatedAt.ToString("O", CultureInfo.InvariantCulture));
                        }).ConfigureAwait(false);

                        check.Note("part " + id + ": Description is " +
                                   (row.Description == null ? "null" : "present") +
                                   ", Weight is " + Describe(row.Weight) +
                                   ", UnitPrice " + row.UnitPrice.ToString(CultureInfo.InvariantCulture));
                    }

                    // Raw mode is documented as lossy, and the scenario records
                    // that rather than asserting a contract the type does not
                    // have: RecordItem carries invariant text, so a database
                    // null and an empty string arrive the same way.
                    List<Dictionary<string, RecordItem>> rawSparse = await context.Workspace.Gateway.GetRaw(
                        "SELECT Description FROM Part WHERE Id = @p1",
                        new Dictionary<string, object?> { ["@p1"] = 4 },
                        context.Ct).ConfigureAwait(false);

                    context.Counters.Success();
                    check.Equal(1, rawSparse.Count, "raw rows for the sparse part");
                    check.Note("raw mode renders the null Description as '" +
                               rawSparse[0]["Description"].Value + "' - the documented lossy contract, not a defect");
                });
        }

        private static Task BuilderClauses(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "builder-clauses",
                "the SQL Server translator renders every builder clause and the rows agree with hand-written SQL",
                async check =>
                {
                    var gateway = context.Workspace.Gateway;

                    List<Dictionary<string, RecordItem>> top = await gateway.GetRaw(
                        new QueryBuilder().Select("Id").From("Part").OrderBy("Id").Top(3),
                        context.Ct).ConfigureAwait(false);
                    check.Equal(3, top.Count, "Top(3) row count");
                    check.Equal("1", top[0]["Id"].Value, "Top with OrderBy returned the wrong first row");
                    check.Note("Top renders as SELECT TOP (n) for SQL Server, and OrderBy survives it");

                    List<Dictionary<string, RecordItem>> counted = await gateway.GetRaw(
                        new QueryBuilder().Count().From("Part"), context.Ct).ConfigureAwait(false);
                    check.Equal(ScenarioSchemaPartCount(), long.Parse(FirstValue(counted), CultureInfo.InvariantCulture),
                        "COUNT(*)");

                    List<Dictionary<string, RecordItem>> distinctCount = await gateway.GetRaw(
                        new QueryBuilder().DistinctCount("WarehouseId").From("Part"), context.Ct).ConfigureAwait(false);
                    check.Equal(ScenarioSchemaWarehouseCount(),
                        long.Parse(FirstValue(distinctCount), CultureInfo.InvariantCulture),
                        "COUNT(DISTINCT WarehouseId)");
                    check.Note("SQL Server accepts COUNT(DISTINCT ...) directly, which is what Access could not - " +
                               "the same builder call, two different translations");

                    List<Dictionary<string, RecordItem>> joined = await gateway.GetRaw(
                        new QueryBuilder()
                            .Select("p.Sku AS Sku", "w.City AS City", "p.Quantity AS Quantity")
                            .From("Part p")
                            .Join("Warehouse w", "w.Id = p.WarehouseId")
                            .Where("w.Active = @p1", true)
                            .OrderBy("p.Id"),
                        context.Ct).ConfigureAwait(false);

                    long activeParts = await ScalarAsync(context,
                        "SELECT COUNT(*) FROM Part p INNER JOIN Warehouse w ON w.Id = p.WarehouseId WHERE w.Active = 1")
                        .ConfigureAwait(false);
                    check.Equal(activeParts, joined.Count, "INNER JOIN with a boolean predicate");

                    List<CategoryTotalRow> grouped = await gateway.GetDto<CategoryTotalRow>(
                        new QueryBuilder()
                            .Select("WarehouseId", "SUM(Quantity) AS TotalQuantity", "SUM(UnitPrice) AS TotalValue")
                            .From("Part")
                            .GroupBy("WarehouseId")
                            .OrderBy("WarehouseId"),
                        context.Ct).ConfigureAwait(false);

                    check.Equal(ScenarioSchemaWarehouseCount(), grouped.Count, "GROUP BY row count");
                    check.Note("aggregates must be aliased for the DTO mapper to bind them, which is a mapper " +
                               "requirement rather than a dialect one");

                    List<Dictionary<string, RecordItem>> inRows = await gateway.GetRaw(
                        new QueryBuilder().Select("Id").From("Part").WhereIn("WarehouseId", new object[] { 1, 2 }),
                        context.Ct).ConfigureAwait(false);
                    List<Dictionary<string, RecordItem>> notInRows = await gateway.GetRaw(
                        new QueryBuilder().Select("Id").From("Part").WhereNotIn("WarehouseId", new object[] { 1, 2 }),
                        context.Ct).ConfigureAwait(false);
                    check.Equal(ScenarioSchemaPartCount(), inRows.Count + notInRows.Count,
                        "IN and NOT IN must partition the table");

                    List<Dictionary<string, RecordItem>> between = await gateway.GetRaw(
                        new QueryBuilder().Select("Id").From("Part").WhereBetween("Id", 5, 10), context.Ct)
                        .ConfigureAwait(false);
                    check.Equal(6, between.Count, "BETWEEN 5 AND 10");

                    List<Dictionary<string, RecordItem>> like = await gateway.GetRaw(
                        new QueryBuilder().Select("Id").From("Part").WhereLike("Sku", "SKU-000%"), context.Ct)
                        .ConfigureAwait(false);
                    check.Equal(9, like.Count, "LIKE 'SKU-000%'");

                    // A parameterized predicate authored before a parameterized
                    // subquery: the case that produced silently wrong rows on
                    // Access. SQL Server binds by name, so it must agree with
                    // hand-written SQL here.
                    QueryBuilder subquery = new QueryBuilder()
                        .Select("1")
                        .From("Warehouse w")
                        .Where("w.Id = Part.WarehouseId AND w.City = @p1", "Curitiba");

                    List<Dictionary<string, RecordItem>> exists = await gateway.GetRaw(
                        new QueryBuilder()
                            .Select("Id")
                            .From("Part")
                            .Where("Quantity > @p1", 0)
                            .WhereExists(subquery),
                        context.Ct).ConfigureAwait(false);

                    long expectedExists = await ScalarAsync(context,
                        "SELECT COUNT(*) FROM Part WHERE Quantity > 0 AND EXISTS " +
                        "(SELECT 1 FROM Warehouse w WHERE w.Id = Part.WarehouseId AND w.City = N'Curitiba')")
                        .ConfigureAwait(false);

                    check.Equal(expectedExists, exists.Count, "EXISTS with parameters on both sides");
                    check.Note("named binding means authoring order does not change the answer here; " +
                               "the positional ordering fix that Access needed is invisible to this provider");

                    // Paging: the builder has no OFFSET/FETCH, so this records
                    // what the library can and cannot express rather than
                    // pretending the clause exists.
                    List<Dictionary<string, RecordItem>> page = await gateway.GetRaw(
                        "SELECT Id FROM Part ORDER BY Id OFFSET @p1 ROWS FETCH NEXT @p2 ROWS ONLY",
                        new Dictionary<string, object?> { ["@p1"] = 10, ["@p2"] = 5 },
                        context.Ct).ConfigureAwait(false);

                    check.Equal(5, page.Count, "OFFSET/FETCH page size");
                    check.Equal("11", page[0]["Id"].Value, "OFFSET/FETCH first row");
                    check.Note("OFFSET/FETCH has no QueryBuilder clause; it is reachable only through raw SQL, " +
                               "so builder-driven paging on SQL Server is Top plus OrderBy");

                    for (int i = 0; i < 10; i++) context.Counters.Success();
                });
        }

        private static Task ReaderFailureAndEarlyExit(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "reader-failure-and-early-exit",
                "a failing callback and an abandoned stream both release their resources and report one terminal",
                async check =>
                {
                    var gateway = context.Workspace.Gateway;
                    context.Workspace.ResetTerminals();

                    Exception? failure = await PhaseContext.CaptureAsync(() =>
                        gateway.ReadDto<PartRow>(
                            Builder(),
                            _ => throw new InvalidOperationException("callback refuses row"),
                            context.Ct)).ConfigureAwait(false);

                    check.That(failure != null, "the throwing callback did not surface");
                    check.Note("callback failure surfaced as " + failure!.GetType().Name);
                    context.Counters.ExpectedFailure();

                    // The connection must be back: the very next call proves it
                    // rather than an inspection of internal state.
                    List<Dictionary<string, RecordItem>> after = await gateway
                        .GetRaw("SELECT 1 AS Probe", null, context.Ct).ConfigureAwait(false);
                    check.Equal(1, after.Count, "ordinary work after a failed callback");
                    context.Counters.Success();

#if NET6_0_OR_GREATER
                    context.Workspace.ResetTerminals();

                    int seen = 0;
                    await foreach (PartRow _ in gateway.StreamDto<PartRow>(Builder(), context.Ct).ConfigureAwait(false))
                    {
                        seen++;
                        if (seen == 2) break;
                    }

                    IReadOnlyList<DbQueryStreamOutcome> terminals = context.Workspace.StreamTerminals;
                    check.Equal(1, terminals.Count, "stream terminals after an early exit");
                    check.Equal(
                        DbQueryStreamOutcome.DisposedBeforeCompletion.ToString(),
                        terminals[0].ToString(),
                        "early-exit terminal outcome");
                    context.Counters.Success();

                    context.Workspace.ResetTerminals();

                    int completed = 0;
                    await foreach (PartRow _ in gateway.StreamDto<PartRow>(Builder(), context.Ct).ConfigureAwait(false))
                        completed++;

                    check.Equal(ScenarioSchemaRowsPerWarehouse(), completed, "fully drained stream row count");
                    check.Equal(1, context.Workspace.StreamTerminals.Count, "stream terminals after completion");
                    check.Equal(
                        DbQueryStreamOutcome.Completed.ToString(),
                        context.Workspace.StreamTerminals[0].ToString(),
                        "completed terminal outcome");
                    context.Counters.Success();
#else
                    check.Note("stream terminal outcomes are a net6+ claim; on net481 the streaming gateway " +
                               "is not part of IDatabaseGateway at all");
#endif
                });
        }

        private static async Task ReadDirectlyAsync(PhaseContext context, int id, Action<PartRow> observe)
        {
            // A generous connect timeout, because this connection is the
            // oracle rather than the measurement. The gateway's own connections
            // keep the realistic default; making the comparison connection wait
            // longer costs nothing and stops a loaded machine from reporting a
            // scenario timeout as if the mapping were wrong - which is exactly
            // what happened the first time two workers ran concurrently.
            string connectionString = context.Endpoint.BuildConnectionString(
                context.DatabaseName,
                pooling: false,
                connectTimeoutSeconds: 60,
                applicationName: "NekoLib.E4-SQL.oracle");

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync(context.Ct).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + SelectColumns + " FROM Part WHERE Id = @id";
                    command.Parameters.AddWithValue("@id", id);

                    using (SqlDataReader reader = await command.ExecuteReaderAsync(context.Ct).ConfigureAwait(false))
                    {
                        if (!await reader.ReadAsync(context.Ct).ConfigureAwait(false))
                            throw new InvalidOperationException("the oracle read found no part " + id);

                        observe(new PartRow
                        {
                            Id = reader.GetInt32(0),
                            WarehouseId = reader.GetInt32(1),
                            Sku = reader.GetString(2),
                            Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                            Quantity = reader.GetInt32(4),
                            UnitPrice = reader.GetDecimal(5),
                            Weight = reader.IsDBNull(6) ? (double?)null : reader.GetDouble(6),
                            Serial = reader.GetInt64(7),
                            Discontinued = reader.GetBoolean(8),
                            UpdatedAt = reader.GetDateTime(9)
                        });
                    }
                }
            }
        }

        private static async Task<long> ScalarAsync(PhaseContext context, string sql)
        {
            List<Dictionary<string, RecordItem>> rows = await context.Workspace.Gateway
                .GetRaw(sql, null, context.Ct).ConfigureAwait(false);

            if (rows.Count != 1)
                throw new InvalidOperationException("expected one row from: " + sql);

            return long.Parse(FirstValue(rows), CultureInfo.InvariantCulture);
        }

        private static string FirstValue(List<Dictionary<string, RecordItem>> rows)
        {
            foreach (KeyValuePair<string, RecordItem> column in rows[0])
                return column.Value.Value;

            return "0";
        }

        private static long SumQuantity(List<PartRow> rows)
        {
            long total = 0;
            foreach (PartRow row in rows) total += row.Quantity;
            return total;
        }

        private static KeyValuePair<string, int> Pair(string name, int rows) =>
            new KeyValuePair<string, int>(name, rows);

        private static string Describe(double? value) =>
            value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "null";

        private static int ScenarioSchemaRowsPerWarehouse() =>
            Schema.ScenarioSchema.PartCount / Schema.ScenarioSchema.WarehouseCount;

        private static int ScenarioSchemaPartCount() => Schema.ScenarioSchema.PartCount;

        private static int ScenarioSchemaWarehouseCount() => Schema.ScenarioSchema.WarehouseCount;
    }
}
