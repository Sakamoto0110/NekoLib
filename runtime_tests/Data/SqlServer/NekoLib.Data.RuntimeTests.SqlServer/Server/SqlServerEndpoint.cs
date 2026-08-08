#nullable enable
using System;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace NekoLib.Data.RuntimeTests.SqlServer.Server
{
    /// <summary>
    /// Builds the connection strings the scenario uses, and owns the one secret
    /// in the whole run.
    /// <para/>
    /// The password is read once, from the documented environment variable, and
    /// never leaves this type: it is not written to source, documentation, a
    /// command line, a log, a result file, or a connection string that anything
    /// else is allowed to print. <see cref="Describe"/> exists so a run can say
    /// exactly how it connected without saying what with.
    /// </summary>
    internal sealed class SqlServerEndpoint
    {
        private readonly string _password;

        private SqlServerEndpoint(string host, int port, string user, string password, string masterDatabase)
        {
            Host = host;
            Port = port;
            User = user;
            _password = password;
            MasterDatabase = masterDatabase;
        }

        public string Host { get; }
        public int Port { get; }
        public string User { get; }
        public string MasterDatabase { get; }

        public string DataSource =>
            Host + "," + Port.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Resolves the password from the documented variable.
        /// <para/>
        /// Three scopes are consulted because <c>setx</c> writes to the user
        /// scope and a shell that was already open never sees it. Falling back
        /// to the persisted scopes is what makes "set the variable and run"
        /// behave the way the operator expects instead of failing with a stale
        /// process environment.
        /// </summary>
        public static bool TryResolve(
            string variableName,
            string host,
            int port,
            string user,
            out SqlServerEndpoint? endpoint,
            out string diagnostic)
        {
            string? password = ReadVariable(variableName);
            if (string.IsNullOrEmpty(password))
            {
                endpoint = null;
                diagnostic =
                    "the environment variable " + variableName + " is not set. " +
                    "Set it to the adopted container's SA password in the shell that launches this scenario, " +
                    "or persist it with setx. Its value is never written to the repository or to any artifact.";
                return false;
            }

            endpoint = new SqlServerEndpoint(host, port, user, password!, "master");
            diagnostic = "resolved from " + variableName;
            return true;
        }

        private static string? ReadVariable(string name)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrEmpty(value)) return value;

            try
            {
                value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
                if (!string.IsNullOrEmpty(value)) return value;

                return Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine);
            }
            catch (Exception)
            {
                // Reading the persisted scopes can be denied; the process scope
                // above is the supported path and has already been tried.
                return null;
            }
        }

        /// <summary>
        /// Builds a connection string for one database.
        /// <para/>
        /// <c>TrustServerCertificate</c> is on because the container serves a
        /// self-signed certificate, and the client package encrypts by default
        /// from version 4 onward. That combination is recorded in the evidence
        /// rather than hidden: this run proves provider and gateway behaviour
        /// over an encrypted loopback connection whose certificate was not
        /// validated, and it proves nothing about certificate validation.
        /// </summary>
        public string BuildConnectionString(
            string database,
            int maxPoolSize = 100,
            int minPoolSize = 0,
            bool pooling = true,
            int connectTimeoutSeconds = 15,
            string applicationName = "NekoLib.E4-SQL")
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder
            {
                DataSource = DataSource,
                InitialCatalog = database,
                UserID = User,
                Password = _password,
                Encrypt = true,
                TrustServerCertificate = true,
                Pooling = pooling,
                MaxPoolSize = maxPoolSize,
                MinPoolSize = minPoolSize,
                ConnectTimeout = connectTimeoutSeconds,
                ApplicationName = applicationName,
                MultipleActiveResultSets = false
            };

            return builder.ConnectionString;
        }

        /// <summary>
        /// The same connection string with the password removed, safe to print
        /// and to write into an artifact.
        /// </summary>
        public string Describe(string connectionString)
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connectionString);
            builder.Password = string.Empty;
            builder.Remove("Password");
            return builder.ConnectionString;
        }

        /// <summary>
        /// Removes the password from arbitrary text before it is reported.
        /// Provider exception messages do not normally carry it, but a failed
        /// connection string can reach a message through other libraries, and a
        /// leak in an artifact cannot be taken back.
        /// </summary>
        public string Redact(string? text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (_password.Length == 0) return text!;

            return text!.Replace(_password, "[password redacted]");
        }
    }
}
