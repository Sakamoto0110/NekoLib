#nullable enable
using System;
using System.Globalization;
using System.Text;

namespace NekoLib.Data
{
    /// <summary>
    /// Represents one database record item (column).
    /// Stores its type, name, and invariant textual value.
    /// This format is intended for simple display and transport and does not
    /// preserve every original detail, including nulls, binary data, or
    /// provider-specific precision.
    /// </summary>
    public sealed class RecordItem
    {
        /// <summary>Gets or sets the source value type name.</summary>
        public string Type = string.Empty;

        /// <summary>Gets or sets the source column name.</summary>
        public string Name = string.Empty;

        /// <summary>Gets or sets the invariant textual representation.</summary>
        public string Value = string.Empty;

        /// <summary>Creates an empty record item.</summary>
        public RecordItem() { }

        /// <summary>Creates a record item from an integer value.</summary>
        /// <param name="V">The value to format with the invariant culture.</param>
        public RecordItem(int V)
        {
            Type = typeof(int).FullName ?? string.Empty;
            Value = V.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Creates a record item from a string value.</summary>
        /// <param name="V">The textual value.</param>
        public RecordItem(string V)
        {
            Type = typeof(string).FullName ?? string.Empty;
            Value = V;
        }

        /// <summary>Creates a record item from a double value.</summary>
        /// <param name="V">The value to format with the invariant culture.</param>
        public RecordItem(double V)
        {
            Type = typeof(double).FullName ?? string.Empty;
            Value = V.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Converts the textual value to a supported target type.</summary>
        /// <typeparam name="T">The requested target type.</typeparam>
        /// <param name="DefaultValue">The value returned when conversion fails or the source is blank.</param>
        /// <returns>The converted value, or <paramref name="DefaultValue"/>.</returns>
        public T As<T>(T DefaultValue = default!)
        {
            if (string.IsNullOrWhiteSpace(Value))
                return DefaultValue;

            try
            {
                Type target = typeof(T);

                if (target == typeof(string))
                    return (T)(object)Value;

                if (target == typeof(int) || target == typeof(int?))
                {
                    int i;
                    if (int.TryParse(Value, NumberStyles.Any, CultureInfo.InvariantCulture, out i))
                        return (T)(object)i;
                    return DefaultValue;
                }

                if (target == typeof(long) || target == typeof(long?))
                {
                    long i;
                    if (long.TryParse(Value, NumberStyles.Any, CultureInfo.InvariantCulture, out i))
                        return (T)(object)i;
                    return DefaultValue;
                }

                if (target == typeof(double) || target == typeof(double?))
                {
                    double d;
                    if (double.TryParse(Value, NumberStyles.Any, CultureInfo.InvariantCulture, out d))
                        return (T)(object)d;
                    return DefaultValue;
                }

                if (target == typeof(DateTime) || target == typeof(DateTime?))
                {
                    DateTime dt;
                    if (DateTime.TryParse(Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                        return (T)(object)dt;
                    return DefaultValue;
                }

                if (target == typeof(bool) || target == typeof(bool?))
                {
                    bool b;
                    if (bool.TryParse(Value, out b))
                        return (T)(object)b;

                    if (Value == "0") return (T)(object)false;
                    if (Value == "1") return (T)(object)true;
                    return DefaultValue;
                }

                try
                {
                    return (T)Convert.ChangeType(Value, typeof(T), CultureInfo.InvariantCulture);
                }
                catch
                {
                    return DefaultValue;
                }
            }
            catch
            {
                return DefaultValue;
            }
        }

        /// <summary>Returns the textual value.</summary>
        /// <returns>The stored value, or an empty string.</returns>
        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        /// <summary>Formats the type, name, and value using the legacy token grammar.</summary>
        /// <param name="Format">
        /// A sequence containing <c>t</c>, <c>n</c>, <c>v</c> or their labeled
        /// uppercase variants; <c>tnv</c> and <c>nv</c> have compact predefined forms.
        /// </param>
        /// <param name="FormatProvider">Accepted for compatibility; formatting is invariant.</param>
        /// <returns>The formatted record description.</returns>
        public string ToString(string Format, IFormatProvider FormatProvider)
        {
            if (string.IsNullOrEmpty(Format))
                return ToString();

            if (string.Equals(Format, "tnv", StringComparison.Ordinal))
                return "[type: " + Type + ", name: " + Name + ", value: " + Value + "]";
            if (string.Equals(Format, "nv", StringComparison.Ordinal))
                return "[name: " + Name + ", value: " + Value + "]";

            StringBuilder sb = new StringBuilder();
            sb.Append('[');

            for (int i = 0; i < Format.Length; i++)
            {
                char c = Format[i];
                switch (c)
                {
                    case 't': sb.Append(Type); break;
                    case 'n': sb.Append(Name); break;
                    case 'v': sb.Append(Value); break;

                    case 'T': sb.Append("Type: ").Append(Type); break;
                    case 'N': sb.Append("Name: ").Append(Name); break;
                    case 'V': sb.Append("Value: ").Append(Value); break;

                    case ',': sb.Append(", "); break;
                    case ' ': sb.Append(' '); break;

                    case '[':
                    case ']':
                        sb.Append(c);
                        break;

                    default:
                        sb.Append(c);
                        break;
                }
            }

            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>Converts a record item to an integer, returning zero on failure.</summary>
        /// <param name="Item">The record item.</param>
        public static explicit operator int(RecordItem Item)
        {
            return Item.As<int>();
        }

        /// <summary>Converts a record item to a double, returning zero on failure.</summary>
        /// <param name="Item">The record item.</param>
        public static explicit operator double(RecordItem Item)
        {
            return Item.As<double>();
        }

        /// <summary>Returns the record item's textual value.</summary>
        /// <param name="Item">The record item.</param>
        public static implicit operator string(RecordItem Item)
        {
            return Item.As<string>();
        }
    }
}
