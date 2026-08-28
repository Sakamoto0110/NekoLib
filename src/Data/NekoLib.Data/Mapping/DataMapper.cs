#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
#if NET6_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace NekoLib.Data.Mapping
{
    /// <summary>
    /// Maps the intentionally textual <see cref="RecordItem"/> row model to DTOs.
    /// </summary>
    /// <remarks>
    /// Strict mapping is the default. Use the overload that accepts
    /// <see cref="DataMappingFailureMode.Lenient"/> only for explicit legacy
    /// compatibility. This API cannot recover null or binary fidelity already
    /// lost by <see cref="RecordItem"/>.
    /// </remarks>
    public static class DataMapper
    {
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache =
            new ConcurrentDictionary<Type, PropertyInfo[]>();

        /// <summary>Maps a textual raw row to a new DTO using strict failure handling.</summary>
        /// <typeparam name="T">A DTO with a public parameterless constructor.</typeparam>
        /// <param name="row">The case-insensitive column map.</param>
        /// <returns>The populated DTO.</returns>
        public static T Map<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(Dictionary<string, RecordItem> row) where T : new()
        {
            return Map<T>(row, DataMappingFailureMode.Strict);
        }

        /// <summary>Maps a textual raw row to a new DTO.</summary>
        /// <typeparam name="T">A DTO with a public parameterless constructor.</typeparam>
        /// <param name="row">The case-insensitive column map.</param>
        /// <param name="failureMode">How conversion or assignment failures are handled.</param>
        /// <returns>The populated DTO.</returns>
        public static T Map<
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            T>(
            Dictionary<string, RecordItem> row,
            DataMappingFailureMode failureMode) where T : new()
        {
            if (row == null)
                throw new ArgumentNullException(nameof(row));

            T instance = new T();
            MapInto(instance!, row, failureMode);
            return instance;
        }

        /// <summary>Maps a textual raw row to a runtime-selected DTO type using strict failure handling.</summary>
        /// <param name="row">The case-insensitive column map.</param>
        /// <param name="targetType">A type with a public parameterless constructor and writable properties.</param>
        /// <returns>The populated DTO instance.</returns>
        public static object Map(
            Dictionary<string, RecordItem> row,
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            Type targetType)
        {
            return Map(row, targetType, DataMappingFailureMode.Strict);
        }

        /// <summary>Maps a textual raw row to a runtime-selected DTO type.</summary>
        /// <param name="row">The case-insensitive column map.</param>
        /// <param name="targetType">A type with a public parameterless constructor and writable properties.</param>
        /// <param name="failureMode">How conversion or assignment failures are handled.</param>
        /// <returns>The populated DTO instance.</returns>
        public static object Map(
            Dictionary<string, RecordItem> row,
#if NET6_0_OR_GREATER
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
            Type targetType,
            DataMappingFailureMode failureMode)
        {
            if (row == null)
                throw new ArgumentNullException(nameof(row));
            if (targetType == null)
                throw new ArgumentNullException(nameof(targetType));
            ValidateFailureMode(failureMode);

            object? instance = Activator.CreateInstance(targetType);
            if (instance == null)
            {
                throw new InvalidOperationException(
                    "Type '" + targetType.FullName + "' must have a public parameterless constructor.");
            }

            MapInto(instance, row, failureMode);
            return instance;
        }

        private static void MapInto(
            object instance,
            Dictionary<string, RecordItem> row,
            DataMappingFailureMode failureMode)
        {
            ValidateFailureMode(failureMode);
            PropertyInfo[] properties = PropertyCache.GetOrAdd(
                instance.GetType(),
                type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(property => property.CanWrite)
                    .ToArray());

            for (int index = 0; index < properties.Length; index++)
            {
                PropertyInfo property = properties[index];
                string columnName;
                RecordItem? record;
                if (!TryGetRecord(row, property.Name, out columnName, out record))
                    continue;

                try
                {
                    object? converted = DataValueConverter.ConvertValue(
                        record!.Value,
                        property.PropertyType);
                    property.SetValue(instance, converted, null);
                }
                catch (Exception ex)
                {
                    if (failureMode == DataMappingFailureMode.Lenient)
                        continue;

                    Exception cause = ex is TargetInvocationException && ex.InnerException != null
                        ? ex.InnerException
                        : ex;
                    throw new DataMappingException(
                        columnName,
                        property.Name,
                        typeof(string),
                        property.PropertyType,
                        cause);
                }
            }
        }

        private static bool TryGetRecord(
            Dictionary<string, RecordItem> row,
            string propertyName,
            out string columnName,
            out RecordItem? record)
        {
            if (row.TryGetValue(propertyName, out record) && record != null)
            {
                columnName = string.IsNullOrWhiteSpace(record.Name)
                    ? propertyName
                    : record.Name;
                return true;
            }

            foreach (KeyValuePair<string, RecordItem> item in row)
            {
                if (string.Equals(item.Key, propertyName, StringComparison.OrdinalIgnoreCase) &&
                    item.Value != null)
                {
                    record = item.Value;
                    columnName = string.IsNullOrWhiteSpace(record.Name)
                        ? item.Key
                        : record.Name;
                    return true;
                }
            }

            columnName = propertyName;
            record = null;
            return false;
        }

        private static void ValidateFailureMode(DataMappingFailureMode failureMode)
        {
            if (failureMode != DataMappingFailureMode.Strict &&
                failureMode != DataMappingFailureMode.Lenient)
            {
                throw new ArgumentOutOfRangeException(nameof(failureMode));
            }
        }
    }
}
