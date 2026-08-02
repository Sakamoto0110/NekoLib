#nullable enable
using System;
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
}
