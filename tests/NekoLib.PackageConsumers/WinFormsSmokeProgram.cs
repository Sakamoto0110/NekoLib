using System;

namespace NekoLib.PackageConsumers
{
    internal static class WinFormsSmokeProgram
    {
        [STAThread]
        private static void Main()
        {
            var packageTypes = new[]
            {
                typeof(NekoLib.Core.Disposable),
                typeof(NekoLib.Core.Inspection.IInspectionRecorder),
                typeof(NekoLib.Core.Logging.ILogger),
                typeof(NekoLib.Core.Telemetry.ITelemetry),
                typeof(NekoLib.Data.Query.DatabaseQuery),
                typeof(NekoLib.Data.Gateway.DatabaseGateway),
                typeof(NekoLib.Data.Gateway.IDatabaseGateway),
                typeof(NekoLib.Data.TypePromotionPolicy),
                typeof(NekoLib.Data.TypeMaterializationRule),
                typeof(NekoLib.Data.ReadTypeAdaptationRule),
                typeof(NekoLib.Data.TypeAdaptationEventArgs),
                typeof(NekoLib.Inspection.InspectionRuntime),
                typeof(NekoLib.Devices.Core.Abstractions.SerialConfig),
                typeof(NekoLib.Diagnostics.CrashHandler),
                typeof(NekoLib.Diagnostics.Windows.WindowsCrash),
                typeof(NekoLib.Http.HttpApiClient),
                typeof(NekoLib.Logging.Logger),
                typeof(NekoLib.Mvvm.ViewModelBase),
                typeof(NekoLib.Navigation.Bootstrap.PageNavBootstrap),
                typeof(NekoLib.Navigation.WinForms.Adapters.WinFormsPlatformAdapter),
                typeof(NekoLib.Pipes.PipeClient),
                typeof(NekoLib.Telemetry.TelemetryPipeline),
                typeof(NekoLib.Watchdog.WatchdogOptions)
            };

            Console.WriteLine("Loaded {0} NekoLib package surfaces.", packageTypes.Length);
        }

        private static void CompileCoreSurface(
            NekoLib.Core.Logging.ILogSink logSink,
            NekoLib.Core.Telemetry.ITelemetrySink telemetrySink,
            NekoLib.Core.Inspection.IInspectionSnapshotSource inspectionSource)
        {
            var logEntry = new NekoLib.Core.Logging.LogEntry(
                DateTime.UtcNow,
                NekoLib.Core.Logging.LogLevel.Info,
                "package-consumer");
            logSink.Write(logEntry);

            var operation = new NekoLib.Core.Telemetry.TelemetryOperation(
                DateTime.UtcNow,
                "PackageConsumer",
                "compile",
                "operation",
                null,
                NekoLib.Core.Telemetry.TelemetryOutcome.Succeeded,
                TimeSpan.Zero);
            telemetrySink.Write(operation);

            _ = inspectionSource.CaptureSnapshot(0, TimeSpan.Zero);
            _ = NekoLib.Core.Logging.NullLogger.Instance;
            _ = NekoLib.Core.Telemetry.NullTelemetry.Instance;
            _ = NekoLib.Core.Inspection.NullInspection.Instance;
        }

        private static void CompileDataSurface(
            NekoLib.Data.Gateway.IDatabaseGateway gateway,
            NekoLib.Data.DbSession session,
            System.Threading.CancellationToken cancellationToken)
        {
            var parameters = new System.Collections.Generic.Dictionary<string, object?>
            {
                { "@p1", 1 }
            };
            var query = new NekoLib.Data.Query.QueryBuilder()
                .Select("Id")
                .From("Rows")
                .Where(
                    "Id",
                    NekoLib.Data.Query.QueryOperator.Equal,
                    1);

            _ = gateway.ContainsData(
                "SELECT Id FROM Rows WHERE Id = @p1",
                parameters,
                cancellationToken);
            _ = gateway.GetDto<PackageDataRow>(query, session, cancellationToken);
            _ = gateway.Insert(
                "INSERT INTO Rows (Id) VALUES (@p1)",
                parameters,
                session,
                cancellationToken);
            _ = gateway.Delete(
                new NekoLib.Data.Query.QueryBuilder()
                    .DeleteFrom("Rows")
                    .Where(
                        "Id",
                        NekoLib.Data.Query.QueryOperator.Equal,
                        1),
                session,
                cancellationToken);

#if NET9_0_OR_GREATER
            _ = gateway.StreamDto<PackageDataRow>(query, session, cancellationToken);
#endif
        }

        private static void CompileDataAdaptationSurface(
            NekoLib.Data.Gateway.DatabaseGateway gateway,
            System.Threading.CancellationToken cancellationToken)
        {
            gateway.OnTypeAdaptation += adaptation =>
                Console.WriteLine(
                    "{0}:{1}:{2}:{3}",
                    adaptation.Kind,
                    adaptation.ReasonCode,
                    adaptation.Loss,
                    adaptation.PropertyName);

            NekoLib.Data.TypeDecayRule formattedFallback =
                NekoLib.Data.TypeDecays.CreateDateTimeOffsetToString(
                    "yyyy/MM/dd HH:mm:ss:fff",
                    System.Globalization.CultureInfo.InvariantCulture);
            NekoLib.Data.ReadTypeAdaptationRule readFallback =
                NekoLib.Data.ReadTypeAdaptationRule.For<PackageDataRow>(
                    nameof(PackageDataRow.OccurredAt),
                    NekoLib.Data.TypeMaterializations.DateTimeOffsetToUtcDateTime);
            NekoLib.Data.DatabaseGatewayOptions readOptions =
                new NekoLib.Data.DatabaseGatewayOptions
                {
                    TypeLossPolicy = NekoLib.Data.TypeLossPolicy.AllowExplicitAndReport
                };
            readOptions.ReadTypeAdaptationRules.Add(readFallback);
            _ = readOptions.AutomaticMaterializationRules.Count;

            _ = gateway.PreloadSchemaAsync(
                "Rows",
                new[] { "Id", "OccurredAt" },
                cancellationToken);
            _ = gateway.RefreshSchemaAsync(
                "Rows",
                new[] { "Id" },
                cancellationToken);
            gateway.ClearSchemaCache();

            _ = new NekoLib.Data.Query.QueryBuilder()
                .InsertInto("Rows")
                .Value(
                    "Id",
                    "54",
                    parameter => parameter.AllowPromotion(
                        NekoLib.Data.TypePromotions.StringToInt32))
                .Value(
                    "OccurredAt",
                    DateTimeOffset.UtcNow,
                    parameter => parameter
                        .AllowDecay(NekoLib.Data.TypeDecays.DateTimeOffsetToUtcDateTime)
                        .AllowDecayFallback(formattedFallback));
        }

#pragma warning disable CS0618
        private static void CompileDeprecatedDataSurface()
        {
            _ = new NekoLib.Data.Query.QueryBuilder().InsertInto(
                "Rows",
                new System.Collections.Generic.Dictionary<string, object?>
                {
                    { "Id", 1 }
                });
            _ = new NekoLib.Data.Query.QueryBuilder().Update(
                "Rows",
                new System.Collections.Generic.Dictionary<string, object?>
                {
                    { "Id", 1 }
                });
            _ = new NekoLib.Data.Query.QueryBuilder()
                .Select("Id")
                .From("Rows")
                .Join("OtherRows", "OtherRows.Id = Rows.Id")
                .Where("Rows.Id = @p1", 1);
        }
#pragma warning restore CS0618

        private sealed class PackageDataRow
        {
            public int Id { get; set; }
            public DateTime OccurredAt { get; set; }
        }
    }
}
