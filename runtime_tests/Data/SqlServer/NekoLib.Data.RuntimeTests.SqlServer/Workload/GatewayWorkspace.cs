#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using NekoLib.Data.Connection;
using NekoLib.Data.Gateway;
using NekoLib.Data.Query;
using NekoLib.Data.RuntimeTests.SqlServer.Server;

namespace NekoLib.Data.RuntimeTests.SqlServer.Workload
{
    /// <summary>
    /// One composed gateway: factory, execution context, and the lifecycle
    /// events the checks assert against.
    /// <para/>
    /// The event counters are the whole reason this is a type rather than three
    /// locals. Several of the specification's requirements — "assert one
    /// cancellation/error lifecycle terminal and no success terminal", "provider
    /// error propagation without duplicate query lifecycle terminals", "exactly
    /// one terminal per stream" — cannot be checked from the return value of a
    /// call. They are only visible from the outside, through the context's own
    /// notifications, which is where they are counted here.
    /// </summary>
    internal sealed class GatewayWorkspace : IDisposable
    {
        private int _generated;
        private int _dispatched;
        private int _succeeded;
        private int _failed;
        private readonly List<DbQueryStreamOutcome> _streamTerminals = new List<DbQueryStreamOutcome>();
        private readonly object _sync = new object();
        private bool _disposed;

        public GatewayWorkspace(
            string connectionString,
            DatabaseGatewayOptions options,
            DbConnectionFactoryOwnership ownership = DbConnectionFactoryOwnership.ContextOwned)
        {
            ConnectionString = connectionString;
            Factory = new SqlServerConnectionFactory(connectionString);
            Options = options;

            Context = new QueryExecutionContext(
                Factory,
                new SqlServerQueryTranslator(),
                options,
                ownership);

            Context.OnSqlGenerated += _ => Interlocked.Increment(ref _generated);
            Context.OnSqlDispatch += _ => Interlocked.Increment(ref _dispatched);
            Context.OnSuccess += _ => Interlocked.Increment(ref _succeeded);
            Context.OnError += _ => Interlocked.Increment(ref _failed);
            Context.OnStreamTerminal += args =>
            {
                lock (_sync) _streamTerminals.Add(args.Outcome);
            };

            Gateway = new DatabaseGateway(Context);
        }

        public string ConnectionString { get; }
        public SqlServerConnectionFactory Factory { get; }
        public QueryExecutionContext Context { get; }
        public DatabaseGatewayOptions Options { get; }
        public IDatabaseGateway Gateway { get; }

        public int Generated => Volatile.Read(ref _generated);
        public int Dispatched => Volatile.Read(ref _dispatched);
        public int Succeeded => Volatile.Read(ref _succeeded);
        public int Failed => Volatile.Read(ref _failed);

        public IReadOnlyList<DbQueryStreamOutcome> StreamTerminals
        {
            get { lock (_sync) return _streamTerminals.ToArray(); }
        }

        /// <summary>Zeroes the counters so one check's terminals are its own.</summary>
        public void ResetTerminals()
        {
            Interlocked.Exchange(ref _generated, 0);
            Interlocked.Exchange(ref _dispatched, 0);
            Interlocked.Exchange(ref _succeeded, 0);
            Interlocked.Exchange(ref _failed, 0);
            lock (_sync) _streamTerminals.Clear();
        }

        public string DescribeTerminals()
        {
            return "generated=" + Generated +
                   " dispatched=" + Dispatched +
                   " success=" + Succeeded +
                   " error=" + Failed +
                   " streamTerminals=" + StreamTerminals.Count;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Context.Dispose();
        }

        /// <summary>
        /// The options every ordinary phase runs with.
        /// <para/>
        /// Raw SQL is emitted into events because the scenario has to record the
        /// statements it dispatched, and the database holds nothing but
        /// generated data. The library's default redacts, and that default is
        /// the right one for an application.
        /// </summary>
        public static DatabaseGatewayOptions DefaultOptions()
        {
            return new DatabaseGatewayOptions
            {
                EmitRawSqlInEvents = true,
                IncludeCommandResultInSuccessEvents = false,
                DynamicMode = DynamicMode.Expando,
                MappingFailureMode = Mapping.DataMappingFailureMode.Strict,
                SynchronousFallbackMode = DbSynchronousFallbackMode.Disabled,
                DefaultCommandTimeoutSeconds = 30
            };
        }
    }
}
