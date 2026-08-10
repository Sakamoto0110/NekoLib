#nullable enable
using System.Collections.Generic;
using NekoLib.Data.RuntimeTests.SqlServer.Container;
using NekoLib.Data.RuntimeTests.SqlServer.Server;
using NekoLib.Data.RuntimeTests.SqlServer.Workload;
using NekoLib.RuntimeTests.Harness;
using NekoLib.RuntimeTests.Harness.Reporting;

namespace NekoLib.Data.RuntimeTests.SqlServer
{
    /// <summary>
    /// The sample columns only this scenario can fill.
    /// <para/>
    /// These used to be fixed fields in the shared sampler, which was a
    /// boundary error the second consumer exposed: E3-OBS has neither
    /// connections nor server sessions. The count of connections the factory was
    /// asked to build is the bounded quantity that matters here, because a run
    /// that ends having built far more than it opened has leaked one.
    /// </summary>
    internal sealed class SqlServerSamples : IScenarioSamples
    {
        private static readonly string[] Columns = { "connections_created" };

        private readonly GatewayWorkspace _workspace;

        public SqlServerSamples(GatewayWorkspace workspace)
        {
            _workspace = workspace;
        }

        public static IReadOnlyList<string> ColumnNamesForHeader => Columns;

        public IReadOnlyList<string> ColumnNames => Columns;

        public long[] Read() => new[] { _workspace.Factory.Created };
    }

    /// <summary>
    /// The versions and identifiers this scenario adds to the shared result
    /// record. The harness writes the document; what counts as "the provider"
    /// and "the server" is knowledge only this scenario has.
    /// </summary>
    internal sealed class SqlServerSummary : IScenarioSummary
    {
        private readonly ServerFacts _server;
        private readonly ContainerFacts? _container;
        private readonly string _database;

        public SqlServerSummary(ServerFacts server, ContainerFacts? container, string database)
        {
            _server = server;
            _container = container;
            _database = database;
        }

        public IReadOnlyList<KeyValuePair<string, string>> Facts => new[]
        {
            new KeyValuePair<string, string>("Provider", ScenarioFacts.ProviderVersion),
            new KeyValuePair<string, string>("Library", ScenarioFacts.LibraryVersion),
            new KeyValuePair<string, string>("Server", _server.ProductVersion + " " + _server.Edition),
            new KeyValuePair<string, string>(
                "Image digest",
                _container == null ? "unknown" : _container.ImageDigest)
        };

        public void WriteJson(JsonWriter json)
        {
            json.Prop("provider", ScenarioFacts.ProviderVersion);
            json.Prop("serverProductVersion", _server.ProductVersion);
            json.Prop("serverEdition", _server.Edition);
            json.Prop("imageDigest", _container == null ? string.Empty : _container.ImageDigest);
            json.Prop("database", _database);
        }
    }
}
