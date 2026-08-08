#nullable enable
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace NekoLib.Data.RuntimeTests.SqlServer.Server
{
    /// <summary>What the server itself reports about its version and edition.</summary>
    internal sealed class ServerFacts
    {
        public string FullVersion = string.Empty;
        public string ProductVersion = string.Empty;
        public string ProductLevel = string.Empty;
        public string Edition = string.Empty;
        public string Collation = string.Empty;
    }

    /// <summary>
    /// Talks to the server outside the gateway, for the three jobs the gateway
    /// is the wrong tool for: waiting until the engine is accepting logins,
    /// reading the version the evidence record has to name, and creating or
    /// dropping the scenario's own database.
    /// <para/>
    /// Everything else in this scenario goes through <c>NekoLib.Data</c>. This
    /// type is deliberately separate so the boundary stays visible: what the
    /// provider did here is setup, and only what went through the gateway is
    /// evidence about the library.
    /// </summary>
    internal sealed class ServerProbe
    {
        private readonly SqlServerEndpoint _endpoint;

        public ServerProbe(SqlServerEndpoint endpoint)
        {
            _endpoint = endpoint;
        }

        /// <summary>
        /// Opens a single connection to <c>master</c> and returns whether the
        /// engine answered. Pooling is off so a previous run's dead handle can
        /// never make an unavailable server look available.
        /// </summary>
        public async Task<bool> IsAvailableAsync(int connectTimeoutSeconds, CancellationToken ct)
        {
            try
            {
                string connectionString = _endpoint.BuildConnectionString(
                    _endpoint.MasterDatabase,
                    pooling: false,
                    connectTimeoutSeconds: connectTimeoutSeconds,
                    applicationName: "NekoLib.E4-SQL.probe");

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync(ct).ConfigureAwait(false);
                    using (SqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT 1";
                        object? value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
                        return value != null && Convert.ToInt32(value, CultureInfo.InvariantCulture) == 1;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Waits for the engine to accept logins.
        /// <para/>
        /// A started container is not a ready server: SQL Server recovers its
        /// databases after the process starts, and rejects logins with
        /// "database is in transition" or a plain connection refusal for several
        /// seconds. The wait is bounded, and a run that exhausts it reports a
        /// prerequisite failure rather than a product finding.
        /// </summary>
        public async Task<ReadinessResult> WaitUntilReadyAsync(TimeSpan budget, CancellationToken ct)
        {
            DateTime deadline = DateTime.UtcNow + budget;
            int attempts = 0;
            string lastError = "never attempted";

            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();
                attempts++;

                try
                {
                    string connectionString = _endpoint.BuildConnectionString(
                        _endpoint.MasterDatabase,
                        pooling: false,
                        connectTimeoutSeconds: 5,
                        applicationName: "NekoLib.E4-SQL.readiness");

                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        await connection.OpenAsync(ct).ConfigureAwait(false);
                        using (SqlCommand command = connection.CreateCommand())
                        {
                            // DATABASEPROPERTYEX answers only once recovery has
                            // finished, which is a stronger readiness signal
                            // than a successful login.
                            command.CommandText =
                                "SELECT CONVERT(nvarchar(64), DATABASEPROPERTYEX('master', 'Status'))";
                            object? status = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
                            string text = Convert.ToString(status, CultureInfo.InvariantCulture) ?? string.Empty;

                            if (string.Equals(text, "ONLINE", StringComparison.OrdinalIgnoreCase))
                                return new ReadinessResult(true, attempts, DateTime.UtcNow, "ONLINE");

                            lastError = "master status is '" + text + "'";
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastError = _endpoint.Redact(ex.GetType().Name + ": " + ex.Message);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(750), ct).ConfigureAwait(false);
            }

            return new ReadinessResult(false, attempts, DateTime.UtcNow, lastError);
        }

        /// <summary>
        /// Waits until one database is online and accepting queries.
        /// <para/>
        /// A ready engine is not a ready database. After a restart SQL Server
        /// brings user databases online one at a time, and a connection that
        /// arrives too early is refused with "database is in transition" — an
        /// environment condition that would otherwise be recorded as a recovery
        /// failure.
        /// </summary>
        public async Task<ReadinessResult> WaitUntilDatabaseReadyAsync(
            string database,
            TimeSpan budget,
            CancellationToken ct)
        {
            DateTime deadline = DateTime.UtcNow + budget;
            int attempts = 0;
            string lastError = "never attempted";

            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();
                attempts++;

                try
                {
                    string connectionString = BuildIsolatedConnectionString(database);
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        await connection.OpenAsync(ct).ConfigureAwait(false);
                        using (SqlCommand command = connection.CreateCommand())
                        {
                            command.CommandText = "SELECT 1";
                            await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
                            return new ReadinessResult(true, attempts, DateTime.UtcNow, "queryable");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastError = _endpoint.Redact(ex.GetType().Name + ": " + ex.Message);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), ct).ConfigureAwait(false);
            }

            return new ReadinessResult(false, attempts, DateTime.UtcNow, lastError);
        }

        /// <summary>
        /// An unpooled connection string, so a readiness answer can never come
        /// from a handle the pool kept from before an interruption.
        /// </summary>
        private string BuildIsolatedConnectionString(string database) =>
            _endpoint.BuildConnectionString(
                database,
                pooling: false,
                connectTimeoutSeconds: 5,
                applicationName: "NekoLib.E4-SQL.readiness");

        public async Task<ServerFacts> ReadServerFactsAsync(CancellationToken ct)
        {
            ServerFacts facts = new ServerFacts();

            await WithMasterAsync(async command =>
            {
                command.CommandText =
                    "SELECT @@VERSION, " +
                    "CONVERT(nvarchar(128), SERVERPROPERTY('ProductVersion')), " +
                    "CONVERT(nvarchar(128), SERVERPROPERTY('ProductLevel')), " +
                    "CONVERT(nvarchar(128), SERVERPROPERTY('Edition')), " +
                    "CONVERT(nvarchar(128), SERVERPROPERTY('Collation'))";

                using (SqlDataReader reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false))
                {
                    if (await reader.ReadAsync(ct).ConfigureAwait(false))
                    {
                        facts.FullVersion = Flatten(reader.GetString(0));
                        facts.ProductVersion = reader.GetString(1);
                        facts.ProductLevel = reader.GetString(2);
                        facts.Edition = reader.GetString(3);
                        facts.Collation = reader.GetString(4);
                    }
                }
            }, ct).ConfigureAwait(false);

            return facts;
        }

        public Task CreateScenarioDatabaseAsync(string database, CancellationToken ct)
        {
            return WithMasterAsync(async command =>
            {
                // The name is generated by the scenario from its campaign id and
                // is validated before it reaches here, but it still cannot be a
                // parameter: CREATE DATABASE takes an identifier, not a value.
                command.CommandText = "CREATE DATABASE " + QuoteIdentifier(database);
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }, ct);
        }

        public async Task<bool> DatabaseExistsAsync(string database, CancellationToken ct)
        {
            bool exists = false;

            await WithMasterAsync(async command =>
            {
                command.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name = @name";
                SqlParameter parameter = command.CreateParameter();
                parameter.ParameterName = "@name";
                parameter.DbType = DbType.String;
                parameter.Value = database;
                command.Parameters.Add(parameter);

                object? count = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
                exists = Convert.ToInt32(count, CultureInfo.InvariantCulture) > 0;
            }, ct).ConfigureAwait(false);

            return exists;
        }

        /// <summary>
        /// Drops one scenario database, forcing other sessions off it first.
        /// <para/>
        /// <c>SET SINGLE_USER WITH ROLLBACK IMMEDIATE</c> is here because a run
        /// that failed halfway can leave a pooled connection attached, and a
        /// cleanup that cannot complete is a worse outcome than a rolled-back
        /// session belonging to the run that is already over.
        /// </summary>
        public async Task DropScenarioDatabaseAsync(string database, CancellationToken ct)
        {
            // Bounded retry, because forcing every other session off a database
            // is itself contended work. On a loaded machine the ALTER can be
            // picked as a deadlock victim and the drop then fails with the
            // database still in use, which leaves exactly the litter cleanup
            // exists to prevent. DEADLOCK_PRIORITY HIGH makes cleanup win that
            // race rather than lose it; the retry covers the rest.
            const int attempts = 4;
            Exception? last = null;

            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                try
                {
                    await WithMasterAsync(async command =>
                    {
                        command.CommandText =
                            "SET DEADLOCK_PRIORITY HIGH; " +
                            "SET LOCK_TIMEOUT 30000; " +
                            "IF DB_ID(@name) IS NOT NULL BEGIN " +
                            "  ALTER DATABASE " + QuoteIdentifier(database) +
                            "    SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                            "  DROP DATABASE " + QuoteIdentifier(database) + "; " +
                            "END";

                        SqlParameter parameter = command.CreateParameter();
                        parameter.ParameterName = "@name";
                        parameter.DbType = DbType.String;
                        parameter.Value = database;
                        command.Parameters.Add(parameter);

                        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                    }, ct).ConfigureAwait(false);

                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    last = ex;
                    if (attempt < attempts)
                        await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct).ConfigureAwait(false);
                }
            }

            throw new InvalidOperationException(
                "the scenario database could not be dropped after " + attempts + " attempts: " +
                _endpoint.Redact(last?.Message ?? "no diagnostic"),
                last);
        }

        /// <summary>
        /// Lists databases carrying the scenario prefix, so cleanup can report
        /// anything a previous run left behind instead of quietly ignoring it.
        /// </summary>
        public async Task<IReadOnlyList<string>> ListScenarioDatabasesAsync(string prefix, CancellationToken ct)
        {
            List<string> names = new List<string>();

            await WithMasterAsync(async command =>
            {
                command.CommandText =
                    "SELECT name FROM sys.databases WHERE name LIKE @prefix + N'%' ORDER BY name";

                SqlParameter parameter = command.CreateParameter();
                parameter.ParameterName = "@prefix";
                parameter.DbType = DbType.String;
                parameter.Value = prefix;
                command.Parameters.Add(parameter);

                using (SqlDataReader reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(ct).ConfigureAwait(false))
                        names.Add(reader.GetString(0));
                }
            }, ct).ConfigureAwait(false);

            return names;
        }

        /// <summary>
        /// Counts the sessions the scenario's own application name holds against
        /// one database. This is the leak check: after cleanup the answer must
        /// be zero, and no assertion about disposal is worth much without it.
        /// </summary>
        public async Task<int> CountScenarioSessionsAsync(string database, CancellationToken ct)
        {
            int count = 0;

            await WithMasterAsync(async command =>
            {
                command.CommandText =
                    "SELECT COUNT(*) FROM sys.dm_exec_sessions s " +
                    "WHERE s.program_name LIKE N'NekoLib.E4-SQL%' " +
                    "  AND s.session_id <> @@SPID " +
                    "  AND DB_NAME(s.database_id) = @database";

                SqlParameter parameter = command.CreateParameter();
                parameter.ParameterName = "@database";
                parameter.DbType = DbType.String;
                parameter.Value = database;
                command.Parameters.Add(parameter);

                object? value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
                count = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }, ct).ConfigureAwait(false);

            return count;
        }

        /// <summary>
        /// Returns whether any session is currently executing a batch carrying
        /// the supplied marker. This is the scenario's server-visible
        /// synchronization point for mid-flight cancellation: it answers "has
        /// this specific statement actually started running on the server",
        /// which a wall-clock sleep never can.
        /// </summary>
        public async Task<bool> IsMarkerExecutingAsync(string marker, CancellationToken ct)
        {
            bool running = false;

            await WithMasterAsync(async command =>
            {
                command.CommandText =
                    "SELECT COUNT(*) " +
                    "FROM sys.dm_exec_requests r " +
                    "OUTER APPLY sys.dm_exec_sql_text(r.sql_handle) t " +
                    "WHERE r.session_id <> @@SPID AND t.text LIKE @marker";

                SqlParameter parameter = command.CreateParameter();
                parameter.ParameterName = "@marker";
                parameter.DbType = DbType.String;
                parameter.Value = "%" + marker + "%";
                command.Parameters.Add(parameter);

                object? value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
                running = Convert.ToInt32(value, CultureInfo.InvariantCulture) > 0;
            }, ct).ConfigureAwait(false);

            return running;
        }

        private async Task WithMasterAsync(Func<SqlCommand, Task> work, CancellationToken ct)
        {
            // Control work is setup and cleanup, not measurement, so it waits
            // longer than a gateway call would. A loaded machine must not turn
            // a slow login into a reported product outcome.
            string connectionString = _endpoint.BuildConnectionString(
                _endpoint.MasterDatabase,
                pooling: false,
                connectTimeoutSeconds: 60,
                applicationName: "NekoLib.E4-SQL.control");

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync(ct).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    await work(command).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Wraps an identifier for T-SQL, rejecting anything that could close
        /// the bracket. Scenario database names are generated, so this can only
        /// fire on a programming error - which is exactly when it should.
        /// </summary>
        public static string QuoteIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                throw new ArgumentException("An identifier is required.", nameof(identifier));

            if (identifier.IndexOf(']') >= 0)
                throw new ArgumentException("Identifier contains ']': " + identifier, nameof(identifier));

            return "[" + identifier + "]";
        }

        private static string Flatten(string text) =>
            text.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();
    }

    internal sealed class ReadinessResult
    {
        public ReadinessResult(bool ready, int attempts, DateTime observedAtUtc, string detail)
        {
            Ready = ready;
            Attempts = attempts;
            ObservedAtUtc = observedAtUtc;
            Detail = detail;
        }

        public bool Ready { get; }
        public int Attempts { get; }
        public DateTime ObservedAtUtc { get; }
        public string Detail { get; }
    }
}
