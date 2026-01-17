namespace NekoLib.Core
{
    /// <summary>
    /// Minimal environment flags. Expand as needed.
    /// </summary>
    public interface INekoEnvironment
    {
        bool IsDevelopment { get; }
        bool IsProduction { get; }
        bool IsHeadless { get; }
    }
}
