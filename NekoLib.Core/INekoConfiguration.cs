namespace NekoLib.Core
{
    /// <summary>
    /// Minimal key/value configuration abstraction.
    /// (File/JSON/env loading is entrypoint responsibility.)
    /// </summary>
    public interface INekoConfiguration
    {
        T Get<T>(string key, T defaultValue = default(T));
    }
}
