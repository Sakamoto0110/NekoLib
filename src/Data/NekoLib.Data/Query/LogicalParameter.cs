#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NekoLib.Data.Query
{
    /// <summary>Configures neutral metadata for one logical query parameter.</summary>
    public sealed class LogicalParameterOptions
    {
        internal Type? SemanticTypeValue { get; private set; }
        internal TypePromotionRule? PromotionRuleValue { get; private set; }
        private readonly List<TypeDecayRule> _decayRules = new List<TypeDecayRule>();

        internal IReadOnlyList<TypeDecayRule> DecayRulesValue =>
            new ReadOnlyCollection<TypeDecayRule>(_decayRules.ToArray());

        /// <summary>Declares the semantic type expected after promotion.</summary>
        /// <param name="semanticType">The exact semantic type.</param>
        /// <returns>This options instance.</returns>
        public LogicalParameterOptions As(Type semanticType)
        {
            SemanticTypeValue = semanticType ?? throw new ArgumentNullException(nameof(semanticType));
            ValidateRuleTargets();
            return this;
        }

        /// <summary>Declares <typeparamref name="T"/> as the semantic type.</summary>
        /// <typeparam name="T">The exact semantic type.</typeparam>
        /// <returns>This options instance.</returns>
        public LogicalParameterOptions As<T>()
        {
            return As(typeof(T));
        }

        /// <summary>Attaches the only promotion rule authorized for this parameter.</summary>
        /// <param name="rule">A rule whose target matches the semantic type.</param>
        /// <returns>This options instance.</returns>
        public LogicalParameterOptions AllowPromotion(TypePromotionRule rule)
        {
            PromotionRuleValue = rule ?? throw new ArgumentNullException(nameof(rule));
            if (SemanticTypeValue == null)
                SemanticTypeValue = rule.TargetType;
            ValidateRuleTargets();
            return this;
        }

        /// <summary>Sets the primary provider-representation fallback for this parameter.</summary>
        /// <param name="rule">A rule whose source matches the semantic type.</param>
        /// <returns>This options instance.</returns>
        public LogicalParameterOptions AllowDecay(TypeDecayRule rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            _decayRules.Clear();
            _decayRules.Add(rule);
            ValidateRuleTargets();
            return this;
        }

        /// <summary>
        /// Appends an ordered representation fallback after the primary decay
        /// rule. Every rule converts from the same semantic source value.
        /// </summary>
        public LogicalParameterOptions AllowDecayFallback(TypeDecayRule rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            if (_decayRules.Count == 0)
            {
                throw new InvalidOperationException(
                    "A primary decay rule must be configured before a fallback.");
            }
            _decayRules.Add(rule);
            ValidateRuleTargets();
            return this;
        }

        internal LogicalParameterOptions Copy()
        {
            LogicalParameterOptions copy = new LogicalParameterOptions();
            copy.SemanticTypeValue = SemanticTypeValue;
            copy.PromotionRuleValue = PromotionRuleValue;
            copy._decayRules.AddRange(_decayRules);
            return copy;
        }

        internal void Validate(object? value)
        {
            ValidateRuleTargets();
            if (PromotionRuleValue != null && value != null &&
                !PromotionRuleValue.SourceType.IsInstanceOfType(value))
            {
                throw new ArgumentException(
                    "The promotion rule source type does not match the supplied value.",
                    nameof(value));
            }
        }

        private void ValidateRuleTargets()
        {
            if (PromotionRuleValue != null && SemanticTypeValue != null &&
                PromotionRuleValue.TargetType != SemanticTypeValue)
            {
                throw new InvalidOperationException(
                    "The promotion rule target must match the requested semantic type.");
            }

            for (int index = 0; index < _decayRules.Count; index++)
            {
                TypeDecayRule decayRule = _decayRules[index];
                if (SemanticTypeValue != null && decayRule.SourceType != SemanticTypeValue)
                {
                    throw new InvalidOperationException(
                        "Every decay rule source must match the requested semantic type.");
                }
            }
        }
    }

    /// <summary>
    /// Immutable provider-neutral identity, provenance, and adaptation intent
    /// for one logical query parameter.
    /// </summary>
    public sealed class LogicalParameter
    {
        internal LogicalParameter(
            string name,
            object? value,
            string? table,
            string? column,
            Type? semanticType,
            TypePromotionRule? promotionRule,
            IEnumerable<TypeDecayRule>? decayRules)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A logical parameter name is required.", nameof(name));

            Name = name;
            Value = value;
            Table = table;
            Column = column;
            SemanticType = semanticType ?? value?.GetType();
            PromotionRule = promotionRule;
            List<TypeDecayRule> decaySnapshot = decayRules == null
                ? new List<TypeDecayRule>()
                : new List<TypeDecayRule>(decayRules);
            DecayRules = new ReadOnlyCollection<TypeDecayRule>(decaySnapshot);
            DecayRule = decaySnapshot.Count == 0 ? null : decaySnapshot[0];
        }

        /// <summary>Gets the logical parameter name.</summary>
        public string Name { get; }
        /// <summary>Gets the original consumer value.</summary>
        public object? Value { get; }
        /// <summary>Gets the optional structured table provenance.</summary>
        public string? Table { get; }
        /// <summary>Gets the optional structured column provenance.</summary>
        public string? Column { get; }
        /// <summary>Gets the declared or inferred semantic type.</summary>
        public Type? SemanticType { get; }
        /// <summary>Gets the explicitly authorized promotion rule, if any.</summary>
        public TypePromotionRule? PromotionRule { get; }
        /// <summary>Gets the ordered provider-representation fallback candidates.</summary>
        public IReadOnlyList<TypeDecayRule> DecayRules { get; }
        /// <summary>Gets the primary fallback for single-rule compatibility.</summary>
        public TypeDecayRule? DecayRule { get; }

        internal LogicalParameter WithName(string name)
        {
            return new LogicalParameter(
                name,
                Value,
                Table,
                Column,
                SemanticType,
                PromotionRule,
                DecayRules);
        }

        internal LogicalParameter WithValue(object? value)
        {
            return new LogicalParameter(
                Name,
                value,
                Table,
                Column,
                SemanticType,
                PromotionRule,
                DecayRules);
        }
    }

    internal sealed class PendingLogicalValue
    {
        public PendingLogicalValue(object? value, LogicalParameterOptions options)
        {
            Value = value;
            Options = options;
        }

        public object? Value { get; }
        public LogicalParameterOptions Options { get; }
    }
}
