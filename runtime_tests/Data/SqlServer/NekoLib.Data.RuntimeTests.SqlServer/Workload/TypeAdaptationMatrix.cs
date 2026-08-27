#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NekoLib.Data.Dynamic;
using NekoLib.Data.Gateway;
using NekoLib.Data.Query;

namespace NekoLib.Data.RuntimeTests.SqlServer.Workload
{
    /// <summary>
    /// Proves schema-validated write promotion and explicit lossy DTO
    /// materialization against the real SQL Server provider. Each structural
    /// hook reports one logical adaptation without exposing the value.
    /// </summary>
    internal static class TypeAdaptationMatrix
    {
        private const string Phase = "type-adaptation";

        public static async Task RunAsync(PhaseContext context)
        {
            await context.Runner.RunAsync(
                Phase,
                "schema-validated-string-to-int",
                "lazy schema discovery authorizes one lossless string-to-int write promotion",
                async check =>
                {
                    DatabaseGateway gateway = (DatabaseGateway)context.Workspace.Gateway;
                    List<Dictionary<string, RecordItem>> before = await gateway.GetRaw(
                        new QueryBuilder()
                            .Select("Quantity")
                            .From("Part")
                            .WhereTrusted("Id = @p1", 1),
                        context.Ct).ConfigureAwait(false);

                    check.Equal(1, before.Count, "rows for Part 1 before the probe");
                    string originalQuantity = before[0]["Quantity"].Value;

                    var adaptations = new List<TypeAdaptationEventArgs>();
                    Action<TypeAdaptationEventArgs> observer = adaptations.Add;
                    TypePromotionPolicy previousPolicy = context.Workspace.Options.TypePromotionPolicy;
                    gateway.OnTypeAdaptation += observer;

                    try
                    {
                        context.Workspace.Options.TypePromotionPolicy =
                            TypePromotionPolicy.SchemaValidated;
                        gateway.ClearSchemaCache();

                        int affected = await gateway.Update(
                            new QueryBuilder()
                                .Update("Part")
                                .Set("Quantity", originalQuantity)
                                .WhereTrusted("Id = @p1", 1),
                            context.Ct).ConfigureAwait(false);

                        check.Equal(1, affected, "rows updated by the promoted value");
                        check.Equal(1, adaptations.Count, "logical adaptations reported");

                        TypeAdaptationEventArgs adaptation = adaptations[0];
                        check.That(
                            adaptation.Direction == TypeAdaptationDirection.Write,
                            "direction is Write");
                        check.That(
                            adaptation.Kind == TypeAdaptationKind.Promotion,
                            "kind is Promotion");
                        check.That(
                            adaptation.ReasonCode == TypeAdaptationReasonCode.SchemaValidatedRule,
                            "reason is SchemaValidatedRule");
                        check.That(
                            adaptation.Loss == TypeAdaptationLoss.Lossless,
                            "loss classification is Lossless");
                        check.That(adaptation.SourceType == typeof(string), "source type is String");
                        check.That(adaptation.TargetType == typeof(int), "target type is Int32");

                        List<Dictionary<string, RecordItem>> after = await gateway.GetRaw(
                            new QueryBuilder()
                                .Select("Quantity")
                                .From("Part")
                                .WhereTrusted("Id = @p1", 1),
                            context.Ct).ConfigureAwait(false);

                        check.Equal(1, after.Count, "rows for Part 1 after the probe");
                        check.Equal(
                            originalQuantity,
                            after[0]["Quantity"].Value,
                            "stored quantity after the probe");
                        check.Note(
                            adaptation.SourceType.Name + "->" + adaptation.TargetType.Name +
                            " / " + adaptation.ReasonCode);
                        context.Counters.Success();
                    }
                    finally
                    {
                        context.Workspace.Options.TypePromotionPolicy = previousPolicy;
                        gateway.OnTypeAdaptation -= observer;
                        gateway.ClearSchemaCache();
                    }
                }).ConfigureAwait(false);

            await context.Runner.RunAsync(
                Phase,
                "explicit-lossy-datetime-read",
                "a DTO-property rule authorizes and reports DateTime to DateTimeOffset materialization",
                async check =>
                {
                    DatabaseGateway gateway = (DatabaseGateway)context.Workspace.Gateway;
                    var adaptations = new List<TypeAdaptationEventArgs>();
                    Action<TypeAdaptationEventArgs> observer = adaptations.Add;
                    TypeLossPolicy previousLossPolicy = context.Workspace.Options.TypeLossPolicy;
                    ReadTypeAdaptationRule binding =
                        ReadTypeAdaptationRule.For<TemporalReadRow>(
                            nameof(TemporalReadRow.UpdatedAt),
                            TypeMaterializations.DateTimeToDateTimeOffsetUsingKind);

                    context.Workspace.Options.ReadTypeAdaptationRules.Add(binding);
                    gateway.OnTypeAdaptation += observer;
                    try
                    {
                        context.Workspace.Options.TypeLossPolicy =
                            TypeLossPolicy.AllowExplicitAndReport;

                        List<TemporalReadRow> rows = await gateway.GetDto<TemporalReadRow>(
                            new QueryBuilder()
                                .Select("UpdatedAt")
                                .From("Part")
                                .WhereTrusted("Id = @p1", 1),
                            context.Ct).ConfigureAwait(false);

                        check.Equal(1, rows.Count, "rows for the temporal read probe");
                        check.That(
                            rows[0].UpdatedAt != default(DateTimeOffset),
                            "DateTimeOffset materialized");
                        check.Equal(1, adaptations.Count, "read adaptations reported");

                        TypeAdaptationEventArgs adaptation = adaptations[0];
                        check.That(
                            adaptation.Direction == TypeAdaptationDirection.Read,
                            "direction is Read");
                        check.That(
                            adaptation.Kind == TypeAdaptationKind.Materialization,
                            "kind is Materialization");
                        check.That(
                            adaptation.ReasonCode == TypeAdaptationReasonCode.ExplicitRule,
                            "reason is ExplicitRule");
                        check.That(
                            adaptation.Loss == TypeAdaptationLoss.PotentiallyLossy,
                            "loss classification is PotentiallyLossy");
                        check.That(
                            adaptation.PropertyName == nameof(TemporalReadRow.UpdatedAt),
                            "property identity is reported");
                        check.Note(
                            adaptation.SourceType.Name + "->" + adaptation.TargetType.Name +
                            " / " + adaptation.ReasonCode);
                        context.Counters.Success();
                    }
                    finally
                    {
                        context.Workspace.Options.TypeLossPolicy = previousLossPolicy;
                        context.Workspace.Options.ReadTypeAdaptationRules.Remove(binding);
                        gateway.OnTypeAdaptation -= observer;
                    }
                }).ConfigureAwait(false);
        }

        private sealed class TemporalReadRow
        {
            public DateTimeOffset UpdatedAt { get; set; }
        }
    }
}
