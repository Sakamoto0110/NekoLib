#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using NekoLib.Data.Dynamic;
using NekoLib.Data.Internal.Gateway;
using NekoLib.Data.Query;
using NekoLib.Data.RuntimeTests.SqlServer.Model;

namespace NekoLib.Data.RuntimeTests.SqlServer.Workload
{
    /// <summary>
    /// <see cref="DynamicMode.IL"/> against varying row shapes, which is the
    /// one part of Data that had never been executed outside unit coverage.
    /// <para/>
    /// The risk DATA-012 left open is specific: emitted types are process-wide
    /// and cannot be unloaded, so a long-lived process that keeps meeting new
    /// row shapes accumulates types forever. The cap exists to stop that, and
    /// the only way to know what the cap does is to cross it against a real
    /// provider with genuinely different schemas — different column names and
    /// different SQL types, not the same shape carrying different values.
    /// <para/>
    /// The process-wide limit is locked by the first IL use, so this phase runs
    /// last and reports the emitted-type count truthfully. Nothing here claims
    /// that generated types are ever collected, because they are not.
    /// </summary>
    internal static class DynamicLifetime
    {
        private const string Phase = "dynamic";

        /// <summary>
        /// Deliberately small. The cap is process-wide and permanent, so the
        /// boundary has to be reachable in one run without emitting a hundred
        /// types into a process that then has to keep them.
        /// </summary>
        private const int SchemaLimit = 12;

        public static async Task RunAsync(PhaseContext context)
        {
            DynamicIlMetrics baseline = DatabaseGateway.GetDynamicIlMetrics();

            await BelowTheCap(context, baseline).ConfigureAwait(false);
            await AtAndBeyondTheCap(context).ConfigureAwait(false);
            await FallbackBeyondTheCap(context).ConfigureAwait(false);
            await OrdinaryWorkAfterTheBoundary(context).ConfigureAwait(false);
        }

        private static Task BelowTheCap(PhaseContext context, DynamicIlMetrics baseline)
        {
            return context.Runner.RunAsync(Phase, "il-below-the-cap",
                "each new row shape emits exactly one type and repeats reuse it",
                async check =>
                {
                    check.Note("before any IL use: limit=" + baseline.SchemaLimit +
                               " emitted=" + baseline.EmittedSchemaCount +
                               " hits=" + baseline.CacheHits + " misses=" + baseline.CacheMisses);

                    using (GatewayWorkspace il = CreateIlWorkspace(context, failOnLimit: true, allowExpando: false))
                    {
                        const int shapes = 8;

                        for (int i = 0; i < shapes; i++)
                        {
                            List<DynamicRow> rows = await il.Gateway
                                .GetDynamic(Shape(i), context.Ct).ConfigureAwait(false);

                            check.That(rows.Count > 0, "shape " + i + " returned no rows");
                            context.Counters.Success();
                        }

                        DynamicIlMetrics afterFirstPass = DatabaseGateway.GetDynamicIlMetrics();
                        check.Equal(SchemaLimit, afterFirstPass.SchemaLimit,
                            "the process-wide schema limit this context asked for");
                        check.Equal(shapes, afterFirstPass.EmittedSchemaCount,
                            "emitted types after " + shapes + " distinct shapes");

                        // Running the same shapes again must cost nothing: a
                        // cache miss here would mean re-emission, which is the
                        // failure mode the cap exists to prevent.
                        long missesBefore = afterFirstPass.CacheMisses;

                        for (int i = 0; i < shapes; i++)
                        {
                            await il.Gateway.GetDynamic(Shape(i), context.Ct).ConfigureAwait(false);
                            context.Counters.Success();
                        }

                        DynamicIlMetrics afterSecondPass = DatabaseGateway.GetDynamicIlMetrics();
                        check.Equal(shapes, afterSecondPass.EmittedSchemaCount,
                            "emitted types after repeating the same shapes");
                        check.Equal(missesBefore, afterSecondPass.CacheMisses,
                            "cache misses caused by repeating a known shape");

                        check.Note("after the repeat pass: emitted=" + afterSecondPass.EmittedSchemaCount +
                                   " hits=" + afterSecondPass.CacheHits +
                                   " misses=" + afterSecondPass.CacheMisses);

                        context.Sampler.Take(Phase, "dynamic-warm-up");
                    }
                });
        }

        private static Task AtAndBeyondTheCap(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "il-at-and-beyond-the-cap",
                "the cap stops emission and a new shape past it fails instead of re-emitting",
                async check =>
                {
                    using (GatewayWorkspace il = CreateIlWorkspace(context, failOnLimit: true, allowExpando: false))
                    {
                        for (int i = 8; i < SchemaLimit; i++)
                        {
                            await il.Gateway.GetDynamic(Shape(i), context.Ct).ConfigureAwait(false);
                            context.Counters.Success();
                        }

                        DynamicIlMetrics atCap = DatabaseGateway.GetDynamicIlMetrics();
                        check.Equal(SchemaLimit, atCap.EmittedSchemaCount, "emitted types at the cap");
                        context.Sampler.Take(Phase, "dynamic-at-cap");

                        Exception? failure = await PhaseContext.CaptureAsync(() =>
                            il.Gateway.GetDynamic(Shape(SchemaLimit + 1), context.Ct)).ConfigureAwait(false);

                        check.That(failure is InvalidOperationException,
                            "expected InvalidOperationException past the cap, got " +
                            (failure == null ? "no exception" : failure.GetType().Name));

                        check.Note("past the cap: " + failure!.Message);
                        context.Counters.ExpectedFailure();

                        DynamicIlMetrics rejected = DatabaseGateway.GetDynamicIlMetrics();
                        check.Equal(SchemaLimit, rejected.EmittedSchemaCount,
                            "emitted types after a rejected shape");
                        check.That(rejected.LimitRejections >= 1,
                            "the rejection was not counted");

                        check.Note("rejections=" + rejected.LimitRejections +
                                   " emitted=" + rejected.EmittedSchemaCount +
                                   " (emitted types are process-wide and are never unloaded, " +
                                   "so this number can only grow)");

                        // A shape already emitted must still work after the cap
                        // is reached; the cap bounds emission, not use.
                        List<DynamicRow> known = await il.Gateway.GetDynamic(Shape(0), context.Ct)
                            .ConfigureAwait(false);
                        check.That(known.Count > 0, "a known shape stopped working after the cap");
                        context.Counters.Success();
                    }
                });
        }

        private static Task FallbackBeyondTheCap(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "il-fallback-beyond-the-cap",
                "a context that permits fallback answers a new shape with Expando instead of failing",
                async check =>
                {
                    using (GatewayWorkspace il = CreateIlWorkspace(context, failOnLimit: false, allowExpando: true))
                    {
                        DynamicIlMetrics before = DatabaseGateway.GetDynamicIlMetrics();

                        List<DynamicRow> rows = await il.Gateway
                            .GetDynamic(Shape(SchemaLimit + 2), context.Ct).ConfigureAwait(false);

                        check.That(rows.Count > 0, "the fallback shape returned no rows");
                        context.Counters.Success();

                        DynamicIlMetrics after = DatabaseGateway.GetDynamicIlMetrics();
                        check.Equal(before.EmittedSchemaCount, after.EmittedSchemaCount,
                            "the fallback must not emit a new type");

                        check.Note("the same context option that fails one caller lets another degrade to Expando; " +
                                   "the process-wide limit stays " + after.SchemaLimit +
                                   " because it was locked by the first IL use and per-context options cannot " +
                                   "reconfigure it");
                    }
                });
        }

        private static Task OrdinaryWorkAfterTheBoundary(PhaseContext context)
        {
            return context.Runner.RunAsync(Phase, "work-continues-after-the-boundary",
                "ordinary dynamic and typed queries still succeed once the cap has been crossed",
                async check =>
                {
                    List<DynamicRow> expando = await context.Workspace.Gateway
                        .GetDynamic(Shape(3), context.Ct).ConfigureAwait(false);
                    check.That(expando.Count > 0, "the Expando path stopped returning rows");
                    context.Counters.Success();

                    List<PartRow> typed = await context.Workspace.Gateway.GetDto<PartRow>(
                        "SELECT Id, WarehouseId, Sku, Description, Quantity, UnitPrice, Weight, Serial, " +
                        "Discontinued, UpdatedAt FROM Part",
                        null,
                        context.Ct).ConfigureAwait(false);

                    check.Equal(Schema.ScenarioSchema.PartCount, typed.Count, "typed rows after the boundary");
                    context.Counters.Success();

                    DynamicIlMetrics final = DatabaseGateway.GetDynamicIlMetrics();
                    context.Sampler.Take(Phase, "dynamic-final");

                    check.Note("final IL metrics: limit=" + final.SchemaLimit +
                               " emitted=" + final.EmittedSchemaCount +
                               " hits=" + final.CacheHits +
                               " misses=" + final.CacheMisses +
                               " rejections=" + final.LimitRejections);

                    check.Note("the emitted count never fell during this run, and no claim is made that it can: " +
                               "Reflection.Emit types live in a non-collectible assembly for the life of the process");
                });
        }

        private static GatewayWorkspace CreateIlWorkspace(
            PhaseContext context,
            bool failOnLimit,
            bool allowExpando)
        {
            DatabaseGatewayOptions options = GatewayWorkspace.DefaultOptions();
            options.DynamicMode = allowExpando ? (DynamicMode.IL | DynamicMode.Expando) : DynamicMode.IL;
            options.MaxDynamicSchemas = SchemaLimit;
            options.FailOnDynamicSchemaLimit = failOnLimit;
            options.AllowExpandoFallback = allowExpando;

            return new GatewayWorkspace(
                context.Endpoint.BuildConnectionString(
                    context.DatabaseName,
                    applicationName: "NekoLib.E4-SQL.dynamic"),
                options);
        }

        /// <summary>
        /// Row shape number <paramref name="index"/>: a distinct set of column
        /// names and SQL types, so each one is a genuinely new schema.
        /// <para/>
        /// Varying only the values under one shape would leave the cap
        /// unapproached, which is precisely the gap this phase exists to close.
        /// The shapes are generated rather than listed so the count can move
        /// with the cap, and no database object is created for any of them.
        /// </summary>
        private static QueryBuilder Shape(int index)
        {
            string suffix = index.ToString("D3", CultureInfo.InvariantCulture);

            // Rotating the projected type as well as the alias means two shapes
            // differ in more than their names, which is what a real application
            // meeting new queries looks like.
            switch (index % 4)
            {
                case 0:
                    return new QueryBuilder()
                        .Select("Id AS Key" + suffix, "Sku AS Text" + suffix)
                        .From("Part")
                        .Top(3);
                case 1:
                    return new QueryBuilder()
                        .Select(
                            "CAST(Id AS bigint) AS Big" + suffix,
                            "CAST(Quantity AS decimal(18,4)) AS Money" + suffix,
                            "Sku AS Label" + suffix)
                        .From("Part")
                        .Top(3);
                case 2:
                    return new QueryBuilder()
                        .Select(
                            "CAST(Discontinued AS bit) AS Flag" + suffix,
                            "UpdatedAt AS Moment" + suffix)
                        .From("Part")
                        .Top(3);
                default:
                    return new QueryBuilder()
                        .Select(
                            "Weight AS Mass" + suffix,
                            "Description AS Notes" + suffix,
                            "WarehouseId AS Home" + suffix,
                            "Serial AS Tag" + suffix)
                        .From("Part")
                        .Top(3);
            }
        }
    }
}
