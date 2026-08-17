#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Data.Dynamic;
using NekoLib.Data.Gateway;
using NekoLib.Data.Query;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.Model;
using NekoLib.Data.RuntimeTests.FarmDatabase.Core.Providers;

namespace NekoLib.Data.RuntimeTests.FarmDatabase.Core
{
    /// <summary>What one read shape returned.</summary>
    public sealed class ReadShapeResult
    {
        public ReadShapeResult(string name, int rows, string note = "")
        {
            Name = name;
            Rows = rows;
            Note = note;
        }

        public string Name { get; }
        public int Rows { get; }
        public string Note { get; }
    }

    /// <summary>
    /// Runs one query through every way the gateway offers of reading it.
    /// <para/>
    /// The scenario had used three of these and ignored the rest: the callback
    /// overloads, the dynamic path and <c>ContainsData</c> had no
    /// coverage at all. Rather than testing each in isolation, they are all pointed at
    /// the same query — <b>every shape must agree on the number of rows</b>. A shape
    /// that disagrees is either mapping differently or losing rows, and both matter.
    /// </summary>
    public sealed partial class FarmDb
    {
        private const string ShapeSql =
            "SELECT [Id], [Name], [Category], [Unit], [Quantity], [UnitPrice] " +
            "FROM [Products] WHERE [Quantity] > @p1";

        private const int ShapeThreshold = 50;

        private static QueryBuilder ShapeBuilder() =>
            new QueryBuilder()
                .Select("[Id]", "[Name]", "[Category]", "[Unit]", "[Quantity]", "[UnitPrice]")
                .From("[Products]")
                .Where("[Quantity] > @p1", ShapeThreshold);

        private static Dictionary<string, object?> ShapeParameters() =>
            new Dictionary<string, object?> { ["@p1"] = ShapeThreshold };

        /// <summary>
        /// Executes every read shape and reports what each returned. Session-bound
        /// overloads share one session so session affinity is exercised too.
        /// </summary>
        public async Task<List<ReadShapeResult>> ReadShapesAsync(CancellationToken ct = default)
        {
            var results = new List<ReadShapeResult>();

            // --- existence ------------------------------------------------
            bool any = await Gateway.ContainsData(
                "SELECT [Id] FROM [Products] WHERE [Quantity] > @p1",
                ShapeParameters(),
                ct).ConfigureAwait(false);
            results.Add(new ReadShapeResult("ContainsData", any ? 1 : 0, any ? "true" : "false"));

            bool none = await Gateway.ContainsData(
                "SELECT [Id] FROM [Products] WHERE [Quantity] > 100000", ct).ConfigureAwait(false);
            results.Add(new ReadShapeResult("ContainsData(vazio)", none ? 1 : 0,
                none ? "true (deveria ser false)" : "false"));

            // --- raw ------------------------------------------------------
            results.Add(new ReadShapeResult("GetRaw(sql)",
                (await Gateway.GetRaw(ShapeSql, ShapeParameters(), ct).ConfigureAwait(false)).Count));

            results.Add(new ReadShapeResult("GetRaw(builder)",
                (await Gateway.GetRaw(ShapeBuilder(), ct).ConfigureAwait(false)).Count));

            int rawCallback = 0;
            await Gateway.ReadRaw(ShapeBuilder(), _ => rawCallback++, ct).ConfigureAwait(false);
            results.Add(new ReadShapeResult("ReadRaw(builder)", rawCallback));

            // --- typed ----------------------------------------------------
            results.Add(new ReadShapeResult("GetDto(sql)",
                (await Gateway.GetDto<Product>(ShapeSql, ShapeParameters(), ct).ConfigureAwait(false)).Count));

            results.Add(new ReadShapeResult("GetDto(builder)",
                (await Gateway.GetDto<Product>(ShapeBuilder(), ct).ConfigureAwait(false)).Count));

            int dtoCallback = 0;
            double dtoTotal = 0;
            await Gateway.ReadDto<Product>(ShapeBuilder(), p =>
            {
                dtoCallback++;
                dtoTotal += p.Quantity;
            }, ct).ConfigureAwait(false);
            results.Add(new ReadShapeResult("ReadDto(builder)", dtoCallback,
                "soma qtd=" + dtoTotal));

            // --- dynamic --------------------------------------------------
            List<DynamicRow> dynamicRows =
                await Gateway.GetDynamic(ShapeBuilder(), ct).ConfigureAwait(false);
            results.Add(new ReadShapeResult("GetDynamic", dynamicRows.Count));

            int dynamicCallback = 0;
            await Gateway.ReadDynamic(ShapeBuilder(), _ => dynamicCallback++, ct).ConfigureAwait(false);
            results.Add(new ReadShapeResult("ReadDynamic", dynamicCallback));

            // --- the same shapes bound to one session ---------------------
            using (DbSession session = await Gateway.OpenSessionAsync(ct).ConfigureAwait(false))
            {
                results.Add(new ReadShapeResult("GetRaw(sessao)",
                    (await Gateway.GetRaw(ShapeSql, ShapeParameters(), session, ct)
                        .ConfigureAwait(false)).Count));

                results.Add(new ReadShapeResult("GetDto(sessao)",
                    (await Gateway.GetDto<Product>(ShapeBuilder(), session, ct)
                        .ConfigureAwait(false)).Count));

                results.Add(new ReadShapeResult("GetDynamic(sessao)",
                    (await Gateway.GetDynamic(ShapeBuilder(), session, ct)
                        .ConfigureAwait(false)).Count));

                int sessionCallback = 0;
                await Gateway.ReadDto<Product>(ShapeBuilder(), _ => sessionCallback++, session, ct)
                    .ConfigureAwait(false);
                results.Add(new ReadShapeResult("ReadDto(sessao)", sessionCallback));
            }

#if NET6_0_OR_GREATER
            // --- streaming, net6+ only ------------------------------------
            int streamRaw = 0;
            await foreach (Dictionary<string, RecordItem> _ in
                Gateway.StreamRaw(ShapeSql, ShapeParameters(), ct).ConfigureAwait(false))
                streamRaw++;
            results.Add(new ReadShapeResult("StreamRaw", streamRaw));

            int streamDto = 0;
            await foreach (Product _ in
                Gateway.StreamDto<Product>(ShapeBuilder(), ct).ConfigureAwait(false))
                streamDto++;
            results.Add(new ReadShapeResult("StreamDto", streamDto));

            int streamDynamic = 0;
            await foreach (DynamicRow _ in
                Gateway.StreamDynamic(ShapeBuilder(), ct).ConfigureAwait(false))
                streamDynamic++;
            results.Add(new ReadShapeResult("StreamDynamic", streamDynamic));
#endif

            return results;
        }
    }
}
