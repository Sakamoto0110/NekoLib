#nullable enable
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

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

            public SchemaInfo()
            {
                Columns = new List<string>();
                ColumnTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static SchemaInfo ExtractSchema(DbDataReader Reader)
        {
            SchemaInfo result = new SchemaInfo();
            DataTable dt = Reader.GetSchemaTable();
            if(dt == null) return result;

            foreach(DataRow row in dt.Rows)
            {
                string name = row["ColumnName"] != null
                    ? row["ColumnName"].ToString() ?? ("Col" + result.Columns.Count)
                    : "Col" + result.Columns.Count;

                Type type = row["DataType"] as Type ?? typeof(string);

                result.Columns.Add(name);
                if(!result.ColumnTypes.ContainsKey(name))
                    result.ColumnTypes[name] = type;
            }

            return result;
        }

        #endregion
    }


}
