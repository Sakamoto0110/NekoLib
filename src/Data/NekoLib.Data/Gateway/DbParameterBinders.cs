#nullable enable
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;
using NekoLib.Data.Query;
#if NETFRAMEWORK
using System.Data.OleDb;
#endif

namespace NekoLib.Data.Internal.Gateway
{
    internal interface IDbParameterBinder
    {
        void Bind(DbCommand command, Dictionary<string, object?>? parameters);
    }

    internal static class DbParameterBinderFactory
    {
        public static IDbParameterBinder Create(
            DbCommand command,
            DbParameterBindingMode bindingMode)
        {
            if (bindingMode == DbParameterBindingMode.Positional ||
                (bindingMode == DbParameterBindingMode.Automatic && IsOleDb(command)))
            {
                return PositionalDbParameterBinder.Instance;
            }

            return NamedDbParameterBinder.Instance;
        }

        private static bool IsOleDb(DbCommand command)
        {
#if NETFRAMEWORK
            return command is OleDbCommand;
#else
            return string.Equals(
                command.GetType().FullName,
                "System.Data.OleDb.OleDbCommand",
                StringComparison.Ordinal);
#endif
        }
    }

    internal sealed class NamedDbParameterBinder : IDbParameterBinder
    {
        public static readonly NamedDbParameterBinder Instance =
            new NamedDbParameterBinder();

        private NamedDbParameterBinder()
        {
        }

        public void Bind(DbCommand command, Dictionary<string, object?>? parameters)
        {
            if (parameters == null)
                return;

            foreach (KeyValuePair<string, object?> parameter in parameters)
            {
                command.Parameters.Add(DbParameterFactory.Create(
                    command,
                    parameter.Key,
                    parameter.Value,
                    includeName: true));
            }
        }
    }

    internal sealed class PositionalDbParameterBinder : IDbParameterBinder
    {
        public static readonly PositionalDbParameterBinder Instance =
            new PositionalDbParameterBinder();

        private PositionalDbParameterBinder()
        {
        }

        public void Bind(DbCommand command, Dictionary<string, object?>? parameters)
        {
            IReadOnlyList<SqlParameterToken> tokens =
                SqlParameterTokenizer.Tokenize(command.CommandText);
            Dictionary<string, object?> supplied = Normalize(parameters);
            HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            StringBuilder sql = new StringBuilder(command.CommandText.Length);
            int previousEnd = 0;

            for (int index = 0; index < tokens.Count; index++)
            {
                SqlParameterToken token = tokens[index];
                if (!supplied.ContainsKey(token.Name))
                {
                    throw new ArgumentException(
                        "No value was supplied for positional placeholder " + token.Name + ".",
                        nameof(parameters));
                }

                used.Add(token.Name);
            }

            foreach (string parameterName in supplied.Keys)
            {
                if (!used.Contains(parameterName))
                {
                    throw new ArgumentException(
                        "The value for positional parameter " + parameterName + " is not used by the SQL text.",
                        nameof(parameters));
                }
            }

            for (int index = 0; index < tokens.Count; index++)
            {
                SqlParameterToken token = tokens[index];

                sql.Append(command.CommandText, previousEnd, token.Start - previousEnd);
                sql.Append('?');
                previousEnd = token.Start + token.Length;
                command.Parameters.Add(DbParameterFactory.Create(
                    command,
                    token.Name,
                    supplied[token.Name],
                    includeName: false));
            }

            sql.Append(command.CommandText, previousEnd, command.CommandText.Length - previousEnd);
            command.CommandText = sql.ToString();
        }

        private static Dictionary<string, object?> Normalize(
            Dictionary<string, object?>? parameters)
        {
            Dictionary<string, object?> normalized =
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            if (parameters == null)
                return normalized;

            foreach (KeyValuePair<string, object?> parameter in parameters)
            {
                if (string.IsNullOrWhiteSpace(parameter.Key))
                    throw new ArgumentException("Parameter names cannot be empty.", nameof(parameters));
                if (normalized.ContainsKey(parameter.Key))
                {
                    throw new ArgumentException(
                        "Positional parameter names must be unique ignoring case.",
                        nameof(parameters));
                }

                normalized.Add(parameter.Key, parameter.Value);
            }

            return normalized;
        }
    }

    internal static class DbParameterFactory
    {
        public static DbParameter Create(
            DbCommand command,
            string parameterName,
            object? value,
            bool includeName)
        {
            DbParameter parameter = command.CreateParameter();
            if (includeName)
                parameter.ParameterName = parameterName;

            DbParameterSpec? specification = value as DbParameterSpec;
            if (specification == null)
            {
                parameter.Value = value ?? DBNull.Value;
                return parameter;
            }

            specification.Validate(parameterName);
            if (specification.DbType.HasValue)
                parameter.DbType = specification.DbType.Value;
            if (specification.Size.HasValue)
                parameter.Size = specification.Size.Value;
            if (specification.Precision.HasValue)
                parameter.Precision = specification.Precision.Value;
            if (specification.Scale.HasValue)
                parameter.Scale = specification.Scale.Value;
            parameter.Direction = specification.Direction;
            parameter.Value = specification.Value ?? DBNull.Value;
            return parameter;
        }
    }
}
