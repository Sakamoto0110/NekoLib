#nullable enable

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Data.Internal.Gateway.Query;
using NekoLib.Data.Internal.Gateway.Dynamic;
using NekoLib.Data.Internal.Gateway.Mapping;
using NekoLib.Data.Query;
 
#if NET6_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace NekoLib.Data.Internal.Gateway
{
    public partial class DatabaseGateway
    {
        #region Universal GET (DTO → fallback Dynamic)

        public async Task<List<T>> Get<TTranslator,
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(QueryBuilder Builder, CancellationToken Ct = default) where TTranslator : IDbQueryTranslator, new() where T : new()
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));

            Type targetType = typeof(T);

            if(targetType == typeof(DynamicRow) || targetType == typeof(object))
            {
                List<DynamicRow> dynRows = await GetDynamic(Builder, Ct).ConfigureAwait(false);
                List<T> castResult = dynRows.Cast<T>().ToList();
                return castResult;
            }

            if(targetType.GetConstructor(Type.EmptyTypes) != null)
            {
                try
                {
                    List<T> dtoResult = await GetDto<T>(Builder, Ct).ConfigureAwait(false);
                    return dtoResult;
                }
                catch
                {
                }
            }

            List<DynamicRow> dynFallback = await GetDynamic(Builder, Ct).ConfigureAwait(false);
            List<T> fallbackCast = dynFallback.Cast<T>().ToList();
            return fallbackCast;
        }

        #endregion

        #region Universal READ (no <T> required + typed overload)

        /// <summary>
        /// Leitura universal usando apenas o tradutor e o delegate do callback.
        /// O tipo do parâmetro do callback determina a estratégia:
        /// DynamicRow → IL, object → IL, DTO com ctor padrão → DTO, senão fallback IL.
        /// </summary>
        public Task Read(QueryBuilder builder,Delegate handler,CancellationToken ct = default)
        {
            if(builder == null) throw new ArgumentNullException(nameof(builder));
            if(handler == null) throw new ArgumentNullException(nameof(handler));

            return ReadUniversalDispatch(builder, handler, ct);
        }


        /// <summary>
        /// Versão tipada de leitura universal, com fallback automático para IL + DynamicRow.
        /// </summary>
        public Task Read<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(QueryBuilder builder,Action<T> callback,CancellationToken ct = default)
        {
            if(callback == null) throw new ArgumentNullException(nameof(callback));

            return Read(builder, (Delegate)callback, ct);
        }

        private async Task ReadUniversalDispatch(QueryBuilder builder,Delegate handler,CancellationToken ct)
        {
            ParameterInfo[] pars = handler.Method.GetParameters();
            if(pars.Length != 1)
                throw new InvalidOperationException(
                    "The handler must have exactly one parameter.");

            Type targetType = pars[0].ParameterType;
            QueryModel model = builder.Build();
            DatabaseQuery dbq = ctx.Translator.Translate(model);
            ctx.RaiseSqlGenerated(dbq.Sql);

            try
            {
                await WithCommandAsync(dbq.Sql, dbq.Parameters, async cmd => {
                    using(DbDataReader reader = await ExecuteReaderSafeAsync(cmd, ct))
                    {
                        SchemaInfo schema = ExtractSchema(reader);

                        bool wantsDynamic = targetType == typeof(DynamicRow) || targetType == typeof(object);
                        bool wantsDto = !wantsDynamic && targetType.GetConstructor(Type.EmptyTypes) != null;

                        Type ilType = null; // resolved lazily by CreateDynamicRow when needed

                        while(await ReadSafeAsync(reader, ct))
                        {
                            ct.ThrowIfCancellationRequested();

                            if(wantsDto)
                            {
                                // DTO path (SAFE)
                                // var record = ReadRecordRow(reader, schema);
                                var record = new Dictionary<string, RecordItem>(StringComparer.OrdinalIgnoreCase);                               
                                foreach(var col in schema.Columns)
                                

                                    record[col] = new RecordItem
                                    {
                                        Name = col,
                                        Type = schema.ColumnTypes[col].FullName,
                                        Value = reader[col] is DBNull ? string.Empty : (Convert.ToString(reader[col], CultureInfo.InvariantCulture) ?? string.Empty)
                                    };


                                
                                object dto = DataMapper.Map(record, targetType);
                                handler.DynamicInvoke(dto);
                                continue;
                            }
                            // Dynamic path (IL/Expando) conforme opções
                            handler.DynamicInvoke((object)CreateDynamicRow(schema, reader));
}
                    }

                    return 0;
                }, ct);


            }
            catch(Exception ex)
            {
                ctx.RaiseError(dbq.Sql, ex);
                throw;
            }
        }




#if NET6_0_OR_GREATER
        #region DataStreaming
        public IAsyncEnumerable<dynamic> StreamData(QueryBuilder builder, CancellationToken ct = default)
        {
            if(builder == null) throw new ArgumentNullException(nameof(builder));

            QueryModel model = builder.Build();
            DatabaseQuery dbq = ctx.Translator.Translate(model);

            ctx.RaiseSqlGenerated(dbq.Sql);

            try
            {
                return StreamDataCore<dynamic>(dbq, ct);
            }
            catch(Exception ex)
            {
                ctx.RaiseError(dbq.Sql, ex);
                throw;
            }
        }

        public IAsyncEnumerable<T> StreamData<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(QueryBuilder builder, CancellationToken ct = default)
        {
            if(builder == null) throw new ArgumentNullException(nameof(builder));

            QueryModel model = builder.Build();
            DatabaseQuery dbq = ctx.Translator.Translate(model);

            ctx.RaiseSqlGenerated(dbq.Sql);

            try
            {
                return StreamDataCore<T>(dbq, ct);
            }
            catch(Exception ex)
            {
                ctx.RaiseError(dbq.Sql, ex);
                throw;
            }
        }

        private async IAsyncEnumerable<T> StreamDataCore<T>(DatabaseQuery dbq,[EnumeratorCancellation] CancellationToken ct)
        {
            DbConnection conn = null;
            DbCommand cmd = null;
            DbDataReader reader = null;

            try
            {
                conn = await ctx.ConnectionFactory.Create().ConfigureAwait(false);
                try { await conn.OpenAsync(ct).ConfigureAwait(false); }
                catch(NotSupportedException) { conn.Open(); }

                cmd = conn.CreateCommand();
                cmd.CommandText = dbq.Sql;
                ctx.RaiseSqlDispatch(dbq.Sql);
                ApplyParameters(cmd, dbq.Parameters);

                reader = await ExecuteReaderSafeAsync(cmd, ct).ConfigureAwait(false);
                var schema = ExtractSchema(reader);

                bool wantsDynamic =
                    typeof(T) == typeof(object) ||
                    typeof(T) == typeof(DynamicRow);

                bool wantsDto =
                    !wantsDynamic &&
                    typeof(T).GetConstructor(Type.EmptyTypes) != null;

                Type ilType = null;
                if(wantsDynamic || !wantsDto)
                    ilType = RuntimeTypeFactory.GetOrCreate(schema);

                while(await ReadSafeAsync(reader, ct).ConfigureAwait(false))
                {
                    ct.ThrowIfCancellationRequested();

                    if(wantsDto)
                    {
                        var record = new Dictionary<string, RecordItem>(StringComparer.OrdinalIgnoreCase);

                        foreach(string col in schema.Columns)
                        {
                            record[col] = new RecordItem
                            {
                                Name = col,
                                Type = schema.ColumnTypes[col]?.FullName,
                                Value = reader[col] is DBNull ? string.Empty : (Convert.ToString(reader[col], CultureInfo.InvariantCulture) ?? string.Empty)
                            };
                        }

                        yield return (T)DataMapper.Map(record, typeof(T));
                    }
                    else
                    {
                        yield return (T)(object)CreateDynamicRow(schema, reader);
                    }
                }

                ctx.RaiseSuccess(dbq.Sql);
            }
            finally
            {
                if(reader != null) reader.Dispose();
                if(cmd != null) cmd.Dispose();
                if(conn != null) conn.Dispose();
            }
        }














        



        #endregion
#endif
        #endregion
    }
}