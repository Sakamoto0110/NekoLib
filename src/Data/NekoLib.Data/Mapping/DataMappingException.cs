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
        /// <summary>Creates a value-free description of a failed DTO property binding.</summary>
        /// <param name="columnName">The source column name.</param>
        /// <param name="propertyName">The destination property name.</param>
        /// <param name="sourceType">The source value type.</param>
        /// <param name="targetType">The destination property type.</param>
        /// <param name="innerException">The conversion or assignment failure.</param>
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
            AdaptationFailure = innerException as TypeAdaptationException;
        }

        /// <summary>Gets the source column name.</summary>
        public string ColumnName { get; }

        /// <summary>Gets the destination property name.</summary>
        public string PropertyName { get; }

        /// <summary>Gets the source value type.</summary>
        public Type SourceType { get; }

        /// <summary>Gets the destination property type.</summary>
        public Type TargetType { get; }

        /// <summary>Gets the structured adaptation failure when conversion used that subsystem.</summary>
        public TypeAdaptationException? AdaptationFailure { get; }
    }
}
