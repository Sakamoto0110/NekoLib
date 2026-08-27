#nullable enable

using NekoLib.Data.Query;
using NekoLib.Data.Internal.Gateway;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace NekoLib.Data.Gateway
{
    /// <summary>
    /// Provider-neutral database gateway for raw SQL and
    /// <see cref="Query.QueryBuilder"/> statements.
    /// 
    /// The public result APIs have three layers:
    /// <list type="bullet">
    ///   <item><b>Raw</b>: <see cref="RecordItem"/> through GetRaw/ReadRaw.</item>
    ///   <item><b>DTO</b>: strongly typed GetDto/ReadDto mapping.</item>
    ///   <item><b>Dynamic</b>: <see cref="Dynamic.DynamicRow"/> storage.</item>
    /// </list>
    /// </summary>
    public sealed partial class DatabaseGateway : IDatabaseGateway
    {

        private readonly QueryExecutionContext ctx;

        #region ctor

        /// <summary>
        /// Creates a gateway for the supplied execution context.
        /// </summary>
        public DatabaseGateway(QueryExecutionContext context)
        {
            ctx = context ?? throw new ArgumentNullException(nameof(context));
        }

        #endregion

        #region Connection / command helpers

        private async Task<DbConnection> OpenConnectionAsync(CancellationToken Ct)
        {
            DbConnection? conn = null;
            try
            {
                conn = await ctx.ConnectionFactory.Create().ConfigureAwait(false);
                await conn.OpenAsync(Ct).ConfigureAwait(false);
                return conn;
            }
            catch(NotSupportedException) when (
                ctx.Options.SynchronousFallbackMode == DbSynchronousFallbackMode.Enabled)
            {
                if(conn == null)
                    throw;

                try
                {
                    Ct.ThrowIfCancellationRequested();
                    conn.Open();
                    return conn;
                }
                catch
                {
                    conn.Dispose();
                    throw;
                }
            }
            catch
            {
                if(conn != null)
                    conn.Dispose();
                throw;
            }
        }
        public async Task<DbSession> OpenSessionAsync(CancellationToken ct = default)
        {
            var conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
            return new DbSession(conn, ctx.SessionAffinityToken);
        }
        private async Task<T> WithCommandAsync<T>(
            string Sql,
            Dictionary<string, object?>? Parameters,
            Func<DbCommand, Task<T>> work,
            CancellationToken Ct,
            DbCommandPolicy? commandPolicy = null,
            IReadOnlyList<LogicalParameter>? logicalParameters = null)
        {
            if(Sql == null) throw new ArgumentNullException(nameof(Sql));
            if(work == null) throw new ArgumentNullException(nameof(work));
            using(DbConnection conn = await OpenConnectionAsync(Ct).ConfigureAwait(false))
            {
                using(DbCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = Sql;
                    cmd.CommandType = CommandType.Text;

                    ApplyCommandPolicy(cmd, commandPolicy);
                    Dictionary<string, object?> effectiveParameters =
                        await PrepareLogicalParametersAsync(
                            cmd,
                            Parameters,
                            logicalParameters,
                            null,
                            Ct).ConfigureAwait(false);
                    ApplyParameters(cmd, effectiveParameters);
                    try
                    {
                        
                        ctx.RaiseSqlDispatch(Sql);
                        T result = await work(cmd).ConfigureAwait(false);
                        ctx.RaiseSuccess(Sql, result);
                        return result;
                    }
                    catch(OperationCanceledException) { throw; }
                    catch(Exception ex) { ctx.RaiseError(Sql, ex); throw; }
                }
            }
        }

        private Task<T> WithCommandAsync<T>(string Sql, Func<DbCommand, Task<T>> work, CancellationToken Ct)
        {
           return WithCommandAsync(Sql, null, work, Ct);
        }

        private async Task<T> WithCommandAsync<T>(
            string sql,
            Dictionary<string, object?>? parameters,
            Func<DbCommand, Task<T>> work,
            CancellationToken ct,
            DbSession? session,
            DbCommandPolicy? commandPolicy = null,
            IReadOnlyList<LogicalParameter>? logicalParameters = null)
        {
            DbConnection conn;
            bool ownsConnection = false;

            if (session != null)
            {
                conn = GetSessionConnection(session);
            }
            else
            {
                conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
                ownsConnection = true;
            }

            try
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql;
                    cmd.CommandType = CommandType.Text;

                    ApplyCommandPolicy(cmd, commandPolicy);
                    if (session?.Transaction != null)
                        cmd.Transaction = session.Transaction;

                    Dictionary<string, object?> effectiveParameters =
                        await PrepareLogicalParametersAsync(
                            cmd,
                            parameters,
                            logicalParameters,
                            session,
                            ct).ConfigureAwait(false);
                    ApplyParameters(cmd, effectiveParameters);

                    ctx.RaiseSqlDispatch(sql);
                    var result = await work(cmd).ConfigureAwait(false);
                    ctx.RaiseSuccess(sql, result);

                    return result;
                }
            }
            catch(OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                ctx.RaiseError(sql, ex);
                throw;
            }
            finally
            {
                if (ownsConnection)
                    conn.Dispose();
            }

        }

        private Task<T> WithCommandAsync<T>(string sql, Func<DbCommand, Task<T>> work, CancellationToken ct, DbSession? session)
        {
            return WithCommandAsync(sql, null, work, ct,session);
        }

        private async Task<DbDataReader> ExecuteReaderSafeAsync(DbCommand Cmd, CancellationToken Ct)
        {
            try
            {
                DbDataReader reader = await Cmd.ExecuteReaderAsync(Ct).ConfigureAwait(false);
                return reader;
            }
            catch(NotSupportedException) when (
                ctx.Options.SynchronousFallbackMode == DbSynchronousFallbackMode.Enabled)
            {
                Ct.ThrowIfCancellationRequested();
                return Cmd.ExecuteReader();
            }
        }

        private DbConnection GetSessionConnection(DbSession session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            session.ValidateFor(ctx.SessionAffinityToken);
            return session.Connection;
        }

        private async Task<int> ExecuteNonQuerySafeAsync(DbCommand Cmd, CancellationToken Ct)
        {
            try
            {
                int count = await Cmd.ExecuteNonQueryAsync(Ct).ConfigureAwait(false);
                return count;
            }
            catch(NotSupportedException) when (
                ctx.Options.SynchronousFallbackMode == DbSynchronousFallbackMode.Enabled)
            {
                Ct.ThrowIfCancellationRequested();
                return Cmd.ExecuteNonQuery();
            }
        }

        private async Task<bool> ReadSafeAsync(DbDataReader Reader, CancellationToken Ct)
        {          
            try
            {
                bool has = await Reader.ReadAsync(Ct).ConfigureAwait(false);
                return has;
            }
            catch(NotSupportedException) when (
                ctx.Options.SynchronousFallbackMode == DbSynchronousFallbackMode.Enabled)
            {
                Ct.ThrowIfCancellationRequested();
                return Reader.Read();
            }
        }

        private void ApplyParameters(DbCommand cmd, Dictionary<string, object?>? parameters)
        {
            IDbParameterBinder binder = DbParameterBinderFactory.Create(
                cmd,
                ctx.Options.ParameterBindingMode);
            binder.Bind(cmd, parameters);
        }

        private void ApplyCommandPolicy(DbCommand command, DbCommandPolicy? commandPolicy)
        {
            if (commandPolicy != null)
                commandPolicy.Validate(nameof(commandPolicy));

            int? timeout = commandPolicy?.TimeoutSeconds ??
                ctx.Options.DefaultCommandTimeoutSeconds;
            if (timeout.HasValue)
                command.CommandTimeout = timeout.Value;
        }

#endregion

        
    }
}
