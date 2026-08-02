#nullable enable
using System;
using System.Data;

namespace NekoLib.Data
{
    /// <summary>Controls how SQL parameter markers are bound.</summary>
    public enum DbParameterBindingMode
    {
        /// <summary>Uses positional binding for OleDb and named binding otherwise.</summary>
        Automatic = 0,

        /// <summary>Binds each supplied parameter once by name.</summary>
        Named = 1,

        /// <summary>
        /// Rewrites generated placeholders and binds once per SQL occurrence.
        /// Missing and unused supplied values are rejected before dispatch.
        /// </summary>
        Positional = 2
    }

    /// <summary>
    /// Describes provider-neutral metadata for one database parameter.
    /// </summary>
    /// <remarks>
    /// Pass an instance as a value in the existing parameter dictionary. A
    /// plain value remains supported and is treated as a specification that
    /// only sets <see cref="Value"/>.
    /// </remarks>
    public sealed class DbParameterSpec
    {
        public DbParameterSpec(object? value)
        {
            Value = value;
        }

        public object? Value { get; set; }
        public DbType? DbType { get; set; }
        public int? Size { get; set; }
        public byte? Precision { get; set; }
        public byte? Scale { get; set; }
        public ParameterDirection Direction { get; set; } = ParameterDirection.Input;

        internal void Validate(string parameterName)
        {
            if (Size.HasValue && Size.Value < -1)
                throw new ArgumentOutOfRangeException(parameterName, "Parameter size must be -1 or greater.");

            if (!Enum.IsDefined(typeof(ParameterDirection), Direction))
                throw new ArgumentOutOfRangeException(parameterName, "Parameter direction is invalid.");
        }
    }

    /// <summary>
    /// Contains overrides applied to one translated command.
    /// </summary>
    public sealed class DbCommandPolicy
    {
        public int? TimeoutSeconds { get; set; }

        internal void Validate(string parameterName)
        {
            if (TimeoutSeconds.HasValue && TimeoutSeconds.Value <= 0)
                throw new ArgumentOutOfRangeException(parameterName, "Command timeout must be greater than zero.");
        }

        internal DbCommandPolicy Copy()
        {
            return new DbCommandPolicy { TimeoutSeconds = TimeoutSeconds };
        }
    }
}
