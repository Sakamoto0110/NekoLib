using System;


namespace NekoLib.Navigation.Metadata.Attributes
{
    /// <summary>
    /// Declares the idle timeout, in seconds, of inactivity that returns the app
    /// to the idle page. Only valid on the idle page itself (a page with
    /// <see cref="PageRole.Idle"/>, the <c>idle</c> tag, or a name containing
    /// "Idle"); the bootstrap throws if it is applied elsewhere.
    /// <para>
    /// Precedence (highest first): the bootstrap DSL <c>.IdleTimeout(seconds)</c>
    /// overrides this attribute, which overrides the global
    /// <c>UseIdleTimeout(milliseconds)</c> fallback.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class PageTimeoutAttribute : Attribute
    {
        /// <summary>Idle timeout in seconds. Values &lt;= 0 are ignored.</summary>
        public int Seconds { get; }

        public PageTimeoutAttribute(int seconds)
        {
            Seconds = seconds;
        }
    }
}
