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
        Disabled = 0,
        ExplicitOnly = 1,
        SchemaValidated = 2
    }

    /// <summary>Controls fallback from a preferred provider representation.</summary>
    public enum TypeDecayPolicy
    {
        Strict = 0,
        AllowFallback = 1
    }

    /// <summary>Controls whether an explicit rule may lose semantic information.</summary>
    public enum TypeLossPolicy
    {
        RejectPotentialLoss = 0,
        AllowExplicitAndReport = 1
    }

    /// <summary>Controls automatic database-schema discovery.</summary>
    public enum SchemaDiscoveryMode
    {
        Disabled = 0,
        Lazy = 1,
        Preload = 2
    }

    /// <summary>Classifies whether an adaptation can discard semantic information.</summary>
    public enum TypeAdaptationLoss
    {
        Lossless = 0,
        PotentiallyLossy = 1
    }

    public enum TypeAdaptationDirection
    {
        Read = 0,
        Write = 1
    }

    public enum TypeAdaptationKind
    {
        Promotion = 0,
        Decay = 1
    }

    /// <summary>Stable, value-free reason codes for adaptation outcomes.</summary>
    public enum TypeAdaptationReasonCode
    {
        ExplicitRule = 0,
        SchemaValidatedRule = 1,
        ProviderFallback = 2,
        PromotionDisabled = 3,
        PromotionRuleMissing = 4,
        SourceTypeMismatch = 5,
        ConversionRejected = 6,
        Overflow = 7,
        SchemaUnavailable = 8,
        SchemaNotPreloaded = 9,
        SchemaRequiredBeforeTransaction = 10,
        ProviderRepresentationUnsupported = 11,
        LossyAdaptationNotAuthorized = 12,
        StrictDecayRejected = 13,
        UnknownProvider = 14
    }

    /// <summary>Converts one semantic value without receiving SQL or credentials.</summary>
    public delegate object? TypeValueConverter(object value);

    /// <summary>Describes one exact, locally executable input-promotion rule.</summary>
    public sealed class TypePromotionRule
    {
        private readonly TypeValueConverter _converter;

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

        public string StrategyId { get; }
        public Type SourceType { get; }
        public Type TargetType { get; }
        public TypeAdaptationLoss Loss { get; }
        public string? Format { get; }
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

        public string StrategyId { get; }
        public Type SourceType { get; }
        public Type TargetType { get; }
        public TypeAdaptationLoss Loss { get; }
        public string? Format { get; }
        public string? CultureName { get; }

        internal object? Convert(object value)
        {
            return _converter(value);
        }
    }

    /// <summary>Provider-neutral built-in promotion rules.</summary>
    public static class TypePromotions
    {
        public static TypePromotionRule StringToInt16 { get; } = new TypePromotionRule(
            "string-to-int16-invariant",
            typeof(string),
            typeof(short),
            value => short.Parse((string)value, NumberStyles.Integer, CultureInfo.InvariantCulture));

        public static TypePromotionRule StringToInt32 { get; } = new TypePromotionRule(
            "string-to-int32-invariant",
            typeof(string),
            typeof(int),
            value => int.Parse((string)value, NumberStyles.Integer, CultureInfo.InvariantCulture));

        public static TypePromotionRule StringToInt64 { get; } = new TypePromotionRule(
            "string-to-int64-invariant",
            typeof(string),
            typeof(long),
            value => long.Parse((string)value, NumberStyles.Integer, CultureInfo.InvariantCulture));

        public static TypePromotionRule StringToDecimal { get; } = new TypePromotionRule(
            "string-to-decimal-invariant",
            typeof(string),
            typeof(decimal),
            value => decimal.Parse((string)value, NumberStyles.Number, CultureInfo.InvariantCulture));

        public static TypePromotionRule StringToDouble { get; } = new TypePromotionRule(
            "string-to-double-invariant",
            typeof(string),
            typeof(double),
            value => double.Parse(
                (string)value,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture));

        public static TypePromotionRule StringToBoolean { get; } = new TypePromotionRule(
            "string-to-boolean-invariant",
            typeof(string),
            typeof(bool),
            value => bool.Parse((string)value));

        public static TypePromotionRule StringToGuid { get; } = new TypePromotionRule(
            "string-to-guid",
            typeof(string),
            typeof(Guid),
            value => Guid.Parse((string)value));

        public static TypePromotionRule StringToDateTimeIso8601 { get; } =
            CreateStringToDateTime("O", CultureInfo.InvariantCulture);

        public static TypePromotionRule StringToDateTimeOffsetIso8601 { get; } =
            CreateStringToDateTimeOffset("O", CultureInfo.InvariantCulture);

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
        public static TypeDecayRule DateTimeToRoundTripString { get; } = new TypeDecayRule(
            "datetime-to-roundtrip-string",
            typeof(DateTime),
            typeof(string),
            value => ((DateTime)value).ToString("O", CultureInfo.InvariantCulture),
            TypeAdaptationLoss.Lossless,
            "O",
            CultureInfo.InvariantCulture.Name);

        public static TypeDecayRule DateTimeOffsetToRoundTripString { get; } = new TypeDecayRule(
            "datetimeoffset-to-roundtrip-string",
            typeof(DateTimeOffset),
            typeof(string),
            value => ((DateTimeOffset)value).ToString("O", CultureInfo.InvariantCulture),
            TypeAdaptationLoss.Lossless,
            "O",
            CultureInfo.InvariantCulture.Name);

        public static TypeDecayRule DateTimeOffsetToUtcDateTime { get; } = new TypeDecayRule(
            "datetimeoffset-to-utc-datetime",
            typeof(DateTimeOffset),
            typeof(DateTime),
            value => ((DateTimeOffset)value).UtcDateTime,
            TypeAdaptationLoss.PotentiallyLossy);

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

        public string StrategyId { get; }
        public Type TargetType { get; }
        public TypeAdaptationLoss Loss { get; }
        public TypeAdaptationReasonCode ReasonCode { get; }
        public string? Format { get; }
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
            string parameterName,
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
            StrategyId = strategyId;
            Loss = loss;
            ReasonCode = reasonCode;
            Format = format;
            CultureName = cultureName;
            Attempts = attempts;
            CorrelationId = correlationId;
        }

        public TypeAdaptationDirection Direction { get; }
        public TypeAdaptationKind Kind { get; }
        public Type SourceType { get; }
        public Type TargetType { get; }
        public string ProviderIdentity { get; }
        public string BindingRepresentation { get; }
        public string? Table { get; }
        public string? Column { get; }
        public string ParameterName { get; }
        public string StrategyId { get; }
        public TypeAdaptationLoss Loss { get; }
        public TypeAdaptationReasonCode ReasonCode { get; }
        public string? Format { get; }
        public string? CultureName { get; }
        public IReadOnlyList<TypeAdaptationAttempt> Attempts { get; }
        public Guid CorrelationId { get; }
    }

    /// <summary>A sanitized local failure raised before database dispatch.</summary>
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

        public Type SourceType { get; }
        public Type? TargetType { get; }
        public string ProviderIdentity { get; }
        public string? StrategyId { get; }
        public TypeAdaptationLoss? Loss { get; }
        public TypeAdaptationReasonCode ReasonCode { get; }
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
