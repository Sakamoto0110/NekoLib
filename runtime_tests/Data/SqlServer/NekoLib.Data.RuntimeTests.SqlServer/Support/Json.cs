#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NekoLib.Data.RuntimeTests.SqlServer.Support
{
    /// <summary>
    /// A small JSON writer, because the artifact contract is JSON and this
    /// project must build on <c>net481</c>, where <c>System.Text.Json</c> is not
    /// in the framework. Taking a serializer package for six result files would
    /// put a dependency in the evidence record that has nothing to do with what
    /// is being measured.
    /// </summary>
    internal sealed class JsonWriter
    {
        private readonly StringBuilder _text = new StringBuilder();
        private readonly Stack<bool> _empty = new Stack<bool>();
        private int _depth;

        public JsonWriter()
        {
            _empty.Push(true);
        }

        public void Object(string? name, Action body)
        {
            Open(name, '{');
            body();
            Close('}');
        }

        public void Array(string? name, Action body)
        {
            Open(name, '[');
            body();
            Close(']');
        }

        public void Prop(string name, string? value)
        {
            Separate();
            NewLine();
            _text.Append(Quote(name)).Append(": ");
            if (value == null) _text.Append("null");
            else _text.Append(Quote(value));
        }

        public void Prop(string name, long value)
        {
            Separate();
            NewLine();
            _text.Append(Quote(name)).Append(": ")
                 .Append(value.ToString(CultureInfo.InvariantCulture));
        }

        public void Prop(string name, double value)
        {
            Separate();
            NewLine();
            _text.Append(Quote(name)).Append(": ").Append(Number(value));
        }

        public void Prop(string name, bool value)
        {
            Separate();
            NewLine();
            _text.Append(Quote(name)).Append(": ").Append(value ? "true" : "false");
        }

        public void Item(string? value)
        {
            Separate();
            NewLine();
            if (value == null) _text.Append("null");
            else _text.Append(Quote(value));
        }

        public void Item(long value)
        {
            Separate();
            NewLine();
            _text.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        public override string ToString() => _text.ToString();

        private void Open(string? name, char brace)
        {
            Separate();
            NewLine();
            if (name != null) _text.Append(Quote(name)).Append(": ");
            _text.Append(brace);
            _depth++;
            _empty.Push(true);
        }

        private void Close(char brace)
        {
            bool empty = _empty.Pop();
            _depth--;
            if (!empty) NewLine();
            _text.Append(brace);
        }

        private void Separate()
        {
            bool empty = _empty.Pop();
            if (!empty) _text.Append(',');
            _empty.Push(false);
        }

        private void NewLine()
        {
            if (_text.Length == 0) return;
            _text.Append('\n').Append(' ', _depth * 2);
        }

        private static string Number(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "null";
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string Quote(string value)
        {
            StringBuilder text = new StringBuilder(value.Length + 2);
            text.Append('"');

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"': text.Append("\\\""); break;
                    case '\\': text.Append("\\\\"); break;
                    case '\b': text.Append("\\b"); break;
                    case '\f': text.Append("\\f"); break;
                    case '\n': text.Append("\\n"); break;
                    case '\r': text.Append("\\r"); break;
                    case '\t': text.Append("\\t"); break;
                    default:
                        if (c < ' ')
                            text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            text.Append(c);
                        break;
                }
            }

            text.Append('"');
            return text.ToString();
        }
    }

    /// <summary>
    /// The matching reader, used only for an externally supplied fault
    /// schedule. Values come back as <see cref="Dictionary{TKey,TValue}"/>,
    /// <see cref="List{T}"/>, <see cref="string"/>, <see cref="double"/>,
    /// <see cref="bool"/>, or null.
    /// </summary>
    internal static class JsonParser
    {
        public static object? Parse(string text)
        {
            int index = 0;
            object? value = ReadValue(text, ref index);
            SkipWhitespace(text, ref index);
            if (index != text.Length)
                throw new FormatException("Trailing content after the JSON document at offset " + index + ".");

            return value;
        }

        public static Dictionary<string, object?> AsObject(object? value, string what)
        {
            if (value is Dictionary<string, object?> map) return map;
            throw new FormatException(what + " must be a JSON object.");
        }

        public static List<object?> AsArray(object? value, string what)
        {
            if (value is List<object?> list) return list;
            throw new FormatException(what + " must be a JSON array.");
        }

        public static string RequireString(Dictionary<string, object?> map, string name)
        {
            if (map.TryGetValue(name, out object? value) && value is string text) return text;
            throw new FormatException("Missing string property '" + name + "'.");
        }

        public static long RequireInt(Dictionary<string, object?> map, string name)
        {
            if (map.TryGetValue(name, out object? value) && value is double number)
                return (long)number;

            throw new FormatException("Missing numeric property '" + name + "'.");
        }

        public static string? OptionalString(Dictionary<string, object?> map, string name)
        {
            if (map.TryGetValue(name, out object? value) && value is string text) return text;
            return null;
        }

        private static object? ReadValue(string text, ref int index)
        {
            SkipWhitespace(text, ref index);
            if (index >= text.Length) throw new FormatException("Unexpected end of JSON.");

            char c = text[index];
            switch (c)
            {
                case '{': return ReadObject(text, ref index);
                case '[': return ReadArray(text, ref index);
                case '"': return ReadString(text, ref index);
                case 't': Expect(text, ref index, "true"); return true;
                case 'f': Expect(text, ref index, "false"); return false;
                case 'n': Expect(text, ref index, "null"); return null;
                default: return ReadNumber(text, ref index);
            }
        }

        private static Dictionary<string, object?> ReadObject(string text, ref int index)
        {
            Dictionary<string, object?> map = new Dictionary<string, object?>(StringComparer.Ordinal);
            index++;
            SkipWhitespace(text, ref index);

            if (index < text.Length && text[index] == '}') { index++; return map; }

            while (true)
            {
                SkipWhitespace(text, ref index);
                string name = ReadString(text, ref index);
                SkipWhitespace(text, ref index);
                if (index >= text.Length || text[index] != ':')
                    throw new FormatException("Expected ':' at offset " + index + ".");

                index++;
                map[name] = ReadValue(text, ref index);
                SkipWhitespace(text, ref index);

                if (index >= text.Length) throw new FormatException("Unterminated JSON object.");
                if (text[index] == ',') { index++; continue; }
                if (text[index] == '}') { index++; return map; }

                throw new FormatException("Expected ',' or '}' at offset " + index + ".");
            }
        }

        private static List<object?> ReadArray(string text, ref int index)
        {
            List<object?> list = new List<object?>();
            index++;
            SkipWhitespace(text, ref index);

            if (index < text.Length && text[index] == ']') { index++; return list; }

            while (true)
            {
                list.Add(ReadValue(text, ref index));
                SkipWhitespace(text, ref index);

                if (index >= text.Length) throw new FormatException("Unterminated JSON array.");
                if (text[index] == ',') { index++; continue; }
                if (text[index] == ']') { index++; return list; }

                throw new FormatException("Expected ',' or ']' at offset " + index + ".");
            }
        }

        private static string ReadString(string text, ref int index)
        {
            if (index >= text.Length || text[index] != '"')
                throw new FormatException("Expected a string at offset " + index + ".");

            index++;
            StringBuilder value = new StringBuilder();

            while (index < text.Length)
            {
                char c = text[index++];
                if (c == '"') return value.ToString();

                if (c != '\\') { value.Append(c); continue; }
                if (index >= text.Length) break;

                char escape = text[index++];
                switch (escape)
                {
                    case '"': value.Append('"'); break;
                    case '\\': value.Append('\\'); break;
                    case '/': value.Append('/'); break;
                    case 'b': value.Append('\b'); break;
                    case 'f': value.Append('\f'); break;
                    case 'n': value.Append('\n'); break;
                    case 'r': value.Append('\r'); break;
                    case 't': value.Append('\t'); break;
                    case 'u':
                        if (index + 4 > text.Length) throw new FormatException("Truncated \\u escape.");
                        value.Append((char)int.Parse(
                            text.Substring(index, 4),
                            NumberStyles.HexNumber,
                            CultureInfo.InvariantCulture));
                        index += 4;
                        break;
                    default:
                        throw new FormatException("Unsupported escape '\\" + escape + "'.");
                }
            }

            throw new FormatException("Unterminated JSON string.");
        }

        private static double ReadNumber(string text, ref int index)
        {
            int start = index;
            while (index < text.Length && "+-.eE0123456789".IndexOf(text[index]) >= 0)
                index++;

            if (start == index) throw new FormatException("Expected a value at offset " + index + ".");

            return double.Parse(
                text.Substring(start, index - start),
                NumberStyles.Float,
                CultureInfo.InvariantCulture);
        }

        private static void Expect(string text, ref int index, string literal)
        {
            if (index + literal.Length > text.Length ||
                string.CompareOrdinal(text, index, literal, 0, literal.Length) != 0)
            {
                throw new FormatException("Expected '" + literal + "' at offset " + index + ".");
            }

            index += literal.Length;
        }

        private static void SkipWhitespace(string text, ref int index)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
                index++;
        }
    }
}
