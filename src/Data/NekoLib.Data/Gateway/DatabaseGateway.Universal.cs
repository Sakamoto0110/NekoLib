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
 
using NekoLib.Data.Query;

 
using NekoLib.Data.Dynamic;
using NekoLib.Data.Mapping;


#if NET6_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace NekoLib.Data.Internal.Gateway
{
    public partial class DatabaseGateway
    {
        #region Universal GET (DTO → fallback Dynamic)

        public Task<List<T>> Get<TTranslator,
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(QueryBuilder Builder, CancellationToken Ct = default) where TTranslator : IDbQueryTranslator, new() where T : new()
        {
            return GetUniversalFromBuilder<TTranslator, T>(Builder, null, Ct);
        }

        public Task<List<T>> Get<TTranslator,
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(QueryBuilder Builder, DbSession session, CancellationToken Ct = default) where TTranslator : IDbQueryTranslator, new() where T : new()
        {
            return GetUniversalFromBuilder<TTranslator, T>(Builder, session, Ct);
        }

        private async Task<List<T>> GetUniversalFromBuilder<TTranslator,
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(QueryBuilder Builder, DbSession? session, CancellationToken Ct) where TTranslator : IDbQueryTranslator, new() where T : new()
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));

            QueryModel model = Builder.Build();
            DatabaseQuery dbq = new TTranslator().Translate(model);
            ctx.RaiseSqlGenerated(dbq.Sql);

            return await GetUniversalFromSql<T>(dbq.Sql, dbq.Parameters, Ct, session).ConfigureAwait(false);
        }

        private async Task<List<T>> GetUniversalFromSql<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(string sql, Dictionary<string, object?>? parameters, CancellationToken ct = default, DbSession? session = null) where T : new()
        {
            Type targetType = typeof(T);
            if(targetType == typeof(DynamicRow) || targetType == typeof(object))
            {
                List<DynamicRow> dynRows = await GetDynamicFromSql(sql, parameters, ct, session).ConfigureAwait(false);
                List<T> result = new List<T>(dynRows.Count);
                for(int i = 0; i < dynRows.Count; i++)
                {
                    result.Add((T)(object)dynRows[i]);
                }
                return result;
            }

            return await GetDtoFromSql<T>(sql, parameters, ct, session).ConfigureAwait(false);
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

            return ReadUniversalDispatch(builder, handler, null, ct);
        }

        public Task Read(QueryBuilder builder,Delegate handler, DbSession session, CancellationToken ct = default)
        {
            if(builder == null) throw new ArgumentNullException(nameof(builder));
            if(handler == null) throw new ArgumentNullException(nameof(handler));

            return ReadUniversalDispatch(builder, handler, session, ct);
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

        public Task Read<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(QueryBuilder builder,Action<T> callback, DbSession session, CancellationToken ct = default)
        {
            if(callback == null) throw new ArgumentNullException(nameof(callback));

            return Read(builder, (Delegate)callback, session, ct);
        }

        private async Task ReadUniversalDispatch(QueryBuilder builder,Delegate handler, DbSession? session, CancellationToken ct)
        {
            ParameterInfo[] pars = handler.Method.GetParameters();
            if(pars.Length != 1)
                throw new InvalidOperationException(
                    "The handler must have exactly one parameter.");

            Type targetType = pars[0].ParameterType;
            QueryModel model = builder.Build();
            DatabaseQuery dbq = ctx.Translator.Translate(model);
            ctx.RaiseSqlGenerated(dbq.Sql);

            await WithCommandAsync(dbq.Sql, dbq.Parameters, async cmd => {
                    using(DbDataReader reader = await ExecuteReaderSafeAsync(cmd, ct))
                    {
                        SchemaInfo schema = ExtractSchema(reader);

                        bool wantsDynamic = targetType == typeof(DynamicRow) || targetType == typeof(object);

                        while(await ReadSafeAsync(reader, ct))
                        {
                            ct.ThrowIfCancellationRequested();

                            if(!wantsDynamic)
                            {
                                object dto = ReaderDtoMapper.Map(
                                    reader,
                                    targetType,
                                    ctx.Options.MappingFailureMode);
                                handler.DynamicInvoke(dto);
                                continue;
                            }
                            // Dynamic path (IL/Expando) conforme opções
                            handler.DynamicInvoke((object)CreateDynamicRow(schema, reader));
}
                    }

                    return 0;
                }, ct, session).ConfigureAwait(false);
        }




#if NET6_0_OR_GREATER
        #region DataStreaming
        public IAsyncEnumerable<dynamic> StreamData(QueryBuilder builder, CancellationToken ct = default)
        {
            return StreamDataFromBuilder(builder, null, ct);
        }

        /// <remarks>
        /// O <see cref="DbDataReader"/> permanece aberto durante toda a enumeração, então a
        /// transação/conexão da <paramref name="session"/> fica presa enquanto o consumidor
        /// itera. Evite manter um <c>await foreach</c> lento (ex.: I/O por linha) dentro de
        /// uma transação aberta, para não prolongar locks. Consuma rapidamente ou materialize.
        /// </remarks>
        public IAsyncEnumerable<dynamic> StreamData(QueryBuilder builder, DbSession session, CancellationToken ct = default)
        {
            return StreamDataFromBuilder(builder, session, ct);
        }

        private IAsyncEnumerable<dynamic> StreamDataFromBuilder(QueryBuilder builder, DbSession? session, CancellationToken ct)
        {
            if(builder == null) throw new ArgumentNullException(nameof(builder));

            QueryModel model = builder.Build();
            DatabaseQuery dbq = ctx.Translator.Translate(model);

            ctx.RaiseSqlGenerated(dbq.Sql);

            return StreamDynamicAsDynamic(dbq, session, ct);
        }

        public IAsyncEnumerable<T> StreamData<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(QueryBuilder builder, CancellationToken ct = default)
        {
            return StreamDataFromBuilder<T>(builder, null, ct);
        }

        /// <remarks>
        /// O <see cref="DbDataReader"/> permanece aberto durante toda a enumeração, então a
        /// transação/conexão da <paramref name="session"/> fica presa enquanto o consumidor
        /// itera. Evite manter um <c>await foreach</c> lento (ex.: I/O por linha) dentro de
        /// uma transação aberta, para não prolongar locks. Consuma rapidamente ou materialize.
        /// </remarks>
        public IAsyncEnumerable<T> StreamData<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(QueryBuilder builder, DbSession session, CancellationToken ct = default)
        {
            return StreamDataFromBuilder<T>(builder, session, ct);
        }

        private IAsyncEnumerable<T> StreamDataFromBuilder<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(QueryBuilder builder, DbSession? session, CancellationToken ct)
        {
            if(builder == null) throw new ArgumentNullException(nameof(builder));

            QueryModel model = builder.Build();
            DatabaseQuery dbq = ctx.Translator.Translate(model);

            ctx.RaiseSqlGenerated(dbq.Sql);

            return StreamDataCore<T>(dbq, session, ct);
        }

        private async IAsyncEnumerable<T> StreamDataCore<T>(DatabaseQuery dbq, DbSession? session, [EnumeratorCancellation] CancellationToken ct)
        {
            DbConnection? conn = null;
            bool ownsConnection = false;
            DbCommand? cmd = null;
            DbDataReader? reader = null;
            SchemaInfo? schema = null;
            bool wantsDynamic = false;

            try
            {
                try
                {
                    if(session != null)
                    {
                        conn = session.Connection;
                    }
                    else
                    {
                        conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
                        ownsConnection = true;
                    }

                    cmd = conn.CreateCommand();
                    cmd.CommandText = dbq.Sql;
                    if(session?.Transaction != null)
                        cmd.Transaction = session.Transaction;
                    ctx.RaiseSqlDispatch(dbq.Sql);
                    ApplyParameters(cmd, dbq.Parameters);

                    reader = await ExecuteReaderSafeAsync(cmd, ct).ConfigureAwait(false);
                    schema = ExtractSchema(reader);

                    wantsDynamic =
                        typeof(T) == typeof(object) ||
                        typeof(T) == typeof(DynamicRow);
                }
                catch(Exception ex) when(!(ex is OperationCanceledException))
                {
                    ctx.RaiseError(dbq.Sql, ex);
                    throw;
                }

                while(true)
                {
                    T item;
                    try
                    {
                        if(!await ReadSafeAsync(reader, ct).ConfigureAwait(false))
                            break;

                        ct.ThrowIfCancellationRequested();

                        if(!wantsDynamic)
                            item = (T)ReaderDtoMapper.Map(
                                reader!,
                                typeof(T),
                                ctx.Options.MappingFailureMode);
                        else
                            item = (T)(object)CreateDynamicRow(schema!, reader!);
                    }
                    catch(Exception ex) when(!(ex is OperationCanceledException))
                    {
                        ctx.RaiseError(dbq.Sql, ex);
                        throw;
                    }

                    yield return item;
                }

                ctx.RaiseSuccess(dbq.Sql);
            }
            finally
            {
                if(reader != null) reader.Dispose();
                if(cmd != null) cmd.Dispose();
                if(ownsConnection && conn != null) conn.Dispose();
            }
        }














        



        #endregion
#endif
        #endregion
    }
}
