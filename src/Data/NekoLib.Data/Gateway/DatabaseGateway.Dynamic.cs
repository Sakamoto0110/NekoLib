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
using NekoLib.Data.Query;
using NekoLib.Data.Gateway;
using NekoLib.Data.Dynamic;
namespace NekoLib.Data.Gateway
{
    public partial class DatabaseGateway
    {
        #region RuntimeTypeFactory + FillDynamicObject

        private static class RuntimeTypeFactory
        {
            private static readonly AssemblyName _assemblyName = new AssemblyName("DynamicRowTypesAsm");
            private static readonly ModuleBuilder _module;
            private static readonly Dictionary<string, Type> _cache = new Dictionary<string, Type>(StringComparer.Ordinal);
            private static int _maxTypes = 256;
            private static bool _limitLocked;
            private static int _typeCounter;
            private static long _cacheHits;
            private static long _cacheMisses;
            private static long _limitRejections;
            private static readonly object _sync = new object();

            static RuntimeTypeFactory()
            {
                AssemblyBuilder ab = AssemblyBuilder.DefineDynamicAssembly(
                    _assemblyName, AssemblyBuilderAccess.Run);
                _module = ab.DefineDynamicModule("MainModule");
            }

            public static DynamicIlMetrics GetMetrics()
            {
                lock(_sync)
                {
                    return new DynamicIlMetrics(
                        _maxTypes,
                        _cache.Count,
                        _cacheHits,
                        _cacheMisses,
                        _limitRejections);
                }
            }

            public static void ConfigureMaxTypes(int maxTypes)
            {
                if(maxTypes < 1) maxTypes = 1;
                lock(_sync)
                {
                    if(_limitLocked)
                        return;

                    _maxTypes = maxTypes;
                    _limitLocked = true;
                }
            }

            public static bool TryGetOrCreate(SchemaInfo schema, out Type? type)
            {
                if(schema == null) throw new ArgumentNullException(nameof(schema));
                string signature = BuildSignature(schema);
                lock(_sync)
                {
                    if(_cache.TryGetValue(signature, out var t))
                    {
                        _cacheHits++;
                        type = t;
                        return true;
                    }

                    _cacheMisses++;
                    if(_cache.Count >= _maxTypes)
                    {
                        _limitRejections++;
                        type = null;
                        return false;
                    }

                    Type created = CreateType(schema);
                    _cache[signature] = created;
                    type = created;
                    return true;
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
                    Type? t;
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
                    Type? sourceType;
                    if(!Schema.ColumnTypes.TryGetValue(propName, out sourceType) || sourceType == null)
                        sourceType = typeof(string);
                    Type propType = GetNullablePropertyType(sourceType);

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

                Type createdType = tb.CreateType() ?? throw new InvalidOperationException("Failed to create dynamic row type.");
                return createdType;
            }

            private static Type GetNullablePropertyType(Type sourceType)
            {
                if(!sourceType.IsValueType || Nullable.GetUnderlyingType(sourceType) != null)
                    return sourceType;

                return typeof(Nullable<>).MakeGenericType(sourceType);
            }
        }

        /// <summary>
        /// Returns a process-wide snapshot of emitted dynamic IL schemas.
        /// </summary>
        public static DynamicIlMetrics GetDynamicIlMetrics()
        {
            return RuntimeTypeFactory.GetMetrics();
        }

        private static void FillDynamicObject(object Instance, Type RuntimeType, SchemaInfo Schema, DbDataReader Reader)
        {
            for(int i = 0; i < Schema.Columns.Count; i++)
            {
                string col = Schema.Columns[i];
                PropertyInfo? pi = RuntimeType.GetProperty(col);
                if(pi == null || !pi.CanWrite) continue;

                object raw = Reader.GetValue(Schema.Ordinals[col]);
                if(raw is DBNull) continue;

                try
                {
                    Type targetType = pi.PropertyType;
                    if(targetType.IsGenericType &&
                        targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                    }

                    object? converted = Convert.ChangeType(raw, targetType, CultureInfo.InvariantCulture);
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

        private DynamicRow CreateDynamicRow(SchemaInfo schema, DbDataReader reader)
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
                    object raw = reader.GetValue(schema.Ordinals[col]);
                    exp[col] = raw is DBNull ? null : raw;
                }
                return new DynamicRow(exp);
            }

            // IL mode (bounded)
            var opts = ctx.Options;
            RuntimeTypeFactory.ConfigureMaxTypes(opts.MaxDynamicSchemas);

            Type? ilType;
            if(!RuntimeTypeFactory.TryGetOrCreate(schema, out ilType))
            {
                DynamicIlMetrics metrics = RuntimeTypeFactory.GetMetrics();
                if(opts.FailOnDynamicSchemaLimit || !opts.AllowExpandoFallback || (opts.DynamicMode & DynamicMode.Expando) != DynamicMode.Expando)
                    throw new InvalidOperationException($"Dynamic IL schema limit reached ({metrics.SchemaLimit}).");

                IDictionary<string, object?> exp = new ExpandoObject();
                for(int i = 0; i < schema.Columns.Count; i++)
                {
                    string col = schema.Columns[i];
                    object raw = reader.GetValue(schema.Ordinals[col]);
                    exp[col] = raw is DBNull ? null : raw;
                }
                return new DynamicRow(exp);
            }

            object inst = Activator.CreateInstance(ilType!)!;
            FillDynamicObject(inst, ilType!, schema, reader);
            return new DynamicRow(inst);
        }

        #endregion

#endregion

        #region Dynamic API (IL + DynamicRow, no DTO)

        public async Task<List<DynamicRow>> GetDynamic(QueryBuilder Builder,  CancellationToken Ct = default)
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));

            List<DynamicRow> list = new List<DynamicRow>();
            await ReadDynamic(Builder, delegate (DynamicRow row) { list.Add(row); }, Ct)
                .ConfigureAwait(false);
            return list;
        }

        public async Task<List<DynamicRow>> GetDynamic(QueryBuilder Builder, DbSession session, CancellationToken Ct = default)
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));

            List<DynamicRow> list = new List<DynamicRow>();
            await ReadDynamic(Builder, delegate (DynamicRow row) { list.Add(row); }, session, Ct)
                .ConfigureAwait(false);
            return list;
        }

        public Task ReadDynamic(QueryBuilder Builder ,Action<DynamicRow> Callback,CancellationToken Ct = default)
        {
            return ReadDynamicFromBuilder(Builder, Callback, null, Ct);
        }

        public Task ReadDynamic(QueryBuilder Builder ,Action<DynamicRow> Callback, DbSession session, CancellationToken Ct = default)
        {
            return ReadDynamicFromBuilder(Builder, Callback, session, Ct);
        }

        private async Task ReadDynamicFromBuilder(QueryBuilder Builder ,Action<DynamicRow> Callback, DbSession? session, CancellationToken Ct)
        {
            if(Builder == null) throw new ArgumentNullException(nameof(Builder));
            if(Callback == null) throw new ArgumentNullException(nameof(Callback));


            var translator = ctx.Translator;
            QueryModel model = Builder.Build();
            DatabaseQuery dbq = translator.Translate(model);
            ctx.RaiseSqlGenerated(dbq.Sql);

            await ReadDynamicFromSql(
                dbq.Sql,
                dbq.Parameters,
                Callback,
                Ct,
                session,
                dbq.CommandPolicy,
                dbq.LogicalParameters).ConfigureAwait(false);
        }

        private async Task ReadDynamicFromSql(
            string sql,
            Dictionary<string, object?>? parameters,
            Action<DynamicRow> callback,
            CancellationToken ct = default,
            DbSession? session = null,
            DbCommandPolicy? commandPolicy = null,
            IReadOnlyList<LogicalParameter>? logicalParameters = null)
        {
            await WithCommandAsync(sql, parameters, async delegate (DbCommand cmd)
            {
                using(DbDataReader reader = await ExecuteReaderSafeAsync(cmd, ct).ConfigureAwait(false))
                {
                    SchemaInfo schema = ExtractSchema(reader);

                    if(schema.Columns.Count == 0)
                        return 0;

                    while(await ReadSafeAsync(reader, ct).ConfigureAwait(false))
                    {
                        ct.ThrowIfCancellationRequested();
                        DynamicRow row = CreateDynamicRow(schema, reader);
                        callback(row);

                    }

                }

                return 0;
            }, ct, session, commandPolicy, logicalParameters).ConfigureAwait(false);
        }








        #if NET6_0_OR_GREATER
        #region DataStreaming

        public IAsyncEnumerable<DynamicRow> StreamDynamic(QueryBuilder builder, CancellationToken ct = default)
        {
            return StreamDynamicFromBuilder(builder, null, ct);
        }

        /// <remarks>
        /// The <see cref="DbDataReader"/> remains open for the entire enumeration,
        /// so the <paramref name="session"/> connection and transaction remain
        /// occupied while the consumer iterates. Avoid slow per-row I/O inside
        /// an open transaction; consume promptly or materialize the results.
        /// </remarks>
        public IAsyncEnumerable<DynamicRow> StreamDynamic(QueryBuilder builder, DbSession session, CancellationToken ct = default)
        {
            return StreamDynamicFromBuilder(builder, session, ct);
        }

        private IAsyncEnumerable<DynamicRow> StreamDynamicFromBuilder(QueryBuilder builder, DbSession? session, CancellationToken ct)
        {
            if(builder == null) throw new ArgumentNullException(nameof(builder));

            var translator = ctx.Translator;
            QueryModel model = builder.Build();
            DatabaseQuery dbq = translator.Translate(model);

            ctx.RaiseSqlGenerated(dbq.Sql);

            return StreamDynamicInternal(dbq, session, ct);
        }
        private async IAsyncEnumerable<DynamicRow> StreamDynamicInternal(DatabaseQuery dbq, DbSession? session, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
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
                        conn = GetSessionConnection(session);
                    }
                    else
                    {
                        conn = await OpenConnectionAsync(ct).ConfigureAwait(false);
                        ownsConnection = true;
                    }

                    cmd = conn.CreateCommand();
                    cmd.CommandText = dbq.Sql;
                    ApplyCommandPolicy(cmd, dbq.CommandPolicy);
                    if(session?.Transaction != null)
                        cmd.Transaction = session.Transaction;
                    Dictionary<string, object?> effectiveParameters =
                        await PrepareLogicalParametersAsync(
                            cmd,
                            dbq.Parameters,
                            dbq.LogicalParameters,
                            session,
                            ct).ConfigureAwait(false);
                    ApplyParameters(cmd, effectiveParameters);
                    ctx.RaiseSqlDispatch(dbq.Sql);

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

                if(schema!.Columns.Count == 0)
                {
                    terminal.Complete();
                    yield break;
                }

                while(true)
                {
                    DynamicRow row;
                    try
                    {
                        if(!await ReadSafeAsync(reader, ct).ConfigureAwait(false))
                            break;

                        ct.ThrowIfCancellationRequested();
                        row = CreateDynamicRow(schema, reader!);
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

        #endregion
    }
}
