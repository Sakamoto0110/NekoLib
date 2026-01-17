#nullable enable
using System;
using System.Globalization;
using System.Text;
 
namespace NekoLib.Data
{
    /// <summary>
    /// Representa um item de registro (coluna) retornado do banco.
    /// Armazena tipo, nome e valor textual (cultura invariável).
    /// </summary>
    public class RecordItem
    {
        public string Type = string.Empty;
        public string Name = string.Empty;
        public string Value = string.Empty;

        public RecordItem() { }

        public RecordItem(int V)
        {
            Type = typeof(int).FullName;
            Value = V.ToString(CultureInfo.InvariantCulture);
        }

        public RecordItem(string V)
        {
            Type = typeof(string).FullName;
            Value = V;
        }

        public RecordItem(double V)
        {
            Type = typeof(double).FullName;
            Value = V.ToString(CultureInfo.InvariantCulture);
        }

        public T As<T>(T DefaultValue = default)
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

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

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

        public static explicit operator int(RecordItem Item)
        {
            return Item.As<int>();
        }

        public static explicit operator double(RecordItem Item)
        {
            return Item.As<double>();
        }

        public static implicit operator string(RecordItem Item)
        {
            return Item.As<string>();
        }
    }
}