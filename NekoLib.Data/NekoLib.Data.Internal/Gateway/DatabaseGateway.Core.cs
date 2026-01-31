#nullable enable
using NekoLib.Data.Internal.Gateway.Query;


using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.OleDb;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;

namespace NekoLib.Data.Internal.Gateway
{
    /// <summary>
    /// Gateway genérico para acesso a dados baseado em SQL bruto e em <see cref="Query.QueryBuilder"/>.
    /// 
    /// Exposição pública em quatro camadas:
    /// <list type="bullet">
    ///   <item><b>Raw</b>: RecordItem (GetRaw/ReadRaw).</item>
    ///   <item><b>DTO</b>: GetDto/ReadDto (tipado forte via reflexão).</item>
    ///   <item><b>Dynamic</b>: DynamicRow + IL.</item>
    ///   <item><b>Universal</b>: Get/Read com fallback DTO → Dynamic.</item>
    /// </list>
    /// </summary>
    public partial class DatabaseGateway
    {

        QueryExecutionContext ctx;

        #region ctor

        /// <summary>
        /// Cria um <see cref="DatabaseGateway"/> baseado no contexto de execução fornecido. 
        /// </summary>
        public DatabaseGateway(QueryExecutionContext _ctx)
        {
            ctx = _ctx;
        }

        #endregion

        #region Connection / command helpers

        private async Task<DbConnection> OpenConnectionAsync(CancellationToken Ct)
        {
            var conn = await ctx.ConnectionFactory.Create().ConfigureAwait(false);


            try
            {
                await conn.OpenAsync(Ct).ConfigureAwait(false);
            }
            catch(NotSupportedException)
            {
                conn.Open();
            }

            return conn;

            
        }

        private async Task<T> WithCommandAsync<T>(string Sql, Dictionary<string, object?>? Parameters, Func<DbCommand, Task<T>> work, CancellationToken Ct)
        {
            if(Sql == null) throw new ArgumentNullException(nameof(Sql));
            if(work == null) throw new ArgumentNullException(nameof(work));
            if(ctx == null) throw new ArgumentNullException(nameof(ctx));
            using(DbConnection conn = await OpenConnectionAsync(Ct).ConfigureAwait(false))
            {
                using(DbCommand cmd = conn.CreateCommand())
                {
                    cmd.CommandText = Sql;
                    cmd.CommandType = CommandType.Text;

                    ApplyParameters(cmd, Parameters);
                    T result = default;
                    try
                    {
                        
                        ctx.RaiseSqlDispatch(Sql);
                        result = await work(cmd).ConfigureAwait(false);
                        ctx.RaiseSuccess(Sql, result);
                    }
                    catch(OperationCanceledException) { throw; }
                    catch(Exception ex) { ctx.RaiseError(Sql, ex); throw; }
                    return result;
                    
                }
            }
        }

        private Task<T> WithCommandAsync<T>(string Sql, Func<DbCommand, Task<T>> work, CancellationToken Ct)
        {
            return WithCommandAsync(Sql, null, work, Ct);
        }

        private static async Task<DbDataReader> ExecuteReaderSafeAsync(DbCommand Cmd, CancellationToken Ct)
        {
            try
            {
                DbDataReader reader = await Cmd.ExecuteReaderAsync(Ct).ConfigureAwait(false);
                return reader;
            }
            catch(NotSupportedException)
            {
                return Cmd.ExecuteReader();
            }
        }

        private static async Task<int> ExecuteNonQuerySafeAsync(DbCommand Cmd, CancellationToken Ct)
        {
            try
            {
                int count = await Cmd.ExecuteNonQueryAsync(Ct).ConfigureAwait(false);
                return count;
            }
            catch(NotSupportedException)
            {
                return Cmd.ExecuteNonQuery();
            }
        }

        private static async Task<bool> ReadSafeAsync(DbDataReader Reader, CancellationToken Ct)
        {          
            try
            {
                bool has = await Reader.ReadAsync(Ct).ConfigureAwait(false);
                return has;
            }
            catch(NotSupportedException)
            {
                return Reader.Read();
            }
        }

        private static void ApplyParameters(DbCommand cmd, Dictionary<string, object?>? parameters)
{
    if(parameters == null || parameters.Count == 0)
        return;
#if NET481
            bool isAccess = cmd is OleDbCommand;
#else
            bool isAccess = false;
#endif


            // OleDb ignora nomes e usa ordem posicional.
            // Em produção, garantimos uma ordem estável baseada em @pN quando possível.
            IEnumerable<KeyValuePair<string, object?>> ordered = parameters;

    if(isAccess)
    {
        ordered = parameters.OrderBy(static kv =>
        {
            // @p1, @p2, ... (ordenação numérica)
            string k = kv.Key ?? string.Empty;
            if(k.Length >= 3 && (k[0] == '@' || k[0] == '?') && (k[1] == 'p' || k[1] == 'P'))
            {
                if(int.TryParse(k.Substring(2), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
                    return n;
            }
            return int.MaxValue;
        }).ThenBy(kv => kv.Key, StringComparer.Ordinal);
    }

    foreach(var kv in ordered)
    {
        DbParameter p = cmd.CreateParameter();
        if(!isAccess)
            p.ParameterName = kv.Key;

        p.Value = kv.Value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }
}

#endregion

        
    }
}