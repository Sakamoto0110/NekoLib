namespace NekoLib.Core
{
    public sealed class DefaultEnvironment : INekoEnvironment
    {
        public static readonly DefaultEnvironment Development = new DefaultEnvironment(isDevelopment: true, isProduction: false, isHeadless: false);
        public static readonly DefaultEnvironment Production = new DefaultEnvironment(isDevelopment: false, isProduction: true, isHeadless: false);
        public static readonly DefaultEnvironment Headless = new DefaultEnvironment(isDevelopment: false, isProduction: true, isHeadless: true);

        public bool IsDevelopment { get; }
        public bool IsProduction { get; }
        public bool IsHeadless { get; }

        public DefaultEnvironment(bool isDevelopment, bool isProduction, bool isHeadless)
        {
            IsDevelopment = isDevelopment;
            IsProduction = isProduction;
            IsHeadless = isHeadless;
        }
    }
}
