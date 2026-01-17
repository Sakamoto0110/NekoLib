namespace NekoLib.Core
{
    public sealed class NekoEnvironment : INekoEnvironment
    {
        public bool IsDevelopment { get; set; }
        public bool IsProduction { get; set; } = true;
        public bool IsHeadless { get; set; }
    }

    public sealed class NekoConfiguration : INekoConfiguration
    {
        public static readonly NekoConfiguration Empty = new NekoConfiguration();

        public T Get<T>(string key, T defaultValue = default(T)) => defaultValue;
    }
}
