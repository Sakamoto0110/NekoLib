#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Reflection;
using System.Text;
using NekoLib.Data.Dynamic;

namespace NekoLib.Data.Mapping
{
    /// <summary>
    /// Builds and caches one DTO binding plan per target type and reader schema.
    /// </summary>
    internal static class ReaderDtoMapper
    {
        private static readonly ConcurrentDictionary<string, ReaderBindingPlan> Plans =
            new ConcurrentDictionary<string, ReaderBindingPlan>();

        public static void ValidateTargetType(Type targetType)
        {
            if (targetType == null)
                throw new ArgumentNullException(nameof(targetType));
            if (targetType == typeof(object) || targetType == typeof(DynamicRow))
            {
                throw new InvalidOperationException(
                    "Object and DynamicRow targets belong to the dynamic API, not the DTO mapper.");
            }
            if (targetType.IsInterface || targetType.IsAbstract || targetType.ContainsGenericParameters)
            {
                throw new InvalidOperationException(
                    "DTO type '" + targetType.FullName + "' must be a concrete closed type.");
            }
            if (!targetType.IsValueType && targetType.GetConstructor(Type.EmptyTypes) == null)
            {
                throw new InvalidOperationException(
                    "DTO type '" + targetType.FullName + "' must have a public parameterless constructor.");
            }
        }

        public static T Map<T>(DbDataReader reader, DataMappingFailureMode failureMode)
        {
            return (T)Map(reader, typeof(T), failureMode, null);
        }

        public static T Map<T>(
            DbDataReader reader,
            DataMappingFailureMode failureMode,
            ReadTypeAdaptationContext adaptationContext)
        {
            return (T)Map(reader, typeof(T), failureMode, adaptationContext);
        }

        public static object Map(
            DbDataReader reader,
            Type targetType,
            DataMappingFailureMode failureMode)
        {
            return Map(reader, targetType, failureMode, null);
        }

        public static object Map(
            DbDataReader reader,
            Type targetType,
            DataMappingFailureMode failureMode,
            ReadTypeAdaptationContext? adaptationContext)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));
            ValidateTargetType(targetType);
            ValidateFailureMode(failureMode);

            string key = BuildPlanKey(reader, targetType);
            ReaderBindingPlan plan = Plans.GetOrAdd(
                key,
                _ => CreatePlan(reader, targetType));
            return plan.Map(reader, failureMode, adaptationContext);
        }

        private static ReaderBindingPlan CreatePlan(DbDataReader reader, Type targetType)
        {
            Dictionary<string, PropertyInfo> properties =
                new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
            PropertyInfo[] targetProperties = targetType.GetProperties(
                BindingFlags.Public | BindingFlags.Instance);
            for (int index = 0; index < targetProperties.Length; index++)
            {
                PropertyInfo property = targetProperties[index];
                if (property.CanWrite)
                    properties[property.Name] = property;
            }

            List<ReaderPropertyBinding> bindings = new List<ReaderPropertyBinding>();
            for (int ordinal = 0; ordinal < reader.FieldCount; ordinal++)
            {
                string columnName = reader.GetName(ordinal);
                PropertyInfo? property;
                if (!properties.TryGetValue(columnName, out property))
                    continue;

                Type sourceType;
                try
                {
                    sourceType = reader.GetFieldType(ordinal) ?? typeof(object);
                }
                catch
                {
                    sourceType = typeof(object);
                }

                bindings.Add(new ReaderPropertyBinding(
                    columnName,
                    ordinal,
                    sourceType,
                    property));
            }

            return new ReaderBindingPlan(targetType, bindings.ToArray());
        }

        private static string BuildPlanKey(DbDataReader reader, Type targetType)
        {
            StringBuilder key = new StringBuilder();
            AppendKeyPart(key, targetType.AssemblyQualifiedName ?? targetType.FullName ?? targetType.Name);
            key.Append('|').Append(reader.FieldCount);

            for (int ordinal = 0; ordinal < reader.FieldCount; ordinal++)
            {
                string columnName = reader.GetName(ordinal) ?? string.Empty;
                Type fieldType;
                try
                {
                    fieldType = reader.GetFieldType(ordinal) ?? typeof(object);
                }
                catch
                {
                    fieldType = typeof(object);
                }

                AppendKeyPart(key, columnName);
                AppendKeyPart(
                    key,
                    fieldType.AssemblyQualifiedName ?? fieldType.FullName ?? fieldType.Name);
            }

            return key.ToString();
        }

        private static void AppendKeyPart(StringBuilder key, string value)
        {
            key.Append('|').Append(value.Length).Append(':').Append(value);
        }

        private static void ValidateFailureMode(DataMappingFailureMode failureMode)
        {
            if (failureMode != DataMappingFailureMode.Strict &&
                failureMode != DataMappingFailureMode.Lenient)
            {
                throw new ArgumentOutOfRangeException(nameof(failureMode));
            }
        }

        private sealed class ReaderBindingPlan
        {
            private readonly Type _targetType;
            private readonly ReaderPropertyBinding[] _bindings;

            public ReaderBindingPlan(Type targetType, ReaderPropertyBinding[] bindings)
            {
                _targetType = targetType;
                _bindings = bindings;
            }

            public object Map(
                DbDataReader reader,
                DataMappingFailureMode failureMode,
                ReadTypeAdaptationContext? adaptationContext)
            {
                object? instance = Activator.CreateInstance(_targetType);
                if (instance == null)
                {
                    throw new InvalidOperationException(
                        "Could not construct DTO type '" + _targetType.FullName + "'.");
                }

                for (int index = 0; index < _bindings.Length; index++)
                {
                    ReaderPropertyBinding binding = _bindings[index];
                    try
                    {
                        object rawValue = reader.GetValue(binding.Ordinal);
                        TypeAdaptationEventArgs? completedAdaptation;
                        object? converted = DataValueConverter.ConvertValue(
                            rawValue,
                            binding.Property.PropertyType,
                            adaptationContext,
                            _targetType,
                            binding.Property.Name,
                            binding.ColumnName,
                            out completedAdaptation);
                        binding.Property.SetValue(instance, converted, null);
                        if (completedAdaptation != null)
                            adaptationContext!.Report(completedAdaptation);
                    }
                    catch (Exception ex)
                    {
                        Exception cause = ex is TargetInvocationException && ex.InnerException != null
                            ? ex.InnerException
                            : ex;
                        TypeAdaptationException? adaptationFailure = cause as TypeAdaptationException;
                        bool policyFailure = adaptationFailure != null &&
                            (adaptationFailure.ReasonCode ==
                                TypeAdaptationReasonCode.LossyAdaptationNotAuthorized ||
                             adaptationFailure.ReasonCode ==
                                TypeAdaptationReasonCode.MaterializationRuleMissing);
                        if (failureMode == DataMappingFailureMode.Lenient && !policyFailure)
                            continue;

                        Type sourceType = GetSourceType(reader, binding);
                        throw new DataMappingException(
                            binding.ColumnName,
                            binding.Property.Name,
                            sourceType,
                            binding.Property.PropertyType,
                            cause);
                    }
                }

                return instance;
            }

            private static Type GetSourceType(
                DbDataReader reader,
                ReaderPropertyBinding binding)
            {
                try
                {
                    object value = reader.GetValue(binding.Ordinal);
                    return value == null || value is DBNull
                        ? binding.SourceType
                        : value.GetType();
                }
                catch
                {
                    return binding.SourceType;
                }
            }
        }

        private sealed class ReaderPropertyBinding
        {
            public ReaderPropertyBinding(
                string columnName,
                int ordinal,
                Type sourceType,
                PropertyInfo property)
            {
                ColumnName = columnName;
                Ordinal = ordinal;
                SourceType = sourceType;
                Property = property;
            }

            public string ColumnName { get; }
            public int Ordinal { get; }
            public Type SourceType { get; }
            public PropertyInfo Property { get; }
        }
    }
}
