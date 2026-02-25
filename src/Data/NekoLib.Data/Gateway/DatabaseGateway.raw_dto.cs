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
                        Dictionary<string, RecordItem> row = new Dictionary<string, RecordItem>(StringComparer.OrdinalIgnoreCase);
                        foreach(string col in schema.Columns)
                        {
                            object raw = reader[col];
                            string strValue = Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;

                            RecordItem item = new RecordItem
                            {
                                Name = col,
                                Type = schema.ColumnTypes[col].FullName ?? typeof(string).FullName!,
                                Value = strValue
                            };
                            row[col] = item;
                        }
                        list.Add(row);
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
                        Dictionary<string, RecordItem> row = new Dictionary<string, RecordItem>(StringComparer.OrdinalIgnoreCase);

                        foreach(string col in schema.Columns)
                        {
                            object raw = reader[col];
                            string strValue = Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;

                            RecordItem item = new RecordItem
                            {
                                Name = col,
                                Type = schema.ColumnTypes[col].FullName ?? typeof(string).FullName!,
                                Value = strValue
                            };
                            row[col] = item;
                        }
                        try
                        {
                            Callback(row);
                        }
                        catch(Exception ex) { ctx.RaiseError(Sql, ex); }
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

            int affected = await WithCommandAsync(Sql,Parameters, delegate (DbCommand cmd)
            {
                try
                {
                    return ExecuteNonQuerySafeAsync(cmd, Ct);
                }
                catch(Exception ex) { ctx.RaiseError(Sql, ex); return Task.FromResult(-1); }
                
                
            }, Ct).ConfigureAwait(false);

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
            List<Dictionary<string, RecordItem>> result = null;
            try 
            {
                ctx.RaiseSqlDispatch(dbq.Sql);
                result = await GetRaw(dbq.Sql, dbq.Parameters, Ct).ConfigureAwait(false);
                ctx.RaiseSuccess(dbq.Sql);
            }
            catch(Exception ex) { ctx.RaiseError(dbq.Sql, ex);  }
            
            return result;
        }

        public async Task ReadRaw(QueryBuilder Builder ,Action<Dictionary<string, RecordItem>> Callback,CancellationToken Ct = default)
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));
            if(Callback == null) throw new ArgumentNullException(nameof(Callback));

            var translator = ctx.Translator;
            QueryModel model = Builder.Build();
            DatabaseQuery dbq = translator.Translate(model);
            ctx.RaiseSqlGenerated(dbq.Sql);

            
            try
            {
                ctx.RaiseSqlDispatch(dbq.Sql);
                await ReadRaw(dbq.Sql, dbq.Parameters, Callback, Ct).ConfigureAwait(false);
                ctx.RaiseSuccess(dbq.Sql);
            }
            catch(Exception ex) { ctx.RaiseError(dbq.Sql,ex); }

        }

        public async Task<int> Insert(QueryBuilder Builder , CancellationToken Ct = default)
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));

            var translator = ctx.Translator;
            QueryModel model = Builder.Build();
            DatabaseQuery dbq = translator.Translate(model);
            ctx.RaiseSqlGenerated(dbq.Sql);

            int affected = 0;
            try
            {
                ctx.RaiseSqlDispatch(dbq.Sql);
                 affected = await Insert(dbq.Sql, dbq.Parameters, Ct).ConfigureAwait(false);
                ctx.RaiseSuccess(dbq.Sql);
            }
            catch(Exception ex) { ctx.RaiseError(dbq.Sql, ex); affected = -1; }
            
            return affected;
        }

        public async Task<int> Update(QueryBuilder Builder , CancellationToken Ct = default)
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));

            var translator = ctx.Translator;
            QueryModel model = Builder.Build();
            DatabaseQuery dbq = translator.Translate(model);
            ctx.RaiseSqlGenerated(dbq.Sql);

            int affected = 0;
            try
            {
                ctx.RaiseSqlDispatch(dbq.Sql);
                affected = await Update(dbq.Sql, dbq.Parameters, Ct).ConfigureAwait(false);
                ctx.RaiseSuccess(dbq.Sql);
            }
            catch(Exception ex) { ctx.RaiseError(dbq.Sql, ex); affected = -1; }
            return affected;
        }

        #endregion

        #region DTO API (strong typed, no IL fallback)

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

            List<T> list = new List<T>();

            await WithCommandAsync(dbq.Sql,dbq.Parameters, async delegate (DbCommand cmd)
            {
                using(DbDataReader reader = await ExecuteReaderSafeAsync(cmd, Ct).ConfigureAwait(false))
                {
                    Type type = typeof(T);
                    PropertyInfo[] props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                    while(await ReadSafeAsync(reader, Ct).ConfigureAwait(false))
                    {
                        Ct.ThrowIfCancellationRequested();
                        T inst = new T();
                        for(int i = 0; i < props.Length; i++)
                        {
                            PropertyInfo p = props[i];
                            if(!reader.HasColumn(p.Name))
                                continue;

                            object val = reader[p.Name];
                            if(val is DBNull) continue;

                            try
                            {
                                object converted = Convert.ChangeType(val, p.PropertyType, CultureInfo.InvariantCulture);
                                p.SetValue(inst, converted, null);
                            }
                            catch
                            {
                            }
                        }
                        list.Add(inst);
                    }
                }

                return 0;
            }, Ct).ConfigureAwait(false);

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

            await WithCommandAsync(dbq.Sql, dbq.Parameters, async delegate (DbCommand cmd)
            {
                using(DbDataReader reader = await ExecuteReaderSafeAsync(cmd, Ct).ConfigureAwait(false))
                {
                    Type type = typeof(T);
                    PropertyInfo[] props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                    while(await ReadSafeAsync(reader, Ct).ConfigureAwait(false))
                    {
                        Ct.ThrowIfCancellationRequested();
                        T inst = new T();
                        for(int i = 0; i < props.Length; i++)
                        {
                            PropertyInfo p = props[i];
                            if(!reader.HasColumn(p.Name))
                                continue;

                            object val = reader[p.Name];
                            if(val is DBNull) continue;

                            try
                            {
                                object converted = Convert.ChangeType(val, p.PropertyType, CultureInfo.InvariantCulture);
                                p.SetValue(inst, converted, null);
                            }
                            catch
                            {
                            }
                        }

                        Callback(inst);
                    }
                }

                return 0;
            }, Ct).ConfigureAwait(false);
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

            try
            {
                return StreamDtoCore<T>(dbq, ct);
            }
            catch(Exception ex)
            {
                ctx.RaiseError(dbq.Sql, ex);
                throw;
            }
        }

        private async IAsyncEnumerable<T> StreamDtoCore<T>(DatabaseQuery dbq, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct) where T : new()
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

                while(await ReadSafeAsync(reader, ct).ConfigureAwait(false))
                {
                    ct.ThrowIfCancellationRequested();

                    var record = new Dictionary<string, RecordItem>(StringComparer.OrdinalIgnoreCase);

                    foreach(string col in schema.Columns)
                    {
                        record[col] = new RecordItem
                        {
                            Name = col,
                            Type = (schema.ColumnTypes[col]?.FullName) ?? typeof(string).FullName!,
                            Value = reader[col] is DBNull ? string.Empty : (Convert.ToString(reader[col], CultureInfo.InvariantCulture) ?? string.Empty)
                        };
                    }

                    yield return DataMapper.Map<T>(record);
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
