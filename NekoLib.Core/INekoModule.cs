namespace NekoLib.Core
{
    /// <summary>
    /// Pluggable module that can register services and participate in host lifecycle.
    /// </summary>
    public interface INekoModule
    {
        /// <summary>Stable module name (for diagnostics and ordering).</summary>
        string Name { get; }

        /// <summary>
        /// Called during host build. Register services and hooks here.
        /// </summary>
        void Configure(INekoModuleContext context);

        /// <summary>Called when the host starts.</summary>
        void Start();

        /// <summary>Called when the host stops.</summary>
        void Stop();
    }
}
