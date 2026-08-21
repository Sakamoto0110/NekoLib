using System;
using System.Collections.Generic;

namespace NekoLib.Navigation.Contracts.Guards
{
    internal static class GuardContractValidation
    {
        internal static string RequireName(string value, string parameterName, string kind)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(
                    $"A {kind} cannot be null, empty, or whitespace.", parameterName);

            return value;
        }

        internal static string[] CopyNames(
            IEnumerable<string> values,
            string parameterName,
            string kind)
        {
            if (values == null)
                throw new ArgumentNullException(parameterName);

            var copy = new List<string>();
            foreach (var value in values)
                copy.Add(RequireName(value, parameterName, kind));

            return copy.ToArray();
        }
    }
}
