#nullable enable
using System;

namespace NekoLib.Data.Mapping
{
    /// <summary>
    /// Selects how DTO mapping handles a column-to-property conversion failure.
    /// </summary>
    public enum DataMappingFailureMode
    {
        /// <summary>Throw a <see cref="DataMappingException"/> immediately.</summary>
        Strict = 0,

        /// <summary>Leave the affected property unchanged and continue mapping.</summary>
        Lenient = 1
    }

    /// <summary>
    /// Reports a failed column-to-property binding without including the source value.
    /// </summary>
    public sealed class DataMappingException : InvalidOperationException
    {
        public DataMappingException(
            string columnName,
            string propertyName,
            Type sourceType,
            Type targetType,
            Exception innerException)
            : base(
                "Cannot map column '" + columnName + "' from " +
                sourceType.FullName + " to property '" + propertyName +
                "' of type " + targetType.FullName + ".",
                innerException)
        {
            ColumnName = columnName ?? throw new ArgumentNullException(nameof(columnName));
            PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
            SourceType = sourceType ?? throw new ArgumentNullException(nameof(sourceType));
            TargetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
        }

        public string ColumnName { get; }
        public string PropertyName { get; }
        public Type SourceType { get; }
        public Type TargetType { get; }
    }
}
