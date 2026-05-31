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

        public async Task<bool> ContainsData(string Sql,CancellationToken Ct = default)
        {
            bool has = await WithCommandAsync(Sql, async delegate (DbCommand cmd)
            {
                using(DbDataReader reader = await ExecuteReaderSafeAsync(cmd, Ct).ConfigureAwait(false))
                {
                    bool any = await ReadSafeAsync(reader, Ct).ConfigureAwait(false);
                    return any;
                }
            }, Ct).ConfigureAwait(false);

            return has;
        }

        public Task<List<Dictionary<string, RecordItem>>> GetRaw(string Sql,CancellationToken Ct = default)
        {
            return GetRaw(Sql,null, Ct);
        }

        public async Task<List<Dictionary<string, RecordItem>>> GetRaw(string Sql, Dictionary<string, object?>? Parameters,CancellationToken Ct = default)
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
            }, Ct).ConfigureAwait(false);

            return result;
        }

        public Task ReadRaw(string Sql,Action<Dictionary<string, RecordItem>> Callback,CancellationToken Ct = default)
        {
            return ReadRaw(Sql, null, Callback, Ct);
        }

        public async Task ReadRaw(string Sql,Dictionary<string, object?>? Parameters,Action<Dictionary<string, RecordItem>> Callback,CancellationToken Ct = default)
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
            }, Ct).ConfigureAwait(false);
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

        public async Task<List<Dictionary<string, RecordItem>>> GetRaw(QueryBuilder Builder,CancellationToken Ct = default)
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));

            var translator = ctx.Translator;
            QueryModel model = Builder.Build();
            DatabaseQuery dbq = translator.Translate(model);
            ctx.RaiseSqlGenerated(dbq.Sql);
            return await GetRaw(dbq.Sql, dbq.Parameters, Ct).ConfigureAwait(false);
        }

        public async Task ReadRaw(QueryBuilder Builder ,Action<Dictionary<string, RecordItem>> Callback,CancellationToken Ct = default)
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));
            if(Callback == null) throw new ArgumentNullException(nameof(Callback));

            var translator = ctx.Translator;
            QueryModel model = Builder.Build();
            DatabaseQuery dbq = translator.Translate(model);
            ctx.RaiseSqlGenerated(dbq.Sql);

            await ReadRaw(dbq.Sql, dbq.Parameters, Callback, Ct).ConfigureAwait(false);

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



        public async Task<List<T>> GetDto<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(QueryBuilder Builder , CancellationToken Ct = default) where T : new()
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));
            
            QueryModel model = Builder.Build();
            DatabaseQuery dbq = ctx.Translator.Translate(model);
            ctx.RaiseSqlGenerated(dbq.Sql);

            return await GetDtoFromSql<T>(dbq.Sql, dbq.Parameters, Ct).ConfigureAwait(false);
        }

        private async Task<List<T>> GetDtoFromSql<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(string sql, Dictionary<string, object?>? parameters, CancellationToken ct = default) where T : new()
        {
            List<T> list = new List<T>();

            await WithCommandAsync(sql, parameters, async delegate (DbCommand cmd)
            {
                using(DbDataReader reader = await ExecuteReaderSafeAsync(cmd, ct).ConfigureAwait(false))
                {
                    List<PropertyColumnBinding> bindings = CreatePropertyBindings(typeof(T), ExtractSchema(reader));

                    while(await ReadSafeAsync(reader, ct).ConfigureAwait(false))
                    {
                        ct.ThrowIfCancellationRequested();
                        list.Add(CreateDtoFromReader<T>(reader, bindings));
                    }
                }

                return 0;
            }, ct).ConfigureAwait(false);

            return list;
        }

        public async Task ReadDto<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>( QueryBuilder Builder , Action<T> Callback,CancellationToken Ct = default)where T : new()
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));
            if(Callback == null) throw new ArgumentNullException(nameof(Callback));
           
            QueryModel model = Builder.Build();
            DatabaseQuery dbq = ctx.Translator.Translate(model);
            ctx.RaiseSqlGenerated(dbq.Sql);

            await ReadDtoFromSql<T>(dbq.Sql, dbq.Parameters, Callback, Ct).ConfigureAwait(false);
        }

        private async Task ReadDtoFromSql<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(string sql, Dictionary<string, object?>? parameters, Action<T> callback, CancellationToken ct = default) where T : new()
        {
            await WithCommandAsync(sql, parameters, async delegate (DbCommand cmd)
            {
                using(DbDataReader reader = await ExecuteReaderSafeAsync(cmd, ct).ConfigureAwait(false))
                {
                    List<PropertyColumnBinding> bindings = CreatePropertyBindings(typeof(T), ExtractSchema(reader));

                    while(await ReadSafeAsync(reader, ct).ConfigureAwait(false))
                    {
                        ct.ThrowIfCancellationRequested();
                        callback(CreateDtoFromReader<T>(reader, bindings));
                    }
                }

                return 0;
            }, ct).ConfigureAwait(false);
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
            if(builder == null) throw new ArgumentNullException(nameof(builder));

            QueryModel model = builder.Build();
            DatabaseQuery dbq = ctx.Translator.Translate(model);

            ctx.RaiseSqlGenerated(dbq.Sql);

            return StreamDtoCore<T>(dbq, ct);
        }

        private async IAsyncEnumerable<T> StreamDtoCore<T>(DatabaseQuery dbq, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct) where T : new()
        {
            DbConnection? conn = null;
            DbCommand? cmd = null;
            DbDataReader? reader = null;
            List<PropertyColumnBinding>? bindings = null;

            try
            {
                try
                {
                    conn = await OpenConnectionAsync(ct).ConfigureAwait(false);

                    cmd = conn.CreateCommand();
                    cmd.CommandText = dbq.Sql;
                    ctx.RaiseSqlDispatch(dbq.Sql);
                    ApplyParameters(cmd, dbq.Parameters);

                    reader = await ExecuteReaderSafeAsync(cmd, ct).ConfigureAwait(false);
                    bindings = CreatePropertyBindings(typeof(T), ExtractSchema(reader));
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
                        item = CreateDtoFromReader<T>(reader, bindings!);
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
                if(conn != null) conn.Dispose();
            }
        }

        private async IAsyncEnumerable<Dictionary<string, RecordItem>> StreamRawCore(DatabaseQuery dbq, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            DbConnection? conn = null;
            DbCommand? cmd = null;
            DbDataReader? reader = null;
            SchemaInfo? schema = null;

            try
            {
                try
                {
                    conn = await OpenConnectionAsync(ct).ConfigureAwait(false);

                    cmd = conn.CreateCommand();
                    cmd.CommandText = dbq.Sql;
                    ctx.RaiseSqlDispatch(dbq.Sql);
                    ApplyParameters(cmd, dbq.Parameters);

                    reader = await ExecuteReaderSafeAsync(cmd, ct).ConfigureAwait(false);
                    schema = ExtractSchema(reader);
                }
                catch(Exception ex) when(!(ex is OperationCanceledException))
                {
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
                    catch(Exception ex) when(!(ex is OperationCanceledException))
                    {
                        ctx.RaiseError(dbq.Sql, ex);
                        throw;
                    }

                    yield return row;
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


        #endregion DataStreaming
#endif
    }
}
