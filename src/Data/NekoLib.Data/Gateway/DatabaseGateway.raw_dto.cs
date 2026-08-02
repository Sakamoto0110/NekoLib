#nullable enable
 
using NekoLib.Data.Query;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Data.Mapping;




#if NET6_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace NekoLib.Data.Internal.Gateway
{
    public partial class DatabaseGateway
    {
        #region Raw API (RecordItem)

        public Task<bool> ContainsData(string Sql, CancellationToken Ct = default)
        {
            return ContainsDataCore(Sql, null, Ct);
        }

        public Task<bool> ContainsData(string Sql, DbSession session, CancellationToken Ct = default)
        {
            return ContainsDataCore(Sql, session, Ct);
        }

        private async Task<bool> ContainsDataCore(string Sql, DbSession? session, CancellationToken Ct)
        {
            bool has = await WithCommandAsync(Sql, async delegate (DbCommand cmd)
            {
                using(DbDataReader reader = await ExecuteReaderSafeAsync(cmd, Ct).ConfigureAwait(false))
                {
                    bool any = await ReadSafeAsync(reader, Ct).ConfigureAwait(false);
                    return any;
                }
            }, Ct, session).ConfigureAwait(false);

            return has;
        }

        public Task<List<Dictionary<string, RecordItem>>> GetRaw(string Sql,CancellationToken Ct = default)
        {
            return GetRawCore(Sql, null, null, Ct);
        }

        public Task<List<Dictionary<string, RecordItem>>> GetRaw(string Sql, Dictionary<string, object?>? Parameters,CancellationToken Ct = default)
        {
            return GetRawCore(Sql, Parameters, null, Ct);
        }

        public Task<List<Dictionary<string, RecordItem>>> GetRaw(string Sql, Dictionary<string, object?>? Parameters, DbSession session, CancellationToken Ct = default)
        {
            return GetRawCore(Sql, Parameters, session, Ct);
        }

        private async Task<List<Dictionary<string, RecordItem>>> GetRawCore(string Sql, Dictionary<string, object?>? Parameters, DbSession? session, CancellationToken Ct)
        {
            List<Dictionary<string, RecordItem>> result = await WithCommandAsync(Sql,Parameters, async delegate (DbCommand cmd)
            {
                List<Dictionary<string, RecordItem>> list = new List<Dictionary<string, RecordItem>>();

                using(DbDataReader reader = await ExecuteReaderSafeAsync(cmd, Ct).ConfigureAwait(false))
                {
                    SchemaInfo schema = ExtractSchema(reader);

                    while(await ReadSafeAsync(reader, Ct).ConfigureAwait(false))
                    {
                        Ct.ThrowIfCancellationRequested();
                        list.Add(ReadRecordRow(reader, schema));
                    }
                }

                return list;
            }, Ct, session).ConfigureAwait(false);

            return result;
        }

        public Task ReadRaw(string Sql,Action<Dictionary<string, RecordItem>> Callback,CancellationToken Ct = default)
        {
            return ReadRawCore(Sql, null, Callback, null, Ct);
        }

        public Task ReadRaw(string Sql,Dictionary<string, object?>? Parameters,Action<Dictionary<string, RecordItem>> Callback,CancellationToken Ct = default)
        {
            return ReadRawCore(Sql, Parameters, Callback, null, Ct);
        }

        public Task ReadRaw(string Sql,Dictionary<string, object?>? Parameters,Action<Dictionary<string, RecordItem>> Callback, DbSession session, CancellationToken Ct = default)
        {
            return ReadRawCore(Sql, Parameters, Callback, session, Ct);
        }

        private async Task ReadRawCore(string Sql,Dictionary<string, object?>? Parameters,Action<Dictionary<string, RecordItem>> Callback, DbSession? session, CancellationToken Ct)
        {
            if(Callback == null) throw new ArgumentNullException(nameof(Callback));
            if(ctx == null) throw new ArgumentNullException(nameof(ctx));

            await WithCommandAsync(Sql, Parameters, async delegate (DbCommand cmd)
            {
                using(DbDataReader reader = await ExecuteReaderSafeAsync(cmd, Ct).ConfigureAwait(false))
                {
                    SchemaInfo schema = ExtractSchema(reader);

                    while(await ReadSafeAsync(reader, Ct).ConfigureAwait(false))
                    {
                        Ct.ThrowIfCancellationRequested();
                        Callback(ReadRecordRow(reader, schema));
                    }
                }

                return 0;
            }, Ct, session).ConfigureAwait(false);
        }

        protected async Task<int> Upsert(string Sql,Dictionary<string, object?>? Parameters,CancellationToken Ct = default)
        {
            if(string.IsNullOrWhiteSpace(Sql))
                throw new ArgumentNullException(nameof(Sql));
            if(ctx == null) throw new ArgumentNullException(nameof(ctx));

            int affected = await WithCommandAsync(Sql,Parameters, cmd => ExecuteNonQuerySafeAsync(cmd, Ct), Ct).ConfigureAwait(false);

            return affected;
        }

        public Task<int> Insert(string Sql,CancellationToken Ct = default)
        {
            return Upsert(Sql,null, Ct);
        }

        public Task<int> Insert(string Sql,Dictionary<string, object?>? Parameters,CancellationToken Ct = default)
        {
            return Upsert(Sql,Parameters, Ct);
        }

        public Task<int> Update(string Sql,CancellationToken Ct = default)
        {
            return Upsert(Sql, null, Ct);
        }

        public Task<int> Update(string Sql,Dictionary<string, object?>? Parameters,CancellationToken Ct = default)
        {
            return Upsert(Sql,Parameters, Ct);
        }

        public Task<List<Dictionary<string, RecordItem>>> GetRaw(QueryBuilder Builder,CancellationToken Ct = default)
        {
            return GetRawFromBuilder(Builder, null, Ct);
        }

        public Task<List<Dictionary<string, RecordItem>>> GetRaw(QueryBuilder Builder, DbSession session, CancellationToken Ct = default)
        {
            return GetRawFromBuilder(Builder, session, Ct);
        }

        private async Task<List<Dictionary<string, RecordItem>>> GetRawFromBuilder(QueryBuilder Builder, DbSession? session, CancellationToken Ct)
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));

            var translator = ctx.Translator;
            QueryModel model = Builder.Build();
            DatabaseQuery dbq = translator.Translate(model);
            ctx.RaiseSqlGenerated(dbq.Sql);
            return await GetRawCore(dbq.Sql, dbq.Parameters, session, Ct).ConfigureAwait(false);
        }

        public Task ReadRaw(QueryBuilder Builder ,Action<Dictionary<string, RecordItem>> Callback,CancellationToken Ct = default)
        {
            return ReadRawFromBuilder(Builder, Callback, null, Ct);
        }

        public Task ReadRaw(QueryBuilder Builder ,Action<Dictionary<string, RecordItem>> Callback, DbSession session, CancellationToken Ct = default)
        {
            return ReadRawFromBuilder(Builder, Callback, session, Ct);
        }

        private async Task ReadRawFromBuilder(QueryBuilder Builder ,Action<Dictionary<string, RecordItem>> Callback, DbSession? session, CancellationToken Ct)
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));
            if(Callback == null) throw new ArgumentNullException(nameof(Callback));

            var translator = ctx.Translator;
            QueryModel model = Builder.Build();
            DatabaseQuery dbq = translator.Translate(model);
            ctx.RaiseSqlGenerated(dbq.Sql);

            await ReadRawCore(dbq.Sql, dbq.Parameters, Callback, session, Ct).ConfigureAwait(false);

        }

        public async Task<int> Insert(QueryBuilder Builder , CancellationToken Ct = default)
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));

            var translator = ctx.Translator;
            QueryModel model = Builder.Build();
            DatabaseQuery dbq = translator.Translate(model);
            ctx.RaiseSqlGenerated(dbq.Sql);

            return await Insert(dbq.Sql, dbq.Parameters, Ct).ConfigureAwait(false);
        }

        public async Task<int> Update(QueryBuilder Builder , CancellationToken Ct = default)
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));

            var translator = ctx.Translator;
            QueryModel model = Builder.Build();
            DatabaseQuery dbq = translator.Translate(model);
            ctx.RaiseSqlGenerated(dbq.Sql);

            return await Update(dbq.Sql, dbq.Parameters, Ct).ConfigureAwait(false);
        }

        #endregion

        #region DTO API (strong typed, no IL fallback)
        /*
         *  var qb = new QueryBuilder()
         *               .Select("User_id")
         *               .From("Users")
         *               .where("something").build();
         *               
         *              var list =  new DatabaseWrapper(ctx).GetDto<User>(qb);
         *               
         *               
         *      
         * 
         * 
         * 
         * 
         * 
         */



        public Task<List<T>> GetDto<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(QueryBuilder Builder , CancellationToken Ct = default) where T : new()
        {
            return GetDtoFromBuilder<T>(Builder, null, Ct);
        }

        public Task<List<T>> GetDto<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(QueryBuilder Builder , DbSession session, CancellationToken Ct = default) where T : new()
        {
            return GetDtoFromBuilder<T>(Builder, session, Ct);
        }

        private async Task<List<T>> GetDtoFromBuilder<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(QueryBuilder Builder , DbSession? session, CancellationToken Ct) where T : new()
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));

            QueryModel model = Builder.Build();
            DatabaseQuery dbq = ctx.Translator.Translate(model);
            ctx.RaiseSqlGenerated(dbq.Sql);

            return await GetDtoFromSql<T>(dbq.Sql, dbq.Parameters, Ct, session).ConfigureAwait(false);
        }

        private async Task<List<T>> GetDtoFromSql<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(string sql, Dictionary<string, object?>? parameters, CancellationToken ct = default, DbSession? session = null) where T : new()
        {
            ReaderDtoMapper.ValidateTargetType(typeof(T));
            List<T> list = new List<T>();

            await WithCommandAsync(sql, parameters, async delegate (DbCommand cmd)
            {
                using(DbDataReader reader = await ExecuteReaderSafeAsync(cmd, ct).ConfigureAwait(false))
                {
                    while(await ReadSafeAsync(reader, ct).ConfigureAwait(false))
                    {
                        ct.ThrowIfCancellationRequested();
                        list.Add(ReaderDtoMapper.Map<T>(reader, ctx.Options.MappingFailureMode));
                    }
                }

                return 0;
            }, ct, session).ConfigureAwait(false);

            return list;
        }

        public Task ReadDto<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>( QueryBuilder Builder , Action<T> Callback,CancellationToken Ct = default)where T : new()
        {
            return ReadDtoFromBuilder<T>(Builder, Callback, null, Ct);
        }

        public Task ReadDto<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>( QueryBuilder Builder , Action<T> Callback, DbSession session, CancellationToken Ct = default)where T : new()
        {
            return ReadDtoFromBuilder<T>(Builder, Callback, session, Ct);
        }

        private async Task ReadDtoFromBuilder<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>( QueryBuilder Builder , Action<T> Callback, DbSession? session, CancellationToken Ct)where T : new()
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));
            if(Callback == null) throw new ArgumentNullException(nameof(Callback));

            QueryModel model = Builder.Build();
            DatabaseQuery dbq = ctx.Translator.Translate(model);
            ctx.RaiseSqlGenerated(dbq.Sql);

            await ReadDtoFromSql<T>(dbq.Sql, dbq.Parameters, Callback, Ct, session).ConfigureAwait(false);
        }

        private async Task ReadDtoFromSql<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(string sql, Dictionary<string, object?>? parameters, Action<T> callback, CancellationToken ct = default, DbSession? session = null) where T : new()
        {
            ReaderDtoMapper.ValidateTargetType(typeof(T));
            await WithCommandAsync(sql, parameters, async delegate (DbCommand cmd)
            {
                using(DbDataReader reader = await ExecuteReaderSafeAsync(cmd, ct).ConfigureAwait(false))
                {
                    while(await ReadSafeAsync(reader, ct).ConfigureAwait(false))
                    {
                        ct.ThrowIfCancellationRequested();
                        callback(ReaderDtoMapper.Map<T>(reader, ctx.Options.MappingFailureMode));
                    }
                }

                return 0;
            }, ct, session).ConfigureAwait(false);
        }

        #endregion

#if NET6_0_OR_GREATER
        #region DataStreaming
 




        public IAsyncEnumerable<T> StreamDto<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(QueryBuilder builder, CancellationToken ct = default) where T : new()
        {
            return StreamDtoFromBuilder<T>(builder, null, ct);
        }

        /// <remarks>
        /// O <see cref="DbDataReader"/> permanece aberto durante toda a enumeração, então a
        /// transação/conexão da <paramref name="session"/> fica presa enquanto o consumidor
        /// itera. Evite manter um <c>await foreach</c> lento (ex.: I/O por linha) dentro de
        /// uma transação aberta, para não prolongar locks. Consuma rapidamente ou materialize.
        /// </remarks>
        public IAsyncEnumerable<T> StreamDto<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(QueryBuilder builder, DbSession session, CancellationToken ct = default) where T : new()
        {
            return StreamDtoFromBuilder<T>(builder, session, ct);
        }

        private IAsyncEnumerable<T> StreamDtoFromBuilder<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(QueryBuilder builder, DbSession? session, CancellationToken ct) where T : new()
        {
            if(builder == null) throw new ArgumentNullException(nameof(builder));
            ReaderDtoMapper.ValidateTargetType(typeof(T));

            QueryModel model = builder.Build();
            DatabaseQuery dbq = ctx.Translator.Translate(model);

            ctx.RaiseSqlGenerated(dbq.Sql);

            return StreamDtoCore<T>(dbq, session, ct);
        }

        private async IAsyncEnumerable<T> StreamDtoCore<T>(DatabaseQuery dbq, DbSession? session, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct) where T : new()
        {
            DbConnection? conn = null;
            bool ownsConnection = false;
            DbCommand? cmd = null;
            DbDataReader? reader = null;
            StreamTerminalState terminal = new StreamTerminalState();

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
                }
                catch(OperationCanceledException ex)
                {
                    terminal.Cancel(ex);
                    throw;
                }
                catch(Exception ex)
                {
                    terminal.Fail(ex);
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
                        item = ReaderDtoMapper.Map<T>(reader, ctx.Options.MappingFailureMode);
                    }
                    catch(OperationCanceledException ex)
                    {
                        terminal.Cancel(ex);
                        throw;
                    }
                    catch(Exception ex)
                    {
                        terminal.Fail(ex);
                        ctx.RaiseError(dbq.Sql, ex);
                        throw;
                    }

                    yield return item;
                }

                terminal.Complete();
            }
            finally
            {
                FinishStreamLifetime(
                    dbq.Sql,
                    terminal,
                    reader,
                    cmd,
                    ownsConnection,
                    conn);
            }
        }

        private async IAsyncEnumerable<Dictionary<string, RecordItem>> StreamRawCore(DatabaseQuery dbq, DbSession? session, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            DbConnection? conn = null;
            bool ownsConnection = false;
            DbCommand? cmd = null;
            DbDataReader? reader = null;
            SchemaInfo? schema = null;
            StreamTerminalState terminal = new StreamTerminalState();

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
                }
                catch(OperationCanceledException ex)
                {
                    terminal.Cancel(ex);
                    throw;
                }
                catch(Exception ex)
                {
                    terminal.Fail(ex);
                    ctx.RaiseError(dbq.Sql, ex);
                    throw;
                }

                while(true)
                {
                    Dictionary<string, RecordItem> row;
                    try
                    {
                        if(!await ReadSafeAsync(reader, ct).ConfigureAwait(false))
                            break;

                        ct.ThrowIfCancellationRequested();
                        row = ReadRecordRow(reader, schema!);
                    }
                    catch(OperationCanceledException ex)
                    {
                        terminal.Cancel(ex);
                        throw;
                    }
                    catch(Exception ex)
                    {
                        terminal.Fail(ex);
                        ctx.RaiseError(dbq.Sql, ex);
                        throw;
                    }

                    yield return row;
                }

                terminal.Complete();
            }
            finally
            {
                FinishStreamLifetime(
                    dbq.Sql,
                    terminal,
                    reader,
                    cmd,
                    ownsConnection,
                    conn);
            }
        }


        #endregion DataStreaming
#endif
    }
}
