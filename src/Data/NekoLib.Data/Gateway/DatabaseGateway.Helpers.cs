#nullable enable
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Reflection;

namespace NekoLib.Data.Internal.Gateway
{
    /// <summary>
    /// Extensões utilitárias para <see cref="IDataRecord"/> / <see cref="System.Data.Common.DbDataReader"/>.
    /// </summary>
    public static class DbDataReaderExtensions
    {
        /// <summary>
        /// Verifica se o reader contém a coluna especificada (case-insensitive).
        /// </summary>
        public static bool HasColumn(this IDataRecord Reader, string ColumnName)
        {
            if(Reader == null) throw new ArgumentNullException(nameof(Reader));
            if(string.IsNullOrEmpty(ColumnName)) return false;

            int fieldCount = Reader.FieldCount;
            for(int i = 0; i < fieldCount; i++)
            {
                string name = Reader.GetName(i);
                if(string.Equals(name, ColumnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }

    public partial class DatabaseGateway
    {
        #region Schema helpers

        /// <summary>
        /// Representa informações de esquema (colunas e tipos) para mapeamento dinâmico.
        /// </summary>
        private sealed class SchemaInfo
        {
            public List<string> Columns { get; private set; }
            public Dictionary<string, Type> ColumnTypes { get; private set; }
            public Dictionary<string, int> Ordinals { get; private set; }

            public SchemaInfo()
            {
                Columns = new List<string>();
                ColumnTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
                Ordinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static SchemaInfo ExtractSchema(DbDataReader Reader)
        {
            SchemaInfo result = new SchemaInfo();
            Dictionary<string, int> usedNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for(int i = 0; i < Reader.FieldCount; i++)
            {
                string rawName = Reader.GetName(i);
                if(string.IsNullOrWhiteSpace(rawName))
                    rawName = "Col" + i.ToString(CultureInfo.InvariantCulture);

                string name = MakeUniqueColumnName(rawName, usedNames);
                Type type;
                try { type = Reader.GetFieldType(i) ?? typeof(string); }
                catch { type = typeof(string); }

                result.Columns.Add(name);
                result.ColumnTypes[name] = type;
                result.Ordinals[name] = i;
            }

            return result;
        }

        private static string MakeUniqueColumnName(string name, Dictionary<string, int> usedNames)
        {
            int count;
            if(!usedNames.TryGetValue(name, out count))
            {
                usedNames[name] = 1;
                return name;
            }

            count++;
            usedNames[name] = count;
            return name + "_" + count.ToString(CultureInfo.InvariantCulture);
        }

        private static Dictionary<string, RecordItem> ReadRecordRow(DbDataReader reader, SchemaInfo schema)
        {
            Dictionary<string, RecordItem> row = new Dictionary<string, RecordItem>(StringComparer.OrdinalIgnoreCase);

            foreach(string col in schema.Columns)
            {
                object raw = reader.GetValue(schema.Ordinals[col]);
                row[col] = new RecordItem
                {
                    Name = col,
                    Type = schema.ColumnTypes[col].FullName ?? typeof(string).FullName!,
                    Value = raw is DBNull ? string.Empty : (Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty)
                };
            }

            return row;
        }

        #endregion
    }


}
