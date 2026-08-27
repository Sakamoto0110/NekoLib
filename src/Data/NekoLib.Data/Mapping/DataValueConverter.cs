#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace NekoLib.Data.Mapping
{
    /// <summary>
    /// Shared invariant conversion matrix for DTO property bindings.
    /// </summary>
    /// <remarks>
    /// The matrix accepts null only for nullable targets, preserves assignable
    /// values, supports enum names and integral values, Guid, DateTime,
    /// DateTimeOffset, TimeSpan, byte arrays, and invariant IConvertible
    /// conversions. Every other conversion is unsupported.
    /// </remarks>
    internal static class DataValueConverter
    {
        public static object? ConvertValue(object? value, Type targetType)
        {
            TypeAdaptationEventArgs? ignored;
            return ConvertValue(
                value,
                targetType,
                null,
                null,
                null,
                null,
                out ignored);
        }

        public static object? ConvertValue(
            object? value,
            Type targetType,
            ReadTypeAdaptationContext? adaptationContext,
            Type? dtoType,
            string? propertyName,
            string? columnName,
            out TypeAdaptationEventArgs? completedAdaptation)
        {
            completedAdaptation = null;
            if (targetType == null)
                throw new ArgumentNullException(nameof(targetType));

            Type? nullableType = Nullable.GetUnderlyingType(targetType);
            Type effectiveTarget = nullableType ?? targetType;

            if (value == null || value is DBNull)
            {
                if (!targetType.IsValueType || nullableType != null)
                    return null;

                throw new InvalidCastException(
                    "A database null cannot be assigned to a non-nullable property.");
            }

            Type sourceType = value.GetType();
            if (effectiveTarget.IsAssignableFrom(sourceType))
                return value;

            if (adaptationContext != null &&
                dtoType != null &&
                propertyName != null &&
                columnName != null &&
                IsTemporalConversion(sourceType, effectiveTarget))
            {
                return ConvertTemporal(
                    value,
                    sourceType,
                    effectiveTarget,
                    adaptationContext,
                    dtoType,
                    propertyName,
                    columnName,
                    out completedAdaptation);
            }

            if (effectiveTarget == typeof(byte[]))
                throw Unsupported(sourceType, effectiveTarget);

            if (effectiveTarget.IsEnum)
            {
                if (value is string enumName)
                    return Enum.Parse(effectiveTarget, enumName, ignoreCase: true);
                if (IsIntegralType(sourceType))
                    return Enum.ToObject(effectiveTarget, value);
                throw Unsupported(sourceType, effectiveTarget);
            }

            if (effectiveTarget == typeof(Guid))
            {
                if (value is string guidText)
                    return Guid.Parse(guidText);
                throw Unsupported(sourceType, effectiveTarget);
            }

            if (effectiveTarget == typeof(DateTimeOffset))
            {
                if (value is DateTime dateTime)
                    return new DateTimeOffset(dateTime);
                if (value is string offsetText)
                {
                    return DateTimeOffset.Parse(
                        offsetText,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind);
                }
                throw Unsupported(sourceType, effectiveTarget);
            }

            if (effectiveTarget == typeof(DateTime) && value is DateTimeOffset dateTimeOffset)
                return dateTimeOffset.UtcDateTime;

            if (effectiveTarget == typeof(TimeSpan))
            {
                if (value is string timeSpanText)
                    return TimeSpan.Parse(timeSpanText, CultureInfo.InvariantCulture);
                throw Unsupported(sourceType, effectiveTarget);
            }

            if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(effectiveTarget))
                return System.Convert.ChangeType(value, effectiveTarget, CultureInfo.InvariantCulture);

            throw Unsupported(sourceType, effectiveTarget);
        }

        private static object ConvertTemporal(
            object value,
            Type sourceType,
            Type targetType,
            ReadTypeAdaptationContext context,
            Type dtoType,
            string propertyName,
            string columnName,
            out TypeAdaptationEventArgs? completedAdaptation)
        {
            completedAdaptation = null;
            TypeMaterializationRule? rule = context.FindExplicit(
                dtoType,
                propertyName,
                columnName,
                sourceType,
                targetType);
            bool explicitRule = rule != null;
            TypeAdaptationReasonCode reasonCode = TypeAdaptationReasonCode.ExplicitRule;
            if (rule == null)
            {
                rule = context.FindAutomatic(sourceType, targetType);
                reasonCode = TypeAdaptationReasonCode.BuiltInRule;
            }

            if (rule == null)
            {
                throw new TypeAdaptationException(
                    sourceType,
                    targetType,
                    context.ProviderIdentity,
                    null,
                    null,
                    TypeAdaptationReasonCode.MaterializationRuleMissing);
            }

            if (rule.Loss == TypeAdaptationLoss.PotentiallyLossy &&
                (!explicitRule ||
                 context.LossPolicy != TypeLossPolicy.AllowExplicitAndReport))
            {
                throw Failure(
                    sourceType,
                    targetType,
                    context.ProviderIdentity,
                    rule,
                    TypeAdaptationReasonCode.LossyAdaptationNotAuthorized);
            }

            object? converted;
            try
            {
                converted = rule.Convert(value);
            }
            catch (OverflowException)
            {
                throw Failure(
                    sourceType,
                    targetType,
                    context.ProviderIdentity,
                    rule,
                    TypeAdaptationReasonCode.Overflow);
            }
            catch (TypeAdaptationException)
            {
                throw;
            }
            catch
            {
                throw Failure(
                    sourceType,
                    targetType,
                    context.ProviderIdentity,
                    rule,
                    TypeAdaptationReasonCode.ConversionRejected);
            }

            if (converted == null || !targetType.IsInstanceOfType(converted))
            {
                throw Failure(
                    sourceType,
                    targetType,
                    context.ProviderIdentity,
                    rule,
                    TypeAdaptationReasonCode.ConversionRejected);
            }

            IReadOnlyList<TypeAdaptationAttempt> attempts = Attempts(rule, reasonCode);
            completedAdaptation = new TypeAdaptationEventArgs(
                TypeAdaptationDirection.Read,
                TypeAdaptationKind.Materialization,
                sourceType,
                targetType,
                context.ProviderIdentity,
                targetType.FullName ?? targetType.Name,
                null,
                columnName,
                null,
                propertyName,
                rule.StrategyId,
                rule.Loss,
                reasonCode,
                rule.Format,
                rule.CultureName,
                attempts,
                context.CorrelationId);
            return converted;
        }

        private static bool IsTemporalConversion(Type sourceType, Type targetType)
        {
            return IsTemporalType(sourceType) || IsTemporalType(targetType);
        }

        private static bool IsTemporalType(Type type)
        {
            return type == typeof(DateTime) || type == typeof(DateTimeOffset);
        }

        private static TypeAdaptationException Failure(
            Type sourceType,
            Type targetType,
            string providerIdentity,
            TypeMaterializationRule rule,
            TypeAdaptationReasonCode reasonCode)
        {
            return new TypeAdaptationException(
                sourceType,
                targetType,
                providerIdentity,
                rule.StrategyId,
                rule.Loss,
                reasonCode,
                Attempts(rule, reasonCode));
        }

        private static IReadOnlyList<TypeAdaptationAttempt> Attempts(
            TypeMaterializationRule rule,
            TypeAdaptationReasonCode reasonCode)
        {
            return new ReadOnlyCollection<TypeAdaptationAttempt>(new[]
            {
                new TypeAdaptationAttempt(
                    rule.StrategyId,
                    rule.TargetType,
                    rule.Loss,
                    reasonCode,
                    rule.Format,
                    rule.CultureName)
            });
        }

        private static bool IsIntegralType(Type type)
        {
            return type == typeof(byte) ||
                   type == typeof(sbyte) ||
                   type == typeof(short) ||
                   type == typeof(ushort) ||
                   type == typeof(int) ||
                   type == typeof(uint) ||
                   type == typeof(long) ||
                   type == typeof(ulong);
        }

        private static InvalidCastException Unsupported(Type sourceType, Type targetType)
        {
            return new InvalidCastException(
                "Conversion from " + sourceType.FullName + " to " +
                targetType.FullName + " is not supported.");
        }
    }

    internal sealed class ReadTypeAdaptationContext
    {
        private readonly IReadOnlyList<TypeMaterializationRule> _automaticRules;
        private readonly IReadOnlyList<ReadTypeAdaptationRule> _explicitRules;
        private readonly Action<TypeAdaptationEventArgs> _report;

        public ReadTypeAdaptationContext(
            DatabaseGatewayOptions options,
            string providerIdentity,
            Guid correlationId,
            Action<TypeAdaptationEventArgs> report)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            ProviderIdentity = providerIdentity ?? throw new ArgumentNullException(nameof(providerIdentity));
            CorrelationId = correlationId;
            _report = report ?? throw new ArgumentNullException(nameof(report));
            LossPolicy = options.TypeLossPolicy;
            _automaticRules = new ReadOnlyCollection<TypeMaterializationRule>(
                new List<TypeMaterializationRule>(options.AutomaticMaterializationRules));
            _explicitRules = new ReadOnlyCollection<ReadTypeAdaptationRule>(
                new List<ReadTypeAdaptationRule>(options.ReadTypeAdaptationRules));
        }

        public TypeLossPolicy LossPolicy { get; }
        public string ProviderIdentity { get; }
        public Guid CorrelationId { get; }

        public TypeMaterializationRule? FindExplicit(
            Type dtoType,
            string propertyName,
            string columnName,
            Type sourceType,
            Type targetType)
        {
            TypeMaterializationRule? propertyFallback = null;
            for (int index = 0; index < _explicitRules.Count; index++)
            {
                ReadTypeAdaptationRule binding = _explicitRules[index];
                TypeMaterializationRule rule = binding.Adaptation;
                if (binding.DtoType == dtoType &&
                    string.Equals(binding.PropertyName, propertyName, StringComparison.Ordinal) &&
                    rule.SourceType == sourceType &&
                    rule.TargetType == targetType)
                {
                    if (binding.ColumnName == null)
                    {
                        propertyFallback = rule;
                    }
                    else if (string.Equals(
                        binding.ColumnName,
                        columnName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return rule;
                    }
                }
            }
            return propertyFallback;
        }

        public TypeMaterializationRule? FindAutomatic(Type sourceType, Type targetType)
        {
            for (int index = 0; index < _automaticRules.Count; index++)
            {
                TypeMaterializationRule rule = _automaticRules[index];
                if (rule.SourceType == sourceType && rule.TargetType == targetType)
                    return rule;
            }
            return null;
        }

        public void Report(TypeAdaptationEventArgs args)
        {
            _report(args);
        }
    }
}
