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

        public LogicalParameterOptions As(Type semanticType)
        {
            SemanticTypeValue = semanticType ?? throw new ArgumentNullException(nameof(semanticType));
            ValidateRuleTargets();
            return this;
        }

        public LogicalParameterOptions As<T>()
        {
            return As(typeof(T));
        }

        public LogicalParameterOptions AllowPromotion(TypePromotionRule rule)
        {
            PromotionRuleValue = rule ?? throw new ArgumentNullException(nameof(rule));
            if (SemanticTypeValue == null)
                SemanticTypeValue = rule.TargetType;
            ValidateRuleTargets();
            return this;
        }

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

        public string Name { get; }
        public object? Value { get; }
        public string? Table { get; }
        public string? Column { get; }
        public Type? SemanticType { get; }
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
