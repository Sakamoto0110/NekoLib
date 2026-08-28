#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace NekoLib.Data
{
    /// <summary>Controls input promotion before database dispatch.</summary>
    public enum TypePromotionPolicy
    {
        /// <summary>Never convert a consumer value before provider binding.</summary>
        Disabled = 0,

        /// <summary>Apply only a rule attached to the logical parameter.</summary>
        ExplicitOnly = 1,

        /// <summary>Also apply registered lossless rules after schema validation.</summary>
        SchemaValidated = 2
    }

    /// <summary>Controls fallback from a preferred provider representation.</summary>
    public enum TypeDecayPolicy
    {
        /// <summary>Reject a value when the provider cannot represent its preferred type.</summary>
        Strict = 0,

        /// <summary>Allow registered provider-representation fallback rules.</summary>
        AllowFallback = 1
    }

    /// <summary>Controls whether an explicit rule may lose semantic information.</summary>
    public enum TypeLossPolicy
    {
        /// <summary>Reject every rule classified as potentially lossy.</summary>
        RejectPotentialLoss = 0,

        /// <summary>Allow and report a potentially lossy rule when it is explicitly scoped.</summary>
        AllowExplicitAndReport = 1
    }

    /// <summary>Controls automatic database-schema discovery.</summary>
    public enum SchemaDiscoveryMode
    {
        /// <summary>Do not discover schema automatically.</summary>
        Disabled = 0,

        /// <summary>Discover and cache schema when a structured parameter first needs it.</summary>
        Lazy = 1,

        /// <summary>Require schema to be loaded explicitly before schema-dependent execution.</summary>
        Preload = 2
    }

    /// <summary>Classifies whether an adaptation can discard semantic information.</summary>
    public enum TypeAdaptationLoss
    {
        /// <summary>The rule preserves the represented semantic information.</summary>
        Lossless = 0,

        /// <summary>The rule can discard semantic information and requires explicit authorization.</summary>
        PotentiallyLossy = 1
    }

    /// <summary>Identifies whether an adaptation occurs during binding or materialization.</summary>
    public enum TypeAdaptationDirection
    {
        /// <summary>Converts a provider value while materializing a DTO.</summary>
        Read = 0,

        /// <summary>Converts a consumer value before provider dispatch.</summary>
        Write = 1
    }

    /// <summary>Identifies the adaptation stage that produced an outcome.</summary>
    public enum TypeAdaptationKind
    {
        /// <summary>Converts a logical input toward a schema-declared semantic type.</summary>
        Promotion = 0,

        /// <summary>Converts a semantic value to a provider-supported representation.</summary>
        Decay = 1,

        /// <summary>Converts a provider value to a DTO property type.</summary>
        Materialization = 2
    }

    /// <summary>Stable, value-free reason codes for adaptation outcomes.</summary>
    public enum TypeAdaptationReasonCode
    {
        /// <summary>An explicitly scoped rule was selected.</summary>
        ExplicitRule = 0,
        /// <summary>A registered rule was selected after schema validation.</summary>
        SchemaValidatedRule = 1,
        /// <summary>A registered provider fallback was selected.</summary>
        ProviderFallback = 2,
        /// <summary>Promotion was disabled by policy.</summary>
        PromotionDisabled = 3,
        /// <summary>No eligible promotion rule was available.</summary>
        PromotionRuleMissing = 4,
        /// <summary>The runtime value did not match the rule source type.</summary>
        SourceTypeMismatch = 5,
        /// <summary>The converter rejected the supplied value.</summary>
        ConversionRejected = 6,
        /// <summary>The conversion exceeded the target numeric or temporal range.</summary>
        Overflow = 7,
        /// <summary>Schema metadata could not be obtained.</summary>
        SchemaUnavailable = 8,
        /// <summary>Preload mode was active but the required schema was not cached.</summary>
        SchemaNotPreloaded = 9,
        /// <summary>Schema discovery was requested after a transaction had begun.</summary>
        SchemaRequiredBeforeTransaction = 10,
        /// <summary>The provider profile does not support the preferred representation.</summary>
        ProviderRepresentationUnsupported = 11,
        /// <summary>A potentially lossy rule lacked the required explicit authorization.</summary>
        LossyAdaptationNotAuthorized = 12,
        /// <summary>Strict decay policy prohibited a representation fallback.</summary>
        StrictDecayRejected = 13,
        /// <summary>The provider could not be matched to a known capability profile.</summary>
        UnknownProvider = 14,
        /// <summary>A built-in automatic rule was selected.</summary>
        BuiltInRule = 15,
        /// <summary>No eligible rule could materialize the provider value.</summary>
        MaterializationRuleMissing = 16
    }

    /// <summary>Converts one semantic value without receiving SQL or credentials.</summary>
    /// <param name="value">The non-null value to convert.</param>
    /// <returns>The converted value, or null when the declared rule permits it.</returns>
    public delegate object? TypeValueConverter(object value);

    /// <summary>Describes one exact, locally executable input-promotion rule.</summary>
    public sealed class TypePromotionRule
    {
        private readonly TypeValueConverter _converter;

        /// <summary>Creates an exact input-promotion rule.</summary>
        /// <param name="strategyId">A stable, value-free identifier of at most 128 characters.</param>
        /// <param name="sourceType">The exact runtime source type.</param>
        /// <param name="targetType">The exact semantic target type.</param>
        /// <param name="converter">A deterministic converter that receives only a non-null value.</param>
        /// <param name="loss">Whether the conversion can discard semantic information.</param>
        /// <param name="format">Optional sanitized format metadata for reporting.</param>
        /// <param name="cultureName">Optional sanitized culture metadata for reporting.</param>
        public TypePromotionRule(
            string strategyId,
            Type sourceType,
            Type targetType,
            TypeValueConverter converter,
            TypeAdaptationLoss loss = TypeAdaptationLoss.Lossless,
            string? format = null,
            string? cultureName = null)
        {
            StrategyId = RequireStrategyId(strategyId);
            SourceType = sourceType ?? throw new ArgumentNullException(nameof(sourceType));
            TargetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
            _converter = converter ?? throw new ArgumentNullException(nameof(converter));
            if (!Enum.IsDefined(typeof(TypeAdaptationLoss), loss))
                throw new ArgumentOutOfRangeException(nameof(loss));

            Loss = loss;
            Format = format;
            CultureName = cultureName;
        }

        /// <summary>Gets the stable, value-free strategy identifier.</summary>
        public string StrategyId { get; }
        /// <summary>Gets the exact runtime source type.</summary>
        public Type SourceType { get; }
        /// <summary>Gets the exact semantic target type.</summary>
        public Type TargetType { get; }
        /// <summary>Gets the declared loss classification.</summary>
        public TypeAdaptationLoss Loss { get; }
        /// <summary>Gets the optional sanitized format metadata.</summary>
        public string? Format { get; }
        /// <summary>Gets the optional sanitized culture name.</summary>
        public string? CultureName { get; }

        internal object? Convert(object value)
        {
            return _converter(value);
        }

        private static string RequireStrategyId(string strategyId)
        {
            if (strategyId == null) throw new ArgumentNullException(nameof(strategyId));
            if (string.IsNullOrWhiteSpace(strategyId))
                throw new ArgumentException("A strategy identifier is required.", nameof(strategyId));
            if (strategyId.Length > 128)
                throw new ArgumentException("A strategy identifier cannot exceed 128 characters.", nameof(strategyId));
            return strategyId;
        }
    }

    /// <summary>Describes one provider-representation fallback.</summary>
    public sealed class TypeDecayRule
    {
        private readonly TypeValueConverter _converter;

        /// <summary>Creates an exact provider-representation fallback rule.</summary>
        /// <param name="strategyId">A stable, value-free identifier of at most 128 characters.</param>
        /// <param name="sourceType">The exact semantic source type.</param>
        /// <param name="targetType">The exact provider-representation target type.</param>
        /// <param name="converter">A deterministic converter that receives only a non-null value.</param>
        /// <param name="loss">Whether the conversion can discard semantic information.</param>
        /// <param name="format">Optional sanitized format metadata for reporting.</param>
        /// <param name="cultureName">Optional sanitized culture metadata for reporting.</param>
        public TypeDecayRule(
            string strategyId,
            Type sourceType,
            Type targetType,
            TypeValueConverter converter,
            TypeAdaptationLoss loss,
            string? format = null,
            string? cultureName = null)
        {
            if (strategyId == null) throw new ArgumentNullException(nameof(strategyId));
            if (string.IsNullOrWhiteSpace(strategyId))
                throw new ArgumentException("A strategy identifier is required.", nameof(strategyId));
            if (strategyId.Length > 128)
                throw new ArgumentException("A strategy identifier cannot exceed 128 characters.", nameof(strategyId));
            SourceType = sourceType ?? throw new ArgumentNullException(nameof(sourceType));
            TargetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
            _converter = converter ?? throw new ArgumentNullException(nameof(converter));
            if (!Enum.IsDefined(typeof(TypeAdaptationLoss), loss))
                throw new ArgumentOutOfRangeException(nameof(loss));

            StrategyId = strategyId;
            Loss = loss;
            Format = format;
            CultureName = cultureName;
        }

        /// <summary>Gets the stable, value-free strategy identifier.</summary>
        public string StrategyId { get; }
        /// <summary>Gets the exact semantic source type.</summary>
        public Type SourceType { get; }
        /// <summary>Gets the exact provider-representation target type.</summary>
        public Type TargetType { get; }
        /// <summary>Gets the declared loss classification.</summary>
        public TypeAdaptationLoss Loss { get; }
        /// <summary>Gets the optional sanitized format metadata.</summary>
        public string? Format { get; }
        /// <summary>Gets the optional sanitized culture name.</summary>
        public string? CultureName { get; }

        internal object? Convert(object value)
        {
            return _converter(value);
        }
    }

    /// <summary>Describes one exact provider-value to DTO-value conversion.</summary>
    public sealed class TypeMaterializationRule
    {
        private readonly TypeValueConverter _converter;

        /// <summary>Creates an exact provider-value to DTO-value conversion rule.</summary>
        /// <param name="strategyId">A stable, value-free identifier of at most 128 characters.</param>
        /// <param name="sourceType">The exact provider value type.</param>
        /// <param name="targetType">The exact DTO property type.</param>
        /// <param name="converter">A deterministic converter that receives only a non-null value.</param>
        /// <param name="loss">Whether the conversion can discard semantic information.</param>
        /// <param name="format">Optional sanitized format metadata for reporting.</param>
        /// <param name="cultureName">Optional sanitized culture metadata for reporting.</param>
        public TypeMaterializationRule(
            string strategyId,
            Type sourceType,
            Type targetType,
            TypeValueConverter converter,
            TypeAdaptationLoss loss,
            string? format = null,
            string? cultureName = null)
        {
            if (strategyId == null) throw new ArgumentNullException(nameof(strategyId));
            if (string.IsNullOrWhiteSpace(strategyId))
                throw new ArgumentException("A strategy identifier is required.", nameof(strategyId));
            if (strategyId.Length > 128)
                throw new ArgumentException("A strategy identifier cannot exceed 128 characters.", nameof(strategyId));
            SourceType = sourceType ?? throw new ArgumentNullException(nameof(sourceType));
            TargetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
            _converter = converter ?? throw new ArgumentNullException(nameof(converter));
            if (!Enum.IsDefined(typeof(TypeAdaptationLoss), loss))
                throw new ArgumentOutOfRangeException(nameof(loss));

            StrategyId = strategyId;
            Loss = loss;
            Format = format;
            CultureName = cultureName;
        }

        /// <summary>Gets the stable, value-free strategy identifier.</summary>
        public string StrategyId { get; }
        /// <summary>Gets the exact provider value type.</summary>
        public Type SourceType { get; }
        /// <summary>Gets the exact DTO property type.</summary>
        public Type TargetType { get; }
        /// <summary>Gets the declared loss classification.</summary>
        public TypeAdaptationLoss Loss { get; }
        /// <summary>Gets the optional sanitized format metadata.</summary>
        public string? Format { get; }
        /// <summary>Gets the optional sanitized culture name.</summary>
        public string? CultureName { get; }

        internal object? Convert(object value)
        {
            return _converter(value);
        }
    }

    /// <summary>Binds an explicit read adaptation to one DTO property.</summary>
    public sealed class ReadTypeAdaptationRule
    {
        /// <summary>Creates an explicit DTO-property-scoped read adaptation.</summary>
        /// <param name="dtoType">The DTO type that owns the property.</param>
        /// <param name="propertyName">The public writable non-indexed property name.</param>
        /// <param name="adaptation">The exact provider-value conversion.</param>
        /// <param name="columnName">An optional column-name constraint; null applies to any bound column.</param>
        public ReadTypeAdaptationRule(
            Type dtoType,
            string propertyName,
            TypeMaterializationRule adaptation,
            string? columnName = null)
        {
            DtoType = dtoType ?? throw new ArgumentNullException(nameof(dtoType));
            if (propertyName == null) throw new ArgumentNullException(nameof(propertyName));
            if (string.IsNullOrWhiteSpace(propertyName))
                throw new ArgumentException("A DTO property name is required.", nameof(propertyName));
            if (columnName != null && string.IsNullOrWhiteSpace(columnName))
                throw new ArgumentException("A column name cannot be empty.", nameof(columnName));

            PropertyName = propertyName;
            ColumnName = columnName;
            Adaptation = adaptation ?? throw new ArgumentNullException(nameof(adaptation));
        }

        /// <summary>Gets the DTO type that owns the destination property.</summary>
        public Type DtoType { get; }
        /// <summary>Gets the destination property name.</summary>
        public string PropertyName { get; }
        /// <summary>Gets the optional exact column-name constraint.</summary>
        public string? ColumnName { get; }
        /// <summary>Gets the materialization rule applied to the binding.</summary>
        public TypeMaterializationRule Adaptation { get; }

        /// <summary>Creates an explicit read adaptation for a property on <typeparamref name="TDto"/>.</summary>
        /// <typeparam name="TDto">The DTO type that owns the property.</typeparam>
        /// <param name="propertyName">The public writable non-indexed property name.</param>
        /// <param name="adaptation">The exact provider-value conversion.</param>
        /// <param name="columnName">An optional column-name constraint.</param>
        /// <returns>The property-scoped rule.</returns>
        public static ReadTypeAdaptationRule For<TDto>(
            string propertyName,
            TypeMaterializationRule adaptation,
            string? columnName = null)
        {
            return new ReadTypeAdaptationRule(
                typeof(TDto),
                propertyName,
                adaptation,
                columnName);
        }
    }

    /// <summary>Provider-neutral built-in promotion rules.</summary>
    public static class TypePromotions
    {
        /// <summary>Gets the invariant string-to-<see cref="short"/> promotion.</summary>
        public static TypePromotionRule StringToInt16 { get; } = new TypePromotionRule(
            "string-to-int16-invariant",
            typeof(string),
            typeof(short),
            value => short.Parse((string)value, NumberStyles.Integer, CultureInfo.InvariantCulture));

        /// <summary>Gets the invariant string-to-<see cref="int"/> promotion.</summary>
        public static TypePromotionRule StringToInt32 { get; } = new TypePromotionRule(
            "string-to-int32-invariant",
            typeof(string),
            typeof(int),
            value => int.Parse((string)value, NumberStyles.Integer, CultureInfo.InvariantCulture));

        /// <summary>Gets the invariant string-to-<see cref="long"/> promotion.</summary>
        public static TypePromotionRule StringToInt64 { get; } = new TypePromotionRule(
            "string-to-int64-invariant",
            typeof(string),
            typeof(long),
            value => long.Parse((string)value, NumberStyles.Integer, CultureInfo.InvariantCulture));

        /// <summary>Gets the invariant string-to-<see cref="decimal"/> promotion.</summary>
        public static TypePromotionRule StringToDecimal { get; } = new TypePromotionRule(
            "string-to-decimal-invariant",
            typeof(string),
            typeof(decimal),
            value => decimal.Parse((string)value, NumberStyles.Number, CultureInfo.InvariantCulture));

        /// <summary>Gets the invariant string-to-<see cref="double"/> promotion.</summary>
        public static TypePromotionRule StringToDouble { get; } = new TypePromotionRule(
            "string-to-double-invariant",
            typeof(string),
            typeof(double),
            value => double.Parse(
                (string)value,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture));

        /// <summary>Gets the string-to-<see cref="bool"/> promotion.</summary>
        public static TypePromotionRule StringToBoolean { get; } = new TypePromotionRule(
            "string-to-boolean-invariant",
            typeof(string),
            typeof(bool),
            value => bool.Parse((string)value));

        /// <summary>Gets the string-to-<see cref="Guid"/> promotion.</summary>
        public static TypePromotionRule StringToGuid { get; } = new TypePromotionRule(
            "string-to-guid",
            typeof(string),
            typeof(Guid),
            value => Guid.Parse((string)value));

        /// <summary>Gets the invariant round-trip string-to-<see cref="DateTime"/> promotion.</summary>
        public static TypePromotionRule StringToDateTimeIso8601 { get; } =
            CreateStringToDateTime("O", CultureInfo.InvariantCulture);

        /// <summary>Gets the invariant round-trip string-to-<see cref="DateTimeOffset"/> promotion.</summary>
        public static TypePromotionRule StringToDateTimeOffsetIso8601 { get; } =
            CreateStringToDateTimeOffset("O", CultureInfo.InvariantCulture);

        /// <summary>Creates an exact-format string-to-<see cref="DateTime"/> promotion.</summary>
        /// <param name="format">The required exact format.</param>
        /// <param name="culture">The culture used by parsing.</param>
        /// <returns>A lossless rule carrying the format and culture as sanitized metadata.</returns>
        public static TypePromotionRule CreateStringToDateTime(
            string format,
            CultureInfo culture)
        {
            RequireFormatAndCulture(format, culture);
            string cultureName = culture.Name;
            return new TypePromotionRule(
                "string-to-datetime:" + cultureName + ":" + format,
                typeof(string),
                typeof(DateTime),
                value => DateTime.ParseExact(
                    (string)value,
                    format,
                    culture,
                    DateTimeStyles.RoundtripKind),
                TypeAdaptationLoss.Lossless,
                format,
                cultureName);
        }

        /// <summary>Creates an exact-format string-to-<see cref="DateTimeOffset"/> promotion.</summary>
        /// <param name="format">The required exact format.</param>
        /// <param name="culture">The culture used by parsing.</param>
        /// <returns>A lossless rule carrying the format and culture as sanitized metadata.</returns>
        public static TypePromotionRule CreateStringToDateTimeOffset(
            string format,
            CultureInfo culture)
        {
            RequireFormatAndCulture(format, culture);
            string cultureName = culture.Name;
            return new TypePromotionRule(
                "string-to-datetimeoffset:" + cultureName + ":" + format,
                typeof(string),
                typeof(DateTimeOffset),
                value => DateTimeOffset.ParseExact(
                    (string)value,
                    format,
                    culture,
                    DateTimeStyles.None),
                TypeAdaptationLoss.Lossless,
                format,
                cultureName);
        }

        internal static IReadOnlyList<TypePromotionRule> BuiltInRules { get; } =
            new ReadOnlyCollection<TypePromotionRule>(new[]
            {
                StringToInt16,
                StringToInt32,
                StringToInt64,
                StringToDecimal,
                StringToDouble,
                StringToBoolean,
                StringToGuid,
                StringToDateTimeIso8601,
                StringToDateTimeOffsetIso8601
            });

        private static void RequireFormatAndCulture(string format, CultureInfo culture)
        {
            if (format == null) throw new ArgumentNullException(nameof(format));
            if (string.IsNullOrWhiteSpace(format))
                throw new ArgumentException("A non-empty format is required.", nameof(format));
            if (culture == null) throw new ArgumentNullException(nameof(culture));
        }
    }

    /// <summary>Provider-neutral built-in representation fallbacks.</summary>
    public static class TypeDecays
    {
        /// <summary>Gets the lossless round-trip <see cref="DateTime"/>-to-string fallback.</summary>
        public static TypeDecayRule DateTimeToRoundTripString { get; } = new TypeDecayRule(
            "datetime-to-roundtrip-string",
            typeof(DateTime),
            typeof(string),
            value => ((DateTime)value).ToString("O", CultureInfo.InvariantCulture),
            TypeAdaptationLoss.Lossless,
            "O",
            CultureInfo.InvariantCulture.Name);

        /// <summary>Gets the lossless round-trip <see cref="DateTimeOffset"/>-to-string fallback.</summary>
        public static TypeDecayRule DateTimeOffsetToRoundTripString { get; } = new TypeDecayRule(
            "datetimeoffset-to-roundtrip-string",
            typeof(DateTimeOffset),
            typeof(string),
            value => ((DateTimeOffset)value).ToString("O", CultureInfo.InvariantCulture),
            TypeAdaptationLoss.Lossless,
            "O",
            CultureInfo.InvariantCulture.Name);

        /// <summary>
        /// Gets the potentially lossy fallback that normalizes a
        /// <see cref="DateTimeOffset"/> to a UTC <see cref="DateTime"/>.
        /// </summary>
        public static TypeDecayRule DateTimeOffsetToUtcDateTime { get; } = new TypeDecayRule(
            "datetimeoffset-to-utc-datetime",
            typeof(DateTimeOffset),
            typeof(DateTime),
            value => ((DateTimeOffset)value).UtcDateTime,
            TypeAdaptationLoss.PotentiallyLossy);

        /// <summary>Gets the lossless <see cref="Guid"/>-to-D-format-string fallback.</summary>
        public static TypeDecayRule GuidToString { get; } = new TypeDecayRule(
            "guid-to-string-d",
            typeof(Guid),
            typeof(string),
            value => ((Guid)value).ToString("D", CultureInfo.InvariantCulture),
            TypeAdaptationLoss.Lossless,
            "D",
            CultureInfo.InvariantCulture.Name);

        /// <summary>
        /// Creates a formatter-backed DateTime-to-string fallback. Custom
        /// formats are potentially lossy unless the caller explicitly proves
        /// and declares otherwise.
        /// </summary>
        public static TypeDecayRule CreateDateTimeToString(
            string format,
            CultureInfo culture,
            TypeAdaptationLoss loss = TypeAdaptationLoss.PotentiallyLossy)
        {
            RequireFormatter(format, culture, loss);
            string cultureName = culture.Name;
            return new TypeDecayRule(
                "datetime-to-string:" + cultureName + ":" + format,
                typeof(DateTime),
                typeof(string),
                value => ((DateTime)value).ToString(format, culture),
                loss,
                format,
                cultureName);
        }

        /// <summary>
        /// Creates a formatter-backed DateTimeOffset-to-string fallback.
        /// Custom formats are potentially lossy unless the caller explicitly
        /// proves and declares otherwise.
        /// </summary>
        public static TypeDecayRule CreateDateTimeOffsetToString(
            string format,
            CultureInfo culture,
            TypeAdaptationLoss loss = TypeAdaptationLoss.PotentiallyLossy)
        {
            RequireFormatter(format, culture, loss);
            string cultureName = culture.Name;
            return new TypeDecayRule(
                "datetimeoffset-to-string:" + cultureName + ":" + format,
                typeof(DateTimeOffset),
                typeof(string),
                value => ((DateTimeOffset)value).ToString(format, culture),
                loss,
                format,
                cultureName);
        }

        internal static IReadOnlyList<TypeDecayRule> BuiltInRules { get; } =
            new ReadOnlyCollection<TypeDecayRule>(new[]
            {
                DateTimeToRoundTripString,
                DateTimeOffsetToRoundTripString,
                GuidToString
            });

        private static void RequireFormatter(
            string format,
            CultureInfo culture,
            TypeAdaptationLoss loss)
        {
            if (format == null) throw new ArgumentNullException(nameof(format));
            if (string.IsNullOrWhiteSpace(format))
                throw new ArgumentException("A non-empty format is required.", nameof(format));
            if (culture == null) throw new ArgumentNullException(nameof(culture));
            if (!Enum.IsDefined(typeof(TypeAdaptationLoss), loss))
                throw new ArgumentOutOfRangeException(nameof(loss));
        }
    }

    /// <summary>Provider-neutral temporal rules for DTO materialization.</summary>
    public static class TypeMaterializations
    {
        /// <summary>Gets the lossless round-trip string-to-<see cref="DateTime"/> rule.</summary>
        public static TypeMaterializationRule StringToDateTimeRoundTrip { get; } =
            CreateStringToDateTime(
                "O",
                CultureInfo.InvariantCulture,
                TypeAdaptationLoss.Lossless);

        /// <summary>Gets the lossless round-trip string-to-<see cref="DateTimeOffset"/> rule.</summary>
        public static TypeMaterializationRule StringToDateTimeOffsetRoundTrip { get; } =
            CreateStringToDateTimeOffset(
                "O",
                CultureInfo.InvariantCulture,
                TypeAdaptationLoss.Lossless);

        /// <summary>Gets the lossless round-trip <see cref="DateTime"/>-to-string rule.</summary>
        public static TypeMaterializationRule DateTimeToRoundTripString { get; } =
            CreateDateTimeToString(
                "O",
                CultureInfo.InvariantCulture,
                TypeAdaptationLoss.Lossless);

        /// <summary>Gets the lossless round-trip <see cref="DateTimeOffset"/>-to-string rule.</summary>
        public static TypeMaterializationRule DateTimeOffsetToRoundTripString { get; } =
            CreateDateTimeOffsetToString(
                "O",
                CultureInfo.InvariantCulture,
                TypeAdaptationLoss.Lossless);

        /// <summary>
        /// Gets the lossless UTC <see cref="DateTime"/>-to-<see cref="DateTimeOffset"/>
        /// rule. Non-UTC values are rejected.
        /// </summary>
        public static TypeMaterializationRule DateTimeUtcToDateTimeOffset { get; } =
            new TypeMaterializationRule(
                "read-datetime-utc-to-datetimeoffset",
                typeof(DateTime),
                typeof(DateTimeOffset),
                value =>
                {
                    DateTime dateTime = (DateTime)value;
                    if (dateTime.Kind != DateTimeKind.Utc)
                        throw new InvalidCastException("The DateTime value is not UTC.");
                    return new DateTimeOffset(dateTime);
                },
                TypeAdaptationLoss.Lossless);

        /// <summary>
        /// Uses <see cref="DateTime.Kind"/>; an Unspecified value is interpreted
        /// with the machine-local offset and is therefore potentially lossy.
        /// </summary>
        public static TypeMaterializationRule DateTimeToDateTimeOffsetUsingKind { get; } =
            new TypeMaterializationRule(
                "read-datetime-to-datetimeoffset-using-kind",
                typeof(DateTime),
                typeof(DateTimeOffset),
                value => new DateTimeOffset((DateTime)value),
                TypeAdaptationLoss.PotentiallyLossy);

        /// <summary>
        /// Gets the potentially lossy rule that normalizes a
        /// <see cref="DateTimeOffset"/> to a UTC <see cref="DateTime"/>.
        /// </summary>
        public static TypeMaterializationRule DateTimeOffsetToUtcDateTime { get; } =
            new TypeMaterializationRule(
                "read-datetimeoffset-to-utc-datetime",
                typeof(DateTimeOffset),
                typeof(DateTime),
                value => ((DateTimeOffset)value).UtcDateTime,
                TypeAdaptationLoss.PotentiallyLossy);

        /// <summary>Creates an exact-format string-to-<see cref="DateTime"/> materialization.</summary>
        /// <param name="format">The required exact format.</param>
        /// <param name="culture">The culture used by parsing.</param>
        /// <param name="loss">The caller-declared loss classification.</param>
        /// <returns>The materialization rule.</returns>
        public static TypeMaterializationRule CreateStringToDateTime(
            string format,
            CultureInfo culture,
            TypeAdaptationLoss loss = TypeAdaptationLoss.PotentiallyLossy)
        {
            RequireFormatter(format, culture, loss);
            string cultureName = culture.Name;
            return new TypeMaterializationRule(
                "read-string-to-datetime:" + cultureName + ":" + format,
                typeof(string),
                typeof(DateTime),
                value => DateTime.ParseExact(
                    (string)value,
                    format,
                    culture,
                    DateTimeStyles.RoundtripKind),
                loss,
                format,
                cultureName);
        }

        /// <summary>Creates an exact-format string-to-<see cref="DateTimeOffset"/> materialization.</summary>
        /// <param name="format">The required exact format.</param>
        /// <param name="culture">The culture used by parsing.</param>
        /// <param name="loss">The caller-declared loss classification.</param>
        /// <returns>The materialization rule.</returns>
        public static TypeMaterializationRule CreateStringToDateTimeOffset(
            string format,
            CultureInfo culture,
            TypeAdaptationLoss loss = TypeAdaptationLoss.PotentiallyLossy)
        {
            RequireFormatter(format, culture, loss);
            string cultureName = culture.Name;
            return new TypeMaterializationRule(
                "read-string-to-datetimeoffset:" + cultureName + ":" + format,
                typeof(string),
                typeof(DateTimeOffset),
                value => DateTimeOffset.ParseExact(
                    (string)value,
                    format,
                    culture,
                    DateTimeStyles.None),
                loss,
                format,
                cultureName);
        }

        /// <summary>Creates a formatter-backed <see cref="DateTime"/>-to-string materialization.</summary>
        /// <param name="format">The output format.</param>
        /// <param name="culture">The culture used by formatting.</param>
        /// <param name="loss">The caller-declared loss classification.</param>
        /// <returns>The materialization rule.</returns>
        public static TypeMaterializationRule CreateDateTimeToString(
            string format,
            CultureInfo culture,
            TypeAdaptationLoss loss = TypeAdaptationLoss.PotentiallyLossy)
        {
            RequireFormatter(format, culture, loss);
            string cultureName = culture.Name;
            return new TypeMaterializationRule(
                "read-datetime-to-string:" + cultureName + ":" + format,
                typeof(DateTime),
                typeof(string),
                value => ((DateTime)value).ToString(format, culture),
                loss,
                format,
                cultureName);
        }

        /// <summary>Creates a formatter-backed <see cref="DateTimeOffset"/>-to-string materialization.</summary>
        /// <param name="format">The output format.</param>
        /// <param name="culture">The culture used by formatting.</param>
        /// <param name="loss">The caller-declared loss classification.</param>
        /// <returns>The materialization rule.</returns>
        public static TypeMaterializationRule CreateDateTimeOffsetToString(
            string format,
            CultureInfo culture,
            TypeAdaptationLoss loss = TypeAdaptationLoss.PotentiallyLossy)
        {
            RequireFormatter(format, culture, loss);
            string cultureName = culture.Name;
            return new TypeMaterializationRule(
                "read-datetimeoffset-to-string:" + cultureName + ":" + format,
                typeof(DateTimeOffset),
                typeof(string),
                value => ((DateTimeOffset)value).ToString(format, culture),
                loss,
                format,
                cultureName);
        }

        internal static IReadOnlyList<TypeMaterializationRule> BuiltInRules { get; } =
            new ReadOnlyCollection<TypeMaterializationRule>(new[]
            {
                StringToDateTimeRoundTrip,
                StringToDateTimeOffsetRoundTrip,
                DateTimeToRoundTripString,
                DateTimeOffsetToRoundTripString,
                DateTimeUtcToDateTimeOffset
            });

        private static void RequireFormatter(
            string format,
            CultureInfo culture,
            TypeAdaptationLoss loss)
        {
            if (format == null) throw new ArgumentNullException(nameof(format));
            if (string.IsNullOrWhiteSpace(format))
                throw new ArgumentException("A non-empty format is required.", nameof(format));
            if (culture == null) throw new ArgumentNullException(nameof(culture));
            if (!Enum.IsDefined(typeof(TypeAdaptationLoss), loss))
                throw new ArgumentOutOfRangeException(nameof(loss));
        }
    }

    /// <summary>One value-free attempt in a bounded adaptation report.</summary>
    public sealed class TypeAdaptationAttempt
    {
        internal TypeAdaptationAttempt(
            string strategyId,
            Type targetType,
            TypeAdaptationLoss loss,
            TypeAdaptationReasonCode reasonCode,
            string? format,
            string? cultureName)
        {
            StrategyId = strategyId;
            TargetType = targetType;
            Loss = loss;
            ReasonCode = reasonCode;
            Format = format;
            CultureName = cultureName;
        }

        /// <summary>Gets the value-free identifier of the attempted strategy.</summary>
        public string StrategyId { get; }
        /// <summary>Gets the target type attempted by the strategy.</summary>
        public Type TargetType { get; }
        /// <summary>Gets the strategy's loss classification.</summary>
        public TypeAdaptationLoss Loss { get; }
        /// <summary>Gets the structured outcome reason.</summary>
        public TypeAdaptationReasonCode ReasonCode { get; }
        /// <summary>Gets the optional sanitized format metadata.</summary>
        public string? Format { get; }
        /// <summary>Gets the optional sanitized culture name.</summary>
        public string? CultureName { get; }
    }

    /// <summary>Sanitized evidence emitted after one logical adaptation.</summary>
    public sealed class TypeAdaptationEventArgs : EventArgs
    {
        internal TypeAdaptationEventArgs(
            TypeAdaptationDirection direction,
            TypeAdaptationKind kind,
            Type sourceType,
            Type targetType,
            string providerIdentity,
            string bindingRepresentation,
            string? table,
            string? column,
            string? parameterName,
            string? propertyName,
            string strategyId,
            TypeAdaptationLoss loss,
            TypeAdaptationReasonCode reasonCode,
            string? format,
            string? cultureName,
            IReadOnlyList<TypeAdaptationAttempt> attempts,
            Guid correlationId)
        {
            Direction = direction;
            Kind = kind;
            SourceType = sourceType;
            TargetType = targetType;
            ProviderIdentity = providerIdentity;
            BindingRepresentation = bindingRepresentation;
            Table = table;
            Column = column;
            ParameterName = parameterName;
            PropertyName = propertyName;
            StrategyId = strategyId;
            Loss = loss;
            ReasonCode = reasonCode;
            Format = format;
            CultureName = cultureName;
            Attempts = attempts;
            CorrelationId = correlationId;
        }

        /// <summary>Gets whether the adaptation occurred during read or write.</summary>
        public TypeAdaptationDirection Direction { get; }
        /// <summary>Gets the adaptation stage.</summary>
        public TypeAdaptationKind Kind { get; }
        /// <summary>Gets the runtime source type.</summary>
        public Type SourceType { get; }
        /// <summary>Gets the selected target type.</summary>
        public Type TargetType { get; }
        /// <summary>Gets the sanitized provider identity.</summary>
        public string ProviderIdentity { get; }
        /// <summary>Gets the provider binding representation selected for dispatch.</summary>
        public string BindingRepresentation { get; }
        /// <summary>Gets the optional structured table identity.</summary>
        public string? Table { get; }
        /// <summary>Gets the optional structured column identity.</summary>
        public string? Column { get; }
        /// <summary>Gets the optional logical parameter name.</summary>
        public string? ParameterName { get; }
        /// <summary>Gets the optional DTO property name.</summary>
        public string? PropertyName { get; }
        /// <summary>Gets the value-free identifier of the selected strategy.</summary>
        public string StrategyId { get; }
        /// <summary>Gets the selected strategy's loss classification.</summary>
        public TypeAdaptationLoss Loss { get; }
        /// <summary>Gets the structured selection reason.</summary>
        public TypeAdaptationReasonCode ReasonCode { get; }
        /// <summary>Gets the optional sanitized format metadata.</summary>
        public string? Format { get; }
        /// <summary>Gets the optional sanitized culture name.</summary>
        public string? CultureName { get; }
        /// <summary>Gets the bounded, value-free sequence of attempted strategies.</summary>
        public IReadOnlyList<TypeAdaptationAttempt> Attempts { get; }
        /// <summary>Gets the identifier correlating this report with one logical adaptation.</summary>
        public Guid CorrelationId { get; }
    }

    /// <summary>A sanitized local adaptation failure.</summary>
    public sealed class TypeAdaptationException : InvalidOperationException
    {
        internal TypeAdaptationException(
            Type sourceType,
            Type? targetType,
            string providerIdentity,
            string? strategyId,
            TypeAdaptationLoss? loss,
            TypeAdaptationReasonCode reasonCode,
            IReadOnlyList<TypeAdaptationAttempt>? attempts = null)
            : base(CreateMessage(sourceType, targetType, providerIdentity, strategyId, reasonCode))
        {
            SourceType = sourceType;
            TargetType = targetType;
            ProviderIdentity = providerIdentity;
            StrategyId = strategyId;
            Loss = loss;
            ReasonCode = reasonCode;
            Attempts = attempts ?? Array.Empty<TypeAdaptationAttempt>();
        }

        /// <summary>Gets the runtime source type.</summary>
        public Type SourceType { get; }
        /// <summary>Gets the intended target type when one was resolved.</summary>
        public Type? TargetType { get; }
        /// <summary>Gets the sanitized provider identity.</summary>
        public string ProviderIdentity { get; }
        /// <summary>Gets the selected strategy identifier, if any.</summary>
        public string? StrategyId { get; }
        /// <summary>Gets the selected strategy's loss classification, if any.</summary>
        public TypeAdaptationLoss? Loss { get; }
        /// <summary>Gets the structured failure reason.</summary>
        public TypeAdaptationReasonCode ReasonCode { get; }
        /// <summary>Gets the bounded, value-free sequence of attempted strategies.</summary>
        public IReadOnlyList<TypeAdaptationAttempt> Attempts { get; }

        private static string CreateMessage(
            Type sourceType,
            Type? targetType,
            string providerIdentity,
            string? strategyId,
            TypeAdaptationReasonCode reasonCode)
        {
            return "Type adaptation failed. SourceType=" + sourceType.FullName +
                   "; TargetType=" + (targetType?.FullName ?? "unknown") +
                   "; Provider=" + providerIdentity +
                   "; Strategy=" + (strategyId ?? "none") +
                   "; Reason=" + reasonCode + ".";
        }
    }
}
