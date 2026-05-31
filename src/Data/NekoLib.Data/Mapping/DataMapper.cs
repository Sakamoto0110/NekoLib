#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Collections.Concurrent;

namespace NekoLib.Data.Mapping
{
    /// <summary>
    /// Utilitário para mapear dicionários de <see cref="RecordItem"/> para DTOs.
    /// </summary>
    public static class DataMapper
    {
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _cache = new();

        public static T Map<T>(Dictionary<string, RecordItem> Row) where T : new()
        {
            if(Row == null) throw new ArgumentNullException(nameof(Row));

            T obj = new T();
            Type type = typeof(T);

            PropertyInfo[] props = _cache.GetOrAdd(type, static t =>
            {
                return t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Where(p => p.CanWrite)
                        .ToArray();
            });

            foreach(PropertyInfo prop in props)
            {
                RecordItem record;
                if(!TryGetRecord(Row, prop.Name, out record))
                    continue;

                object? converted = ConvertValue(record, prop.PropertyType);

                try
                {
                    prop.SetValue(obj, converted, null);
                }
                catch { /* ignora falhas individuais */ }
            }

            return obj;
        }

        public static object Map(Dictionary<string, RecordItem> row,Type targetType)
        {
            if(row == null)
                throw new ArgumentNullException(nameof(row));
            if(targetType == null)
                throw new ArgumentNullException(nameof(targetType));

            ConstructorInfo? ctor = targetType.GetConstructor(Type.EmptyTypes);
            if(ctor == null)
                throw new InvalidOperationException($"Type '{targetType.FullName}' must have a parameterless constructor.");

            object instance = ctor.Invoke(null);
            MapInto(instance, row);
            return instance;
        }
        private static void MapInto(object instance,Dictionary<string, RecordItem> row)
        {
            Type type = instance.GetType();
            PropertyInfo[] props = _cache.GetOrAdd(type, static t =>
            {
                return t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Where(p => p.CanWrite)
                        .ToArray();
            });

            for(int i = 0; i < props.Length; i++)
            {
                PropertyInfo prop = props[i];

                RecordItem record;
                if(!TryGetRecord(row, prop.Name, out record))
                    continue;

                try
                {
                    object? converted = ConvertValue(record, prop.PropertyType);
                    prop.SetValue(instance, converted, null);
                }
                catch { /* ignora falhas individuais */ }
            }
        }

        private static bool TryGetRecord(Dictionary<string, RecordItem> row, string name, out RecordItem record)
        {
            RecordItem? found;
            if(row.TryGetValue(name, out found) && found != null)
            {
                record = found;
                return true;
            }

            foreach(KeyValuePair<string, RecordItem> kv in row)
            {
                if(string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    record = kv.Value;
                    return true;
                }
            }

            record = null!;
            return false;
        }

        private static object? ConvertValue(RecordItem Record, Type Target)
        {
            Type effectiveTarget = Nullable.GetUnderlyingType(Target) ?? Target;

            if(Target == typeof(string)) return Record.As<string>();

            if(Target == typeof(int) || Target == typeof(int?))
                return Record.As<int>();

            if(Target == typeof(long) || Target == typeof(long?))
                return Record.As<long>();

            if(Target == typeof(double) || Target == typeof(double?))
                return Record.As<double>();

            if(Target == typeof(decimal) || Target == typeof(decimal?))
            {
                decimal d;
                if(decimal.TryParse(Record.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out d))
                    return d;
                return default(decimal);
            }

            if(Target == typeof(bool) || Target == typeof(bool?))
                return Record.As<bool>();

            if(Target == typeof(DateTime) || Target == typeof(DateTime?))
                return Record.As<DateTime>();

            try
            {
                if(effectiveTarget.IsEnum)
                    return Enum.Parse(effectiveTarget, Record.Value);

                return Convert.ChangeType(Record.Value, effectiveTarget, CultureInfo.InvariantCulture);
            }
            catch { return null; }
             
        }
    }
}
