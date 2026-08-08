#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using NekoLib.Data.RuntimeTests.SqlServer.Container;
using NekoLib.Data.RuntimeTests.SqlServer.Reporting;
using NekoLib.Data.RuntimeTests.SqlServer.Server;

namespace NekoLib.Data.RuntimeTests.SqlServer.Workload
{
    /// <summary>
    /// What every phase needs, in one place, so a phase's constructor does not
    /// become a list of nine arguments.
    /// </summary>
    internal sealed class PhaseContext
    {
        public GatewayWorkspace Workspace = null!;
        public CheckRunner Runner = null!;
        public SqlServerEndpoint Endpoint = null!;
        public ServerProbe Probe = null!;
        public WorkloadCounters Counters = null!;
        public ResourceSampler Sampler = null!;
        public RunArtifacts Artifacts = null!;
        public AdoptedContainer? Adopted;
        public string DatabaseName = string.Empty;
        public int Seed;
        public bool ContainerFaultsAllowed;
        public CancellationToken Ct;

        /// <summary>
        /// Classifies an exception as a cancellation outcome.
        /// <para/>
        /// This exists because the answer is genuinely provider-dependent and
        /// the specification says so: the check requires
        /// <see cref="OperationCanceledException"/> <i>or the current
        /// documented cancellation terminal</i>, and not a generic failure.
        /// <c>Microsoft.Data.SqlClient</c> raises <see cref="SqlException"/>
        /// with number 0 for some cancelled paths, so recognising that shape is
        /// part of reading the result honestly rather than a way of passing a
        /// check that should fail.
        /// </summary>
        public static CancellationShape ClassifyCancellation(Exception exception)
        {
            if (exception is OperationCanceledException)
                return new CancellationShape(true, "OperationCanceledException", exception.GetType().Name);

            SqlException? sql = exception as SqlException;
            if (sql != null && sql.Number == 0)
            {
                return new CancellationShape(
                    true,
                    "SqlException number 0 (the provider's cancellation shape)",
                    "SqlException#0: " + Flatten(sql.Message));
            }

            if (sql != null)
            {
                return new CancellationShape(
                    false,
                    "provider failure, not a cancellation",
                    "SqlException#" + sql.Number + ": " + Flatten(sql.Message));
            }

            return new CancellationShape(
                false,
                "unrelated failure",
                exception.GetType().Name + ": " + Flatten(exception.Message));
        }

        /// <summary>
        /// Describes a provider exception for the record: the type, the SQL
        /// Server error number, and its class. The number is what makes a
        /// transport loss distinguishable from a login failure later.
        /// </summary>
        public static string DescribeProviderFailure(Exception exception)
        {
            SqlException? sql = exception as SqlException;
            if (sql == null)
                return exception.GetType().Name + ": " + Flatten(exception.Message);

            List<string> numbers = new List<string>();
            foreach (SqlError error in sql.Errors)
                numbers.Add(error.Number.ToString(System.Globalization.CultureInfo.InvariantCulture));

            return "SqlException number=" + sql.Number +
                   " class=" + sql.Class +
                   " state=" + sql.State +
                   " errors=[" + string.Join(",", numbers.ToArray()) + "]" +
                   " : " + Flatten(sql.Message);
        }

        /// <summary>Runs a call that is expected to throw and returns what it threw.</summary>
        public static async Task<Exception?> CaptureAsync(Func<Task> call)
        {
            try
            {
                await call().ConfigureAwait(false);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        private static string Flatten(string text) =>
            (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
    }

    /// <summary>The result of classifying one exception as a cancellation or not.</summary>
    internal sealed class CancellationShape
    {
        public CancellationShape(bool isCancellation, string kind, string detail)
        {
            IsCancellation = isCancellation;
            Kind = kind;
            Detail = detail;
        }

        public bool IsCancellation { get; }
        public string Kind { get; }
        public string Detail { get; }
    }
}
