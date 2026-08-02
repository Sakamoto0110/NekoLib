#nullable enable
using System;
using System.Collections.Generic;

namespace NekoLib.Data.Query
{
    internal sealed class SqlParameterToken
    {
        public SqlParameterToken(int start, int length, string name, int index)
        {
            Start = start;
            Length = length;
            Name = name;
            Index = index;
        }

        public int Start { get; }
        public int Length { get; }
        public string Name { get; }
        public int Index { get; }
    }

    /// <summary>
    /// Locates generated <c>@pN</c> placeholders outside SQL literals,
    /// quoted identifiers, and comments.
    /// </summary>
    internal static class SqlParameterTokenizer
    {
        public static IReadOnlyList<SqlParameterToken> Tokenize(string sql)
        {
            if (sql == null)
                throw new ArgumentNullException(nameof(sql));

            List<SqlParameterToken> tokens = new List<SqlParameterToken>();
            int position = 0;

            while (position < sql.Length)
            {
                char current = sql[position];

                if (current == '\'' || current == '"' || current == '`')
                {
                    position = SkipQuoted(sql, position, current);
                    continue;
                }

                if (current == '[')
                {
                    position = SkipBracketed(sql, position);
                    continue;
                }

                if (current == '-' && HasNext(sql, position, '-'))
                {
                    position = SkipLineComment(sql, position);
                    continue;
                }

                if (current == '/' && HasNext(sql, position, '*'))
                {
                    position = SkipBlockComment(sql, position);
                    continue;
                }

                if (current == '@' &&
                    position + 2 < sql.Length &&
                    (sql[position + 1] == 'p' || sql[position + 1] == 'P') &&
                    char.IsDigit(sql[position + 2]))
                {
                    int end = position + 3;
                    while (end < sql.Length && char.IsDigit(sql[end]))
                        end++;

                    if (end == sql.Length || !IsIdentifierCharacter(sql[end]))
                    {
                        string name = sql.Substring(position, end - position);
                        int index;
                        if (!int.TryParse(name.Substring(2), out index))
                            index = 0;

                        tokens.Add(new SqlParameterToken(
                            position,
                            end - position,
                            name,
                            index));
                        position = end;
                        continue;
                    }
                }

                position++;
            }

            return tokens;
        }

        private static bool HasNext(string sql, int position, char expected)
        {
            return position + 1 < sql.Length && sql[position + 1] == expected;
        }

        private static bool IsIdentifierCharacter(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_';
        }

        private static int SkipQuoted(string sql, int position, char delimiter)
        {
            position++;
            while (position < sql.Length)
            {
                if (sql[position] != delimiter)
                {
                    position++;
                    continue;
                }

                if (position + 1 < sql.Length && sql[position + 1] == delimiter)
                {
                    position += 2;
                    continue;
                }

                return position + 1;
            }

            return position;
        }

        private static int SkipBracketed(string sql, int position)
        {
            position++;
            while (position < sql.Length)
            {
                if (sql[position] != ']')
                {
                    position++;
                    continue;
                }

                if (position + 1 < sql.Length && sql[position + 1] == ']')
                {
                    position += 2;
                    continue;
                }

                return position + 1;
            }

            return position;
        }

        private static int SkipLineComment(string sql, int position)
        {
            position += 2;
            while (position < sql.Length && sql[position] != '\r' && sql[position] != '\n')
                position++;
            return position;
        }

        private static int SkipBlockComment(string sql, int position)
        {
            position += 2;
            while (position + 1 < sql.Length)
            {
                if (sql[position] == '*' && sql[position + 1] == '/')
                    return position + 2;
                position++;
            }
            return sql.Length;
        }
    }
}
