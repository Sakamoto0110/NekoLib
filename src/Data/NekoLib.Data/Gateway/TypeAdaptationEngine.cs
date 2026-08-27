#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Data.Query;

namespace NekoLib.Data.Gateway
{
    public sealed partial class DatabaseGateway
    {
        private readonly SemaphoreSlim _schemaDiscoveryGate = new SemaphoreSlim(1, 1);
        private readonly object _schemaCacheSync = new object();
        private readonly Dictionary<string, SchemaColumnMetadata> _schemaColumns =
            new Dictionary<string, SchemaColumnMetadata>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _loadedProviderCatalogs =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Type> _providerTypesByName =
            new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Type> _providerTypesByCode =
            new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Reports completed logical promotions and representation decays.
        /// Subscribers cannot authorize adaptation and their failures are isolated.
        /// </summary>
        public event Action<TypeAdaptationEventArgs>? OnTypeAdaptation;

        /// <summary>Loads selected column metadata into this gateway's cache.</summary>
        public async Task PreloadSchemaAsync(
            string table,
            IEnumerable<string> columns,
            CancellationToken cancellationToken = default)
        {
            string normalizedTable = NormalizeRequiredTable(table, nameof(table));
            List<string> normalizedColumns = NormalizeRequiredColumns(columns);
            using (DbConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false))
            {
                ProviderProfile profile = ProviderProfile.Resolve(connection, ctx.Translator);
                await LoadSchemaColumnsAsync(
                    connection,
                    profile,
                    normalizedTable,
                    normalizedColumns,
                    false,
                    cancellationToken).ConfigureAwait(false);

                EnsureSchemaLoaded(profile, normalizedTable, normalizedColumns);
            }
        }

        /// <summary>Invalidates and reloads selected cached column metadata.</summary>
        public async Task RefreshSchemaAsync(
            string table,
            IEnumerable<string> columns,
            CancellationToken cancellationToken = default)
        {
            string normalizedTable = NormalizeRequiredTable(table, nameof(table));
            List<string> normalizedColumns = NormalizeRequiredColumns(columns);
            using (DbConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false))
            {
                ProviderProfile profile = ProviderProfile.Resolve(connection, ctx.Translator);
                await LoadSchemaColumnsAsync(
                    connection,
                    profile,
                    normalizedTable,
                    normalizedColumns,
                    true,
                    cancellationToken).ConfigureAwait(false);

                EnsureSchemaLoaded(profile, normalizedTable, normalizedColumns);
            }
        }

        /// <summary>Clears provider and column metadata cached by this gateway.</summary>
        public void ClearSchemaCache()
        {
            _schemaDiscoveryGate.Wait();
            try
            {
                lock (_schemaCacheSync)
                {
                    _schemaColumns.Clear();
                    _loadedProviderCatalogs.Clear();
                    _providerTypesByName.Clear();
                    _providerTypesByCode.Clear();
                }
            }
            finally
            {
                _schemaDiscoveryGate.Release();
            }
        }

        private async Task<Dictionary<string, object?>> PrepareLogicalParametersAsync(
            DbCommand command,
            Dictionary<string, object?>? parameters,
            IReadOnlyList<LogicalParameter>? logicalParameters,
            DbSession? session,
            CancellationToken cancellationToken)
        {
            Dictionary<string, object?> effective = parameters == null
                ? new Dictionary<string, object?>()
                : new Dictionary<string, object?>(parameters);
            if (logicalParameters == null || logicalParameters.Count == 0)
                return effective;

            ctx.Options.Validate();
            ProviderProfile profile = ProviderProfile.Resolve(command.Connection, ctx.Translator);
            Guid correlationId = Guid.NewGuid();

            for (int index = 0; index < logicalParameters.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                LogicalParameter logical = logicalParameters[index];
                object? supplied;
                if (!effective.TryGetValue(logical.Name, out supplied))
                    continue;
                if (supplied == null || supplied is DBNull || supplied is DbParameterSpec)
                    continue;

                object adapted = await AdaptLogicalValueAsync(
                    command.Connection,
                    profile,
                    logical.WithValue(supplied),
                    session,
                    correlationId,
                    cancellationToken).ConfigureAwait(false);
                effective[logical.Name] = adapted;
            }

            return effective;
        }

        private async Task<object> AdaptLogicalValueAsync(
            DbConnection? connection,
            ProviderProfile profile,
            LogicalParameter logical,
            DbSession? session,
            Guid correlationId,
            CancellationToken cancellationToken)
        {
            object current = logical.Value!;
            Type sourceType = current.GetType();
            Type semanticType = logical.SemanticType ?? sourceType;
            SchemaColumnMetadata? schema = null;

            if (!profile.IsKnown &&
                ctx.Options.TypePromotionPolicy == TypePromotionPolicy.SchemaValidated &&
                logical.PromotionRule == null &&
                logical.Table != null &&
                logical.Column != null)
            {
                throw Failure(
                    sourceType,
                    semanticType,
                    profile.Identity,
                    (TypePromotionRule?)null,
                    TypeAdaptationReasonCode.UnknownProvider);
            }

            if (logical.PromotionRule != null && sourceType != semanticType)
            {
                if (ctx.Options.TypePromotionPolicy == TypePromotionPolicy.Disabled)
                {
                    throw Failure(
                        sourceType,
                        semanticType,
                        profile.Identity,
                        logical.PromotionRule,
                        TypeAdaptationReasonCode.PromotionDisabled);
                }

                current = ApplyPromotion(
                    logical,
                    current,
                    logical.PromotionRule,
                    profile,
                    TypeAdaptationReasonCode.ExplicitRule,
                    correlationId);
                semanticType = logical.PromotionRule.TargetType;
            }
            else if (sourceType != semanticType)
            {
                TypePromotionRule? automaticRule = null;
                if (ctx.Options.TypePromotionPolicy == TypePromotionPolicy.SchemaValidated)
                {
                    schema = await GetRequiredSchemaAsync(
                        connection,
                        profile,
                        logical,
                        session,
                        cancellationToken).ConfigureAwait(false);
                    if (schema.SemanticType == null)
                    {
                        throw Failure(
                            sourceType,
                            semanticType,
                            profile.Identity,
                            (TypePromotionRule?)null,
                            TypeAdaptationReasonCode.SchemaUnavailable);
                    }
                    if (schema.SemanticType == semanticType)
                        automaticRule = FindAutomaticPromotion(sourceType, semanticType);
                }

                if (ctx.Options.TypePromotionPolicy == TypePromotionPolicy.Disabled)
                {
                    throw Failure(
                        sourceType,
                        semanticType,
                        profile.Identity,
                        (TypePromotionRule?)null,
                        TypeAdaptationReasonCode.PromotionDisabled);
                }
                if (automaticRule == null)
                {
                    throw Failure(
                        sourceType,
                        semanticType,
                        profile.Identity,
                        (TypePromotionRule?)null,
                        TypeAdaptationReasonCode.PromotionRuleMissing);
                }

                current = ApplyPromotion(
                    logical,
                    current,
                    automaticRule,
                    profile,
                    TypeAdaptationReasonCode.SchemaValidatedRule,
                    correlationId);
                semanticType = automaticRule.TargetType;
            }
            else if (logical.PromotionRule == null &&
                     ctx.Options.TypePromotionPolicy == TypePromotionPolicy.SchemaValidated &&
                     logical.Table != null && logical.Column != null)
            {
                schema = await GetRequiredSchemaAsync(
                    connection,
                    profile,
                    logical,
                    session,
                    cancellationToken).ConfigureAwait(false);
                Type? schemaType = schema.SemanticType;
                if (schemaType == null)
                {
                    throw Failure(
                        sourceType,
                        semanticType,
                        profile.Identity,
                        (TypePromotionRule?)null,
                        TypeAdaptationReasonCode.SchemaUnavailable);
                }
                if (schemaType != null && schemaType != sourceType)
                {
                    TypePromotionRule? automaticRule = FindAutomaticPromotion(sourceType, schemaType);
                    if (automaticRule == null)
                    {
                        throw Failure(
                            sourceType,
                            schemaType,
                            profile.Identity,
                            (TypePromotionRule?)null,
                            TypeAdaptationReasonCode.PromotionRuleMissing);
                    }

                    current = ApplyPromotion(
                        logical,
                        current,
                        automaticRule,
                        profile,
                        TypeAdaptationReasonCode.SchemaValidatedRule,
                        correlationId);
                    semanticType = automaticRule.TargetType;
                }
            }

            Type representationType = current.GetType();
            if (profile.Supports(representationType))
                return current;

            if (ctx.Options.TypeDecayPolicy == TypeDecayPolicy.Strict)
            {
                throw Failure(
                    semanticType,
                    null,
                    profile.Identity,
                    logical.DecayRule,
                    TypeAdaptationReasonCode.StrictDecayRejected);
            }

            List<TypeDecayRule> decayRules = new List<TypeDecayRule>(logical.DecayRules);
            bool explicitRules = decayRules.Count > 0;
            if (!explicitRules)
            {
                if (!profile.IsKnown)
                {
                    throw Failure(
                        semanticType,
                        null,
                        profile.Identity,
                        (TypeDecayRule?)null,
                        TypeAdaptationReasonCode.UnknownProvider);
                }

                schema = schema ?? await GetRequiredSchemaAsync(
                    connection,
                    profile,
                    logical,
                    session,
                    cancellationToken).ConfigureAwait(false);
                TypeDecayRule? automaticDecay =
                    FindAutomaticDecay(semanticType, schema.SemanticType, profile);
                if (automaticDecay != null)
                    decayRules.Add(automaticDecay);
            }

            if (decayRules.Count == 0)
            {
                throw Failure(
                    semanticType,
                    schema?.SemanticType,
                    profile.Identity,
                    (TypeDecayRule?)null,
                    TypeAdaptationReasonCode.ProviderRepresentationUnsupported);
            }

            List<TypeAdaptationAttempt> attempts = new List<TypeAdaptationAttempt>();
            for (int index = 0; index < decayRules.Count; index++)
            {
                TypeDecayRule decayRule = decayRules[index];
                if (decayRule.SourceType != semanticType &&
                    !decayRule.SourceType.IsInstanceOfType(current))
                {
                    attempts.Add(Attempt(decayRule, TypeAdaptationReasonCode.SourceTypeMismatch));
                    continue;
                }
                if (!profile.Supports(decayRule.TargetType))
                {
                    attempts.Add(Attempt(
                        decayRule,
                        TypeAdaptationReasonCode.ProviderRepresentationUnsupported));
                    continue;
                }
                if (!IsLossAllowed(decayRule.Loss, explicitRules))
                {
                    attempts.Add(Attempt(
                        decayRule,
                        TypeAdaptationReasonCode.LossyAdaptationNotAuthorized));
                    continue;
                }

                object decayed;
                try
                {
                    decayed = ConvertValue(
                        current,
                        decayRule.TargetType,
                        decayRule.StrategyId,
                        decayRule.Loss,
                        profile.Identity,
                        decayRule.Convert);
                }
                catch (TypeAdaptationException ex)
                {
                    attempts.Add(Attempt(decayRule, ex.ReasonCode));
                    continue;
                }

                attempts.Add(Attempt(decayRule, TypeAdaptationReasonCode.ProviderFallback));
                IReadOnlyList<TypeAdaptationAttempt> attemptSnapshot =
                    new ReadOnlyCollection<TypeAdaptationAttempt>(attempts.ToArray());
                NotifyTypeAdaptation(new TypeAdaptationEventArgs(
                    TypeAdaptationDirection.Write,
                    TypeAdaptationKind.Decay,
                    semanticType,
                    decayRule.TargetType,
                    profile.Identity,
                    decayRule.TargetType.FullName ?? decayRule.TargetType.Name,
                    logical.Table,
                    logical.Column,
                    logical.Name,
                    decayRule.StrategyId,
                    decayRule.Loss,
                    TypeAdaptationReasonCode.ProviderFallback,
                    decayRule.Format,
                    decayRule.CultureName,
                    attemptSnapshot,
                    correlationId));
                return decayed;
            }

            TypeAdaptationAttempt lastAttempt = attempts[attempts.Count - 1];
            throw new TypeAdaptationException(
                semanticType,
                lastAttempt.TargetType,
                profile.Identity,
                lastAttempt.StrategyId,
                lastAttempt.Loss,
                lastAttempt.ReasonCode,
                new ReadOnlyCollection<TypeAdaptationAttempt>(attempts.ToArray()));
        }

        private object ApplyPromotion(
            LogicalParameter logical,
            object value,
            TypePromotionRule rule,
            ProviderProfile profile,
            TypeAdaptationReasonCode reasonCode,
            Guid correlationId)
        {
            if (!rule.SourceType.IsInstanceOfType(value))
            {
                throw Failure(
                    value.GetType(),
                    rule.TargetType,
                    profile.Identity,
                    rule,
                    TypeAdaptationReasonCode.SourceTypeMismatch);
            }
            bool explicitRule = ReferenceEquals(logical.PromotionRule, rule);
            EnsureLossAllowed(rule.Loss, explicitRule, value.GetType(), rule.TargetType, profile, rule.StrategyId);

            object promoted = ConvertValue(
                value,
                rule.TargetType,
                rule.StrategyId,
                rule.Loss,
                profile.Identity,
                rule.Convert);
            NotifyTypeAdaptation(new TypeAdaptationEventArgs(
                TypeAdaptationDirection.Write,
                TypeAdaptationKind.Promotion,
                value.GetType(),
                rule.TargetType,
                profile.Identity,
                rule.TargetType.FullName ?? rule.TargetType.Name,
                logical.Table,
                logical.Column,
                logical.Name,
                rule.StrategyId,
                rule.Loss,
                reasonCode,
                rule.Format,
                rule.CultureName,
                Attempts(
                    rule.StrategyId,
                    rule.TargetType,
                    rule.Loss,
                    reasonCode,
                    rule.Format,
                    rule.CultureName),
                correlationId));
            return promoted;
        }

        private void EnsureLossAllowed(
            TypeAdaptationLoss loss,
            bool explicitRule,
            Type sourceType,
            Type targetType,
            ProviderProfile profile,
            string strategyId)
        {
            if (IsLossAllowed(loss, explicitRule))
                return;
            throw new TypeAdaptationException(
                sourceType,
                targetType,
                profile.Identity,
                strategyId,
                loss,
                TypeAdaptationReasonCode.LossyAdaptationNotAuthorized);
        }

        private bool IsLossAllowed(TypeAdaptationLoss loss, bool explicitRule)
        {
            return loss == TypeAdaptationLoss.Lossless ||
                   (explicitRule &&
                    ctx.Options.TypeLossPolicy == TypeLossPolicy.AllowExplicitAndReport);
        }

        private static object ConvertValue(
            object value,
            Type targetType,
            string strategyId,
            TypeAdaptationLoss loss,
            string providerIdentity,
            Func<object, object?> converter)
        {
            try
            {
                object? converted = converter(value);
                if (converted == null || !targetType.IsInstanceOfType(converted))
                {
                    throw new TypeAdaptationException(
                        value.GetType(),
                        targetType,
                        providerIdentity,
                        strategyId,
                        loss,
                        TypeAdaptationReasonCode.ConversionRejected);
                }
                return converted;
            }
            catch (TypeAdaptationException)
            {
                throw;
            }
            catch (OverflowException)
            {
                throw new TypeAdaptationException(
                    value.GetType(),
                    targetType,
                    providerIdentity,
                    strategyId,
                    loss,
                    TypeAdaptationReasonCode.Overflow);
            }
            catch
            {
                throw new TypeAdaptationException(
                    value.GetType(),
                    targetType,
                    providerIdentity,
                    strategyId,
                    loss,
                    TypeAdaptationReasonCode.ConversionRejected);
            }
        }

        private TypePromotionRule? FindAutomaticPromotion(Type sourceType, Type targetType)
        {
            IList<TypePromotionRule> rules = ctx.Options.AutomaticPromotionRules;
            for (int index = 0; index < rules.Count; index++)
            {
                TypePromotionRule rule = rules[index];
                if (rule.SourceType == sourceType &&
                    rule.TargetType == targetType &&
                    rule.Loss == TypeAdaptationLoss.Lossless)
                {
                    return rule;
                }
            }
            return null;
        }

        private TypeDecayRule? FindAutomaticDecay(
            Type sourceType,
            Type? schemaType,
            ProviderProfile profile)
        {
            if (schemaType == null)
                return null;

            IList<TypeDecayRule> rules = ctx.Options.AutomaticDecayRules;
            for (int index = 0; index < rules.Count; index++)
            {
                TypeDecayRule rule = rules[index];
                if (rule.SourceType == sourceType &&
                    rule.TargetType == schemaType &&
                    rule.Loss == TypeAdaptationLoss.Lossless &&
                    profile.Supports(rule.TargetType))
                {
                    return rule;
                }
            }
            return null;
        }

        private async Task<SchemaColumnMetadata> GetRequiredSchemaAsync(
            DbConnection? connection,
            ProviderProfile profile,
            LogicalParameter logical,
            DbSession? session,
            CancellationToken cancellationToken)
        {
            string? table = TryNormalizeTable(logical.Table);
            string? column = TryNormalizeIdentifier(logical.Column);
            if (table == null || column == null || connection == null)
            {
                throw Failure(
                    logical.Value?.GetType() ?? typeof(object),
                    logical.SemanticType,
                    profile.Identity,
                    logical.PromotionRule,
                    TypeAdaptationReasonCode.SchemaUnavailable);
            }

            SchemaColumnMetadata? cached = GetCachedSchema(profile.CacheIdentity, table, column);
            if (cached != null)
                return cached;

            if (ctx.Options.SchemaDiscoveryMode == SchemaDiscoveryMode.Disabled)
            {
                throw Failure(
                    logical.Value?.GetType() ?? typeof(object),
                    logical.SemanticType,
                    profile.Identity,
                    logical.PromotionRule,
                    TypeAdaptationReasonCode.SchemaUnavailable);
            }
            if (ctx.Options.SchemaDiscoveryMode == SchemaDiscoveryMode.Preload)
            {
                throw Failure(
                    logical.Value?.GetType() ?? typeof(object),
                    logical.SemanticType,
                    profile.Identity,
                    logical.PromotionRule,
                    TypeAdaptationReasonCode.SchemaNotPreloaded);
            }
            if (session?.Transaction != null)
            {
                throw Failure(
                    logical.Value?.GetType() ?? typeof(object),
                    logical.SemanticType,
                    profile.Identity,
                    logical.PromotionRule,
                    TypeAdaptationReasonCode.SchemaRequiredBeforeTransaction);
            }

            await LoadSchemaColumnsAsync(
                connection,
                profile,
                table,
                new[] { column },
                false,
                cancellationToken).ConfigureAwait(false);
            cached = GetCachedSchema(profile.CacheIdentity, table, column);
            if (cached == null)
            {
                throw Failure(
                    logical.Value?.GetType() ?? typeof(object),
                    logical.SemanticType,
                    profile.Identity,
                    logical.PromotionRule,
                    TypeAdaptationReasonCode.SchemaUnavailable);
            }
            return cached;
        }

        private async Task LoadSchemaColumnsAsync(
            DbConnection connection,
            ProviderProfile profile,
            string table,
            IReadOnlyList<string> columns,
            bool forceRefresh,
            CancellationToken cancellationToken)
        {
            await _schemaDiscoveryGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                LoadProviderTypeCatalog(connection, profile);

                if (forceRefresh)
                    ClearSchemaEntries(profile.CacheIdentity, table, columns);

                bool complete = true;
                for (int index = 0; index < columns.Count; index++)
                {
                    if (GetCachedSchema(profile.CacheIdentity, table, columns[index]) == null)
                    {
                        complete = false;
                        break;
                    }
                }
                if (complete)
                    return;

                DataTable schema;
                string? schemaName;
                string tableName;
                SplitNormalizedTable(table, out schemaName, out tableName);
                try
                {
                    schema = connection.GetSchema(
                        "Columns",
                        new string?[] { null, schemaName, tableName, null });
                }
                catch
                {
                    try
                    {
                        schema = connection.GetSchema("Columns");
                    }
                    catch
                    {
                        if (profile.Kind == ProviderKind.Sqlite)
                        {
                            await LoadSqliteColumnsAsync(
                                connection,
                                profile,
                                table,
                                columns,
                                cancellationToken).ConfigureAwait(false);
                        }
                        else if (profile.Kind == ProviderKind.Access)
                        {
                            LoadOleDbColumns(
                                connection,
                                profile,
                                table,
                                columns,
                                schemaName,
                                tableName);
                        }
                        return;
                    }
                }

                CacheSchemaRows(
                    schema,
                    profile,
                    table,
                    columns,
                    schemaName,
                    tableName);

                if (profile.Kind == ProviderKind.Access &&
                    !HasAllSchemaColumns(profile.CacheIdentity, table, columns))
                {
                    LoadOleDbColumns(
                        connection,
                        profile,
                        table,
                        columns,
                        schemaName,
                        tableName);
                }
            }
            finally
            {
                _schemaDiscoveryGate.Release();
            }
        }

        private void CacheSchemaRows(
            DataTable schema,
            ProviderProfile profile,
            string table,
            IReadOnlyList<string> columns,
            string? schemaName,
            string tableName)
        {
            for (int rowIndex = 0; rowIndex < schema.Rows.Count; rowIndex++)
            {
                DataRow row = schema.Rows[rowIndex];
                string? rowTable = ReadString(row, "TABLE_NAME", "TableName");
                string? rowSchema = ReadString(row, "TABLE_SCHEMA", "TableSchema", "OWNER", "Owner");
                string? rowColumn = ReadString(row, "COLUMN_NAME", "ColumnName");
                if (!string.Equals(TryNormalizeIdentifier(rowTable), tableName, StringComparison.OrdinalIgnoreCase) ||
                    (schemaName != null &&
                     !string.Equals(TryNormalizeIdentifier(rowSchema), schemaName, StringComparison.OrdinalIgnoreCase)) ||
                    rowColumn == null)
                {
                    continue;
                }

                string normalizedColumn;
                try
                {
                    normalizedColumn = NormalizeRequiredIdentifier(rowColumn, "column");
                }
                catch
                {
                    continue;
                }
                if (columns.Count > 0 && !ContainsIgnoreCase(columns, normalizedColumn))
                    continue;

                object? providerType = ReadValue(row, "DATA_TYPE", "DataType", "PROVIDER_TYPE", "ProviderType");
                string? providerTypeName = ReadString(row, "TYPE_NAME", "TypeName", "DATA_TYPE_NAME", "DataTypeName");
                Type? semanticType = ResolveSchemaType(
                    profile,
                    providerType,
                    providerTypeName);
                SchemaColumnMetadata metadata = new SchemaColumnMetadata(
                    table,
                    normalizedColumn,
                    providerTypeName ?? Convert.ToString(providerType, CultureInfo.InvariantCulture) ?? "unknown",
                    semanticType);
                SetCachedSchema(profile.CacheIdentity, metadata);
            }
        }

        private void LoadOleDbColumns(
            DbConnection connection,
            ProviderProfile profile,
            string table,
            IReadOnlyList<string> columns,
            string? schemaName,
            string tableName)
        {
            try
            {
                Type? schemaGuidType = connection.GetType().Assembly.GetType(
                    "System.Data.OleDb.OleDbSchemaGuid",
                    throwOnError: false);
                FieldInfo? columnsField = schemaGuidType?.GetField(
                    "Columns",
                    BindingFlags.Public | BindingFlags.Static);
                object? schemaGuid = columnsField?.GetValue(null);
                MethodInfo? getSchema = connection.GetType().GetMethod(
                    "GetOleDbSchemaTable",
                    new[] { typeof(Guid), typeof(object[]) });
                if (!(schemaGuid is Guid) || getSchema == null)
                    return;

                object[] restrictions = { null!, schemaName!, tableName, null! };
                DataTable? schema = getSchema.Invoke(
                    connection,
                    new object[] { (Guid)schemaGuid, restrictions }) as DataTable;
                if (schema != null)
                {
                    CacheSchemaRows(
                        schema,
                        profile,
                        table,
                        columns,
                        schemaName,
                        tableName);
                }
            }
            catch
            {
            }
        }

        private bool HasAllSchemaColumns(
            string provider,
            string table,
            IReadOnlyList<string> columns)
        {
            for (int index = 0; index < columns.Count; index++)
            {
                if (GetCachedSchema(provider, table, columns[index]) == null)
                    return false;
            }
            return true;
        }

        private async Task LoadSqliteColumnsAsync(
            DbConnection connection,
            ProviderProfile profile,
            string table,
            IReadOnlyList<string> columns,
            CancellationToken cancellationToken)
        {
            try
            {
                string? schemaName;
                string tableName;
                SplitNormalizedTable(table, out schemaName, out tableName);
                using (DbCommand command = connection.CreateCommand())
                {
                    command.CommandType = CommandType.Text;
                    command.CommandText = "PRAGMA " +
                        (schemaName == null
                            ? string.Empty
                            : "\"" + schemaName.Replace("\"", "\"\"") + "\".") +
                        "table_info(\"" + tableName.Replace("\"", "\"\"") + "\")";
                    using (DbDataReader reader = await ExecuteReaderSafeAsync(
                        command,
                        cancellationToken).ConfigureAwait(false))
                    {
                        int nameOrdinal = reader.GetOrdinal("name");
                        int typeOrdinal = reader.GetOrdinal("type");
                        while (await ReadSafeAsync(reader, cancellationToken).ConfigureAwait(false))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            string column = reader.IsDBNull(nameOrdinal)
                                ? string.Empty
                                : reader.GetString(nameOrdinal);
                            string? normalizedColumn = TryNormalizeIdentifier(column);
                            if (normalizedColumn == null ||
                                !ContainsIgnoreCase(columns, normalizedColumn))
                            {
                                continue;
                            }

                            string declaredType = reader.IsDBNull(typeOrdinal)
                                ? string.Empty
                                : reader.GetString(typeOrdinal);
                            SetCachedSchema(
                                profile.CacheIdentity,
                                new SchemaColumnMetadata(
                                    table,
                                    normalizedColumn,
                                    declaredType,
                                    MapSqliteTypeName(declaredType)));
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // The caller turns a missing cache entry into sanitized evidence.
            }
        }

        private void LoadProviderTypeCatalog(DbConnection connection, ProviderProfile profile)
        {
            lock (_schemaCacheSync)
            {
                if (_loadedProviderCatalogs.Contains(profile.CacheIdentity))
                    return;
            }

            try
            {
                DataTable types = connection.GetSchema("DataTypes");
                for (int index = 0; index < types.Rows.Count; index++)
                {
                    DataRow row = types.Rows[index];
                    string? name = ReadString(row, "TYPE_NAME", "TypeName", "DataTypeName");
                    object? code = ReadValue(row, "PROVIDER_DB_TYPE", "ProviderDbType", "DATA_TYPE", "DataType");
                    Type? clrType = ReadValue(row, "DATA_TYPE", "DataType") as Type;
                    if (clrType == null)
                        clrType = MapTypeName(name);

                    lock (_schemaCacheSync)
                    {
                        if (clrType != null && name != null)
                            _providerTypesByName[profile.CacheIdentity + "|" + name] = clrType;
                        if (clrType != null && code != null)
                        {
                            _providerTypesByCode[
                                profile.CacheIdentity + "|" + Convert.ToString(code, CultureInfo.InvariantCulture)] = clrType;
                        }
                    }
                }
            }
            catch
            {
            }

            lock (_schemaCacheSync)
                _loadedProviderCatalogs.Add(profile.CacheIdentity);
        }

        private Type? ResolveSchemaType(
            ProviderProfile profile,
            object? providerType,
            string? providerTypeName)
        {
            Type? direct = providerType as Type;
            if (direct != null)
                return direct;

            lock (_schemaCacheSync)
            {
                if (providerTypeName != null)
                {
                    Type? byName;
                    if (_providerTypesByName.TryGetValue(
                        profile.CacheIdentity + "|" + providerTypeName,
                        out byName))
                        return byName;
                }
                if (providerType != null)
                {
                    string code = Convert.ToString(providerType, CultureInfo.InvariantCulture) ?? string.Empty;
                    Type? byCode;
                    if (_providerTypesByCode.TryGetValue(profile.CacheIdentity + "|" + code, out byCode))
                        return byCode;
                }
            }

            if (providerType is DbType)
                return MapDbType((DbType)providerType);
            if (profile.Kind == ProviderKind.Access)
            {
                Type? oleDbType = MapOleDbType(providerType);
                if (oleDbType != null)
                    return oleDbType;
            }
            return MapTypeName(providerTypeName ?? Convert.ToString(providerType, CultureInfo.InvariantCulture));
        }

        private static Type? MapOleDbType(object? providerType)
        {
            if (providerType == null)
                return null;

            int code;
            try { code = Convert.ToInt32(providerType, CultureInfo.InvariantCulture); }
            catch { return null; }

            switch (code)
            {
                case 2: return typeof(short);
                case 3: return typeof(int);
                case 4: return typeof(float);
                case 5: return typeof(double);
                case 6:
                case 14:
                case 131:
                case 139: return typeof(decimal);
                case 7:
                case 64:
                case 133:
                case 134:
                case 135: return typeof(DateTime);
                case 11: return typeof(bool);
                case 16: return typeof(sbyte);
                case 17: return typeof(byte);
                case 18: return typeof(ushort);
                case 19: return typeof(uint);
                case 20: return typeof(long);
                case 21: return typeof(ulong);
                case 72: return typeof(Guid);
                case 128:
                case 204:
                case 205: return typeof(byte[]);
                case 8:
                case 129:
                case 130:
                case 200:
                case 201:
                case 202:
                case 203: return typeof(string);
                default: return null;
            }
        }

        private static Type? MapDbType(DbType dbType)
        {
            switch (dbType)
            {
                case DbType.Int16: return typeof(short);
                case DbType.Int32: return typeof(int);
                case DbType.Int64: return typeof(long);
                case DbType.Decimal:
                case DbType.Currency:
                case DbType.VarNumeric: return typeof(decimal);
                case DbType.Double: return typeof(double);
                case DbType.Single: return typeof(float);
                case DbType.Boolean: return typeof(bool);
                case DbType.Guid: return typeof(Guid);
                case DbType.Date:
                case DbType.DateTime:
                case DbType.DateTime2: return typeof(DateTime);
                case DbType.DateTimeOffset: return typeof(DateTimeOffset);
                case DbType.Binary: return typeof(byte[]);
                case DbType.AnsiString:
                case DbType.AnsiStringFixedLength:
                case DbType.String:
                case DbType.StringFixedLength:
                case DbType.Xml: return typeof(string);
                default: return null;
            }
        }

        private static Type? MapTypeName(string? providerTypeName)
        {
            if (string.IsNullOrWhiteSpace(providerTypeName))
                return null;
            string name = providerTypeName!.Trim().ToUpperInvariant();
            if (name.Contains("DATETIMEOFFSET")) return typeof(DateTimeOffset);
            if (name.Contains("BIGINT") || name.Contains("INT64")) return typeof(long);
            if (name.Contains("SMALLINT") || name.Contains("INT16")) return typeof(short);
            if (name == "INT" || name.Contains("INTEGER") || name.Contains("INT32") || name.Contains("COUNTER")) return typeof(int);
            if (name.Contains("DECIMAL") || name.Contains("NUMERIC") || name.Contains("CURRENCY")) return typeof(decimal);
            if (name.Contains("DOUBLE") || name.Contains("FLOAT")) return typeof(double);
            if (name.Contains("REAL") || name.Contains("SINGLE")) return typeof(float);
            if (name.Contains("BOOL") || name == "BIT" || name.Contains("YESNO")) return typeof(bool);
            if (name.Contains("GUID") || name.Contains("UNIQUEIDENTIFIER")) return typeof(Guid);
            if (name.Contains("DATE") || name.Contains("TIME")) return typeof(DateTime);
            if (name.Contains("BINARY") || name.Contains("IMAGE") || name.Contains("OLEOBJECT")) return typeof(byte[]);
            if (name.Contains("CHAR") || name.Contains("TEXT") || name.Contains("MEMO") || name.Contains("STRING")) return typeof(string);
            return null;
        }

        private static Type? MapSqliteTypeName(string? providerTypeName)
        {
            if (string.IsNullOrWhiteSpace(providerTypeName))
                return null;

            string name = providerTypeName!.Trim().ToUpperInvariant();
            if (name.Contains("DATETIMEOFFSET")) return typeof(DateTimeOffset);
            if (name.Contains("DATE") || name.Contains("TIME")) return typeof(DateTime);
            if (name.Contains("BOOL")) return typeof(bool);
            if (name.Contains("GUID") || name.Contains("UUID")) return typeof(Guid);
            if (name.Contains("DECIMAL") || name.Contains("NUMERIC")) return typeof(decimal);
            if (name.Contains("INT")) return typeof(long);
            if (name.Contains("CHAR") || name.Contains("CLOB") || name.Contains("TEXT")) return typeof(string);
            if (name.Contains("BLOB") || name.Length == 0) return typeof(byte[]);
            if (name.Contains("REAL") || name.Contains("FLOA") || name.Contains("DOUB")) return typeof(double);
            return null;
        }

        private static object? ReadValue(DataRow row, params string[] names)
        {
            for (int index = 0; index < names.Length; index++)
            {
                if (row.Table.Columns.Contains(names[index]))
                {
                    object value = row[names[index]];
                    return value is DBNull ? null : value;
                }
            }
            return null;
        }

        private static string? ReadString(DataRow row, params string[] names)
        {
            object? value = ReadValue(row, names);
            return value == null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private SchemaColumnMetadata? GetCachedSchema(string provider, string table, string column)
        {
            lock (_schemaCacheSync)
            {
                SchemaColumnMetadata? metadata;
                _schemaColumns.TryGetValue(SchemaKey(provider, table, column), out metadata);
                return metadata;
            }
        }

        private void SetCachedSchema(string provider, SchemaColumnMetadata metadata)
        {
            lock (_schemaCacheSync)
                _schemaColumns[SchemaKey(provider, metadata.Table, metadata.Column)] = metadata;
        }

        private void EnsureSchemaLoaded(
            ProviderProfile profile,
            string table,
            IReadOnlyList<string> columns)
        {
            for (int index = 0; index < columns.Count; index++)
            {
                if (GetCachedSchema(profile.CacheIdentity, table, columns[index]) == null)
                {
                    throw new TypeAdaptationException(
                        typeof(object),
                        null,
                        profile.Identity,
                        null,
                        null,
                        TypeAdaptationReasonCode.SchemaUnavailable);
                }
            }
        }

        private void ClearSchemaEntries(
            string provider,
            string table,
            IReadOnlyList<string> columns)
        {
            lock (_schemaCacheSync)
            {
                List<string> remove = new List<string>();
                foreach (string key in _schemaColumns.Keys)
                {
                    string prefix = provider + "|" + table + "|";
                    if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (ContainsIgnoreCase(columns, key.Substring(prefix.Length)))
                        remove.Add(key);
                }
                for (int index = 0; index < remove.Count; index++)
                    _schemaColumns.Remove(remove[index]);
            }
        }

        private static string SchemaKey(string provider, string table, string column)
        {
            return provider + "|" + table + "|" + column;
        }

        private static List<string> NormalizeRequiredColumns(IEnumerable<string> columns)
        {
            if (columns == null) throw new ArgumentNullException(nameof(columns));
            List<string> normalized = new List<string>();
            foreach (string column in columns)
            {
                string value = NormalizeRequiredIdentifier(column, nameof(columns));
                if (!ContainsIgnoreCase(normalized, value))
                    normalized.Add(value);
            }
            if (normalized.Count == 0)
                throw new ArgumentException("At least one schema column is required.", nameof(columns));
            return normalized;
        }

        private static string NormalizeRequiredIdentifier(string value, string parameterName)
        {
            string? normalized = TryNormalizeIdentifier(value);
            if (normalized == null)
                throw new ArgumentException("Schema discovery requires a simple identifier.", parameterName);
            return normalized;
        }

        private static string NormalizeRequiredTable(string value, string parameterName)
        {
            string? normalized = TryNormalizeTable(value);
            if (normalized == null)
                throw new ArgumentException("Schema discovery requires a simple table identifier.", parameterName);
            return normalized;
        }

        private static string? TryNormalizeTable(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            string candidate = value!.Trim();
            if (candidate.IndexOfAny(new[] { ' ', '\t', '\r', '\n', '(', ')', ';', '/', '*' }) >= 0)
                return null;

            string[] parts = candidate.Split('.');
            if (parts.Length < 1 || parts.Length > 3)
                return null;

            string[] normalized = new string[parts.Length];
            for (int index = 0; index < parts.Length; index++)
            {
                string? part = TryNormalizeIdentifierPart(parts[index]);
                if (part == null)
                    return null;
                normalized[index] = part;
            }
            return string.Join(".", normalized);
        }

        private static string? TryNormalizeIdentifier(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            string candidate = value!.Trim();
            if (candidate.IndexOfAny(new[] { ' ', '\t', '\r', '\n', '(', ')', ';', '/', '*' }) >= 0)
                return null;
            string[] parts = candidate.Split('.');
            return TryNormalizeIdentifierPart(parts[parts.Length - 1]);
        }

        private static string? TryNormalizeIdentifierPart(string value)
        {
            string part = value.Trim();
            if (part.Length >= 2 &&
                ((part[0] == '[' && part[part.Length - 1] == ']') ||
                 (part[0] == '"' && part[part.Length - 1] == '"') ||
                 (part[0] == '`' && part[part.Length - 1] == '`')))
            {
                part = part.Substring(1, part.Length - 2);
            }
            if (string.IsNullOrWhiteSpace(part) ||
                part.IndexOfAny(new[] { '[', ']', '"', '`' }) >= 0)
                return null;
            return part;
        }

        private static void SplitNormalizedTable(
            string table,
            out string? schema,
            out string tableName)
        {
            string[] parts = table.Split('.');
            tableName = parts[parts.Length - 1];
            schema = parts.Length >= 2 ? parts[parts.Length - 2] : null;
        }

        private static bool ContainsIgnoreCase(IReadOnlyList<string> values, string value)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (string.Equals(values[index], value, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static IReadOnlyList<TypeAdaptationAttempt> Attempts(
            string strategyId,
            Type targetType,
            TypeAdaptationLoss loss,
            TypeAdaptationReasonCode reasonCode,
            string? format,
            string? cultureName)
        {
            return new ReadOnlyCollection<TypeAdaptationAttempt>(new[]
            {
                new TypeAdaptationAttempt(
                    strategyId,
                    targetType,
                    loss,
                    reasonCode,
                    format,
                    cultureName)
            });
        }

        private static TypeAdaptationAttempt Attempt(
            TypeDecayRule rule,
            TypeAdaptationReasonCode reasonCode)
        {
            return new TypeAdaptationAttempt(
                rule.StrategyId,
                rule.TargetType,
                rule.Loss,
                reasonCode,
                rule.Format,
                rule.CultureName);
        }

        private static TypeAdaptationException Failure(
            Type sourceType,
            Type? targetType,
            string providerIdentity,
            TypePromotionRule? rule,
            TypeAdaptationReasonCode reasonCode)
        {
            return new TypeAdaptationException(
                sourceType,
                targetType,
                providerIdentity,
                rule?.StrategyId,
                rule?.Loss,
                reasonCode);
        }

        private static TypeAdaptationException Failure(
            Type sourceType,
            Type? targetType,
            string providerIdentity,
            TypeDecayRule? rule,
            TypeAdaptationReasonCode reasonCode)
        {
            return new TypeAdaptationException(
                sourceType,
                targetType,
                providerIdentity,
                rule?.StrategyId,
                rule?.Loss,
                reasonCode);
        }

        private void NotifyTypeAdaptation(TypeAdaptationEventArgs args)
        {
            Action<TypeAdaptationEventArgs>? handlers = OnTypeAdaptation;
            if (handlers == null)
                return;

            Delegate[] subscribers = handlers.GetInvocationList();
            for (int index = 0; index < subscribers.Length; index++)
            {
                try
                {
                    ((Action<TypeAdaptationEventArgs>)subscribers[index])(args);
                }
                catch
                {
                }
            }
        }

        private sealed class SchemaColumnMetadata
        {
            public SchemaColumnMetadata(
                string table,
                string column,
                string providerTypeName,
                Type? semanticType)
            {
                Table = table;
                Column = column;
                ProviderTypeName = providerTypeName;
                SemanticType = semanticType;
            }

            public string Table { get; }
            public string Column { get; }
            public string ProviderTypeName { get; }
            public Type? SemanticType { get; }
        }

        private sealed class ProviderProfile
        {
            private ProviderProfile(string identity, string cacheIdentity, ProviderKind kind)
            {
                Identity = identity;
                CacheIdentity = cacheIdentity;
                Kind = kind;
            }

            public string Identity { get; }
            public string CacheIdentity { get; }
            public ProviderKind Kind { get; }
            public bool IsKnown => Kind != ProviderKind.Unknown;

            public bool Supports(Type type)
            {
                Type actual = Nullable.GetUnderlyingType(type) ?? type;
                if (Kind == ProviderKind.Access && actual == typeof(DateTimeOffset))
                    return false;
                return true;
            }

            public static ProviderProfile Resolve(
                DbConnection? connection,
                IDbQueryTranslator translator)
            {
                string connectionType = connection?.GetType().FullName ?? "unknown-connection";
                string translatorType = translator.GetType().FullName ?? translator.GetType().Name;
                string combined = connectionType + "|" + translatorType;
                string dataSourceIdentity = GetCacheLocation(connection);
                if (translator is AccessQueryTranslator || combined.IndexOf("OleDb", StringComparison.OrdinalIgnoreCase) >= 0)
                    return new ProviderProfile(connectionType, "access|" + connectionType + dataSourceIdentity, ProviderKind.Access);
                if (translator is SqlServerQueryTranslator || combined.IndexOf("SqlConnection", StringComparison.OrdinalIgnoreCase) >= 0)
                    return new ProviderProfile(connectionType, "sqlserver|" + connectionType + dataSourceIdentity, ProviderKind.SqlServer);
                if (translator is SqliteQueryTranslator || combined.IndexOf("Sqlite", StringComparison.OrdinalIgnoreCase) >= 0 || combined.IndexOf("SQLite", StringComparison.OrdinalIgnoreCase) >= 0)
                    return new ProviderProfile(connectionType, "sqlite|" + connectionType + dataSourceIdentity, ProviderKind.Sqlite);
                return new ProviderProfile(connectionType, "unknown|" + connectionType + dataSourceIdentity, ProviderKind.Unknown);
            }

            private static string GetCacheLocation(DbConnection? connection)
            {
                if (connection == null)
                    return "|0:|0:";

                string database;
                string dataSource;
                try { database = connection.Database ?? string.Empty; }
                catch { database = string.Empty; }
                try { dataSource = connection.DataSource ?? string.Empty; }
                catch { dataSource = string.Empty; }

                return "|" + database.Length.ToString(CultureInfo.InvariantCulture) + ":" + database +
                       "|" + dataSource.Length.ToString(CultureInfo.InvariantCulture) + ":" + dataSource;
            }
        }

        private enum ProviderKind
        {
            Unknown = 0,
            Access = 1,
            SqlServer = 2,
            Sqlite = 3
        }
    }
}
