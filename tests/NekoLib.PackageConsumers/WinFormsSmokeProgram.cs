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
                typeof(NekoLib.Core.Logging.ILogger),
                typeof(NekoLib.Data.Query.DatabaseQuery),
                typeof(NekoLib.Data.Gateway.DatabaseGateway),
                typeof(NekoLib.Data.Gateway.IDatabaseGateway),
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

        private static void CompileDataSurface(
            NekoLib.Data.Gateway.IDatabaseGateway gateway,
            NekoLib.Data.DbSession session,
            System.Threading.CancellationToken cancellationToken)
        {
            var parameters = new System.Collections.Generic.Dictionary<string, object>
            {
                { "@p1", 1 }
            };
            var query = new NekoLib.Data.Query.QueryBuilder()
                .Select("Id")
                .From("Rows")
                .Where("Id = @p1", 1);

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

#if NET9_0_OR_GREATER
            _ = gateway.StreamDto<PackageDataRow>(query, session, cancellationToken);
#endif
        }

        private sealed class PackageDataRow
        {
            public int Id { get; set; }
        }
    }
}
