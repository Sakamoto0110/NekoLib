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
    /// Proves that schema-validated write promotion is active against the real
    /// SQL Server provider and that its structural hook reports exactly one
    /// logical adaptation without exposing the value.
    /// </summary>
    internal static class TypeAdaptationMatrix
    {
        private const string Phase = "type-adaptation";

        public static Task RunAsync(PhaseContext context)
        {
            return context.Runner.RunAsync(
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
                });
        }
    }
}
