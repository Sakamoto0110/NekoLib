#nullable enable
 using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Data.Common;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Data.Internal.Gateway.Dynamic;
using NekoLib.Data.Internal.Gateway.Query;
using NekoLib.Data.Internal.Gateway;
using NekoLib.Data.Query;
namespace NekoLib.Data.Internal.Gateway
{
    public partial class DatabaseGateway
    {
        #region RuntimeTypeFactory + FillDynamicObject

        private static class RuntimeTypeFactory
        {
            private static readonly AssemblyName _assemblyName = new AssemblyName("DynamicRowTypesAsm");
            private static readonly ModuleBuilder _module;
            private static readonly Dictionary<string, Type> _cache = new Dictionary<string, Type>(StringComparer.Ordinal);
            private static readonly LinkedList<string> _lru = new LinkedList<string>();
            private static int _maxTypes = 256;
            private static int _typeCounter;
            private static readonly object _sync = new object();

            static RuntimeTypeFactory()
            {
                AssemblyBuilder ab = AssemblyBuilder.DefineDynamicAssembly(
                    _assemblyName, AssemblyBuilderAccess.Run);
                _module = ab.DefineDynamicModule("MainModule");
            }

            public static int Count
            {
                get { lock(_sync) return _cache.Count; }
            }

            public static void ConfigureMaxTypes(int maxTypes)
            {
                if(maxTypes < 1) maxTypes = 1;
                lock(_sync)
                {
                    _maxTypes = maxTypes;
                    EnforceCapacity();
                }
            }

            public static bool TryGetExisting(SchemaInfo schema, out Type? type)
            {
                if(schema == null) throw new ArgumentNullException(nameof(schema));
                string signature = BuildSignature(schema);
                lock(_sync)
                {
                    if(_cache.TryGetValue(signature, out var t))
                    {
                        MoveToTail(signature);
                        type = t;
                        return true;
                    }
                }
                type = null;
                return false;
            }

            public static Type GetOrCreate(SchemaInfo Schema)
            {
                if(Schema == null) throw new ArgumentNullException(nameof(Schema));

                string signature = BuildSignature(Schema);

                lock(_sync)
                {
                    Type t;
                    if(_cache.TryGetValue(signature, out t))
                    {
                        MoveToTail(signature);
                        return t;
                    }

                    Type created = CreateType(Schema);
                    _cache[signature] = created;
                    _lru.AddLast(signature);
                    EnforceCapacity();
                    return created;
                }
            }

            private static string BuildSignature(SchemaInfo Schema)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();

                List<string> ordered = new List<string>(Schema.Columns);
                ordered.Sort(StringComparer.OrdinalIgnoreCase);

                for(int i = 0; i < ordered.Count; i++)
                {
                    string col = ordered[i];
                    Type t;
                    if(!Schema.ColumnTypes.TryGetValue(col, out t) || t == null)
                        t = typeof(string);

                    sb.Append(col.ToLowerInvariant());
                    sb.Append(':');
                    sb.Append(t.FullName);
                    sb.Append('|');
                }

                return sb.ToString();
            }

            private static Type CreateType(SchemaInfo Schema)
            {
                string typeName = "DynamicRowType_" + System.Threading.Interlocked.Increment(ref _typeCounter);
                TypeBuilder tb = _module.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class);

                for(int i = 0; i < Schema.Columns.Count; i++)
                {
                    string propName = Schema.Columns[i];
                    Type propType;
                    if(!Schema.ColumnTypes.TryGetValue(propName, out propType) || propType == null)
                        propType = typeof(string);

                    FieldBuilder field = tb.DefineField("_" + propName, propType, FieldAttributes.Private);
                    PropertyBuilder prop = tb.DefineProperty(
                        propName,
                        System.Reflection.PropertyAttributes.HasDefault,
                        propType,
                        null);

                    MethodBuilder getter = tb.DefineMethod(
                        "get_" + propName,
                        MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                        propType,
                        Type.EmptyTypes);

                    ILGenerator ilGet = getter.GetILGenerator();
                    ilGet.Emit(OpCodes.Ldarg_0);
                    ilGet.Emit(OpCodes.Ldfld, field);
                    ilGet.Emit(OpCodes.Ret);

                    MethodBuilder setter = tb.DefineMethod(
                        "set_" + propName,
                        MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                        null,
                        new Type[] { propType });

                    ILGenerator ilSet = setter.GetILGenerator();
                    ilSet.Emit(OpCodes.Ldarg_0);
                    ilSet.Emit(OpCodes.Ldarg_1);
                    ilSet.Emit(OpCodes.Stfld, field);
                    ilSet.Emit(OpCodes.Ret);

                    prop.SetGetMethod(getter);
                    prop.SetSetMethod(setter);
                }

                Type createdType = tb.CreateType();
                return createdType;
            }

            private static void MoveToTail(string Key)
            {
                LinkedListNode<string> node = _lru.Find(Key);
                if(node != null)
                {
                    _lru.Remove(node);
                    _lru.AddLast(node);
                }
            }

            private static void EnforceCapacity()
            {
                while(_lru.Count > _maxTypes)
                {
                    LinkedListNode<string> first = _lru.First;
                    if(first == null) break;

                    string key = first.Value;
                    _lru.RemoveFirst();
                    _cache.Remove(key);
                }
            }
        }

        private static void FillDynamicObject(object Instance, Type RuntimeType, SchemaInfo Schema, DbDataReader Reader)
        {
            for(int i = 0; i < Schema.Columns.Count; i++)
            {
                string col = Schema.Columns[i];
                PropertyInfo pi = RuntimeType.GetProperty(col);
                if(pi == null || !pi.CanWrite) continue;

                object raw = Reader[col];
                if(raw is DBNull) continue;

                try
                {
                    Type targetType = pi.PropertyType;
                    if(targetType.IsGenericType &&
                        targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        targetType = Nullable.GetUnderlyingType(targetType);
                    }

                    object converted = Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture);
                    pi.SetValue(Instance, converted, null);
                }
                catch
                {
                    try { pi.SetValue(Instance, raw, null); } catch { }
                }
            }
        }

        
        #region Dynamic mode helpers

        private DynamicMode ResolveDynamicMode(QueryExecutionContext ctx)
        {
            var opts = ctx?.Options ?? new DatabaseGatewayOptions();
            opts.Validate();

            // Hard disable
            if((opts.DynamicMode & DynamicMode.Disabled) == DynamicMode.Disabled)
                return DynamicMode.Disabled;

            // If IL requested but not supported (AOT), fallback or fail.
            bool wantsIL = (opts.DynamicMode & DynamicMode.IL) == DynamicMode.IL;
            if(wantsIL && !PlatformGuards.SupportsDynamicIL())
            {
                if(opts.AllowExpandoFallback && (opts.DynamicMode & DynamicMode.Expando) == DynamicMode.Expando)
                    return DynamicMode.Expando;

                throw new PlatformNotSupportedException("DynamicMode.IL is not supported on this runtime (AOT). Use Expando or DTO.");
            }

            if(wantsIL) return DynamicMode.IL;

            if((opts.DynamicMode & DynamicMode.Expando) == DynamicMode.Expando)
                return DynamicMode.Expando;

            return DynamicMode.Disabled;
        }

        private DynamicRow CreateDynamicRow(QueryExecutionContext ctx, SchemaInfo schema, DbDataReader reader)
        {
            var mode = ResolveDynamicMode(ctx);

            if(mode == DynamicMode.Disabled)
                throw new InvalidOperationException("Dynamic mode is disabled by options.");

            if(mode == DynamicMode.Expando)
            {
                IDictionary<string, object?> exp = new ExpandoObject();
                for(int i = 0; i < schema.Columns.Count; i++)
                {
                    string col = schema.Columns[i];
                    object raw = reader[col];
                    exp[col] = raw is DBNull ? null : raw;
                }
                return new DynamicRow(exp);
            }

            // IL mode (bounded)
            var opts = ctx.Options;
            RuntimeTypeFactory.ConfigureMaxTypes(opts.MaxDynamicSchemas);

            if(!RuntimeTypeFactory.TryGetExisting(schema, out var existing))
            {
                if(RuntimeTypeFactory.Count >= opts.MaxDynamicSchemas)
                {
                    if(opts.FailOnDynamicSchemaLimit || !opts.AllowExpandoFallback || (opts.DynamicMode & DynamicMode.Expando) != DynamicMode.Expando)
                        throw new InvalidOperationException($"Dynamic IL schema limit reached ({opts.MaxDynamicSchemas}).");

                    // fallback to Expando
                    IDictionary<string, object?> exp = new ExpandoObject();
                    for(int i = 0; i < schema.Columns.Count; i++)
                    {
                        string col = schema.Columns[i];
                        object raw = reader[col];
                        exp[col] = raw is DBNull ? null : raw;
                    }
                    return new DynamicRow(exp);
                }
            }

            Type ilType = existing ?? RuntimeTypeFactory.GetOrCreate(schema);

            object inst = Activator.CreateInstance(ilType)!;
            FillDynamicObject(inst, ilType, schema, reader);
            return new DynamicRow(inst);
        }

        #endregion

#endregion

        #region Dynamic API (IL + DynamicRow, no DTO)

        public async Task<List<DynamicRow>> GetDynamic(QueryExecutionContext ctx,  QueryBuilder Builder,  CancellationToken Ct = default)
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));
            if(ctx == null) throw new ArgumentNullException(nameof(ctx));

            List<DynamicRow> list = new List<DynamicRow>();
            await ReadDynamic(ctx, Builder, delegate (DynamicRow row) { list.Add(row); }, Ct)
                .ConfigureAwait(false);
            return list;
        }

        public async Task ReadDynamic(QueryExecutionContext ctx, QueryBuilder Builder ,Action<DynamicRow> Callback,CancellationToken Ct = default)
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));
            if(Callback == null) throw new ArgumentNullException(nameof(Callback));
            if(ctx == null) throw new ArgumentNullException(nameof(ctx));

            var translator = ctx.Translator;
            QueryModel model = Builder.Build();
            ctx.RaiseSqlGenerated(model.Sql);
            DatabaseQuery dbq = translator.Translate(model);

            await WithCommandAsync(ctx, dbq.Sql,  dbq.Parameters, async delegate (DbCommand cmd)
            {
                using(DbDataReader reader = await ExecuteReaderSafeAsync(cmd, Ct).ConfigureAwait(false))
                {
                    SchemaInfo schema = ExtractSchema(reader);

                    if(schema.Columns.Count == 0)
                        return 0;

                    while(await ReadSafeAsync(reader, Ct).ConfigureAwait(false))
                    {
                        DynamicRow row = CreateDynamicRow(ctx, schema, reader);
                        try
                        {
                            Callback(row);
                        }
                        catch(Exception ex) { ctx.RaiseError(dbq.Sql,ex); }
                        
                    }
                    
                }

                return 0;
            }, Ct).ConfigureAwait(false);
        }








        #if NET6_0_OR_GREATER
        #region DataStreaming

        public IAsyncEnumerable<DynamicRow> StreamDynamic(QueryExecutionContext ctx,  QueryBuilder builder, CancellationToken ct = default)

        {
            if(ctx == null) throw new ArgumentNullException(nameof(ctx));
            if(builder == null) throw new ArgumentNullException(nameof(builder));

            var translator = ctx.Translator;
            QueryModel model = builder.Build();
            DatabaseQuery dbq = translator.Translate(model);

            ctx.RaiseSqlGenerated(dbq.Sql);

            try
            {
                return StreamDynamicInternal(ctx, dbq, ct);
            }
            catch(Exception ex)
            {
                ctx.RaiseError(dbq.Sql, ex);
                throw;
            }
        }
        private async IAsyncEnumerable<DynamicRow> StreamDynamicInternal(QueryExecutionContext ctx, DatabaseQuery dbq, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
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

                if(schema.Columns.Count == 0)
                    yield break;

                while(await ReadSafeAsync(reader, ct).ConfigureAwait(false))
                {
                    ct.ThrowIfCancellationRequested();
                    yield return CreateDynamicRow(ctx, schema, reader);
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

        #endregion
    }
}