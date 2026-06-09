using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;

namespace NekoLib.Navigation.Bootstrap
{
    public sealed class PageBuilderConfigurator
    {
        private readonly PageMetadataBuilder _builder;

        public PageBuilderConfigurator(PageMetadataBuilder builder)
        {
            _builder = builder;
        }

        public PageRuleBuilder<T> Page<T>() where T : IPageView
            => new PageRuleBuilder<T>(_builder);
    }
    public sealed class PageRuleBuilder<T> where T : IPageView
    {
        private readonly PageMetadataBuilder _builder;

        internal PageRuleBuilder(PageMetadataBuilder builder)
        {
            _builder = builder;
        }

        public PageRuleBuilder<T> AsIdle()
        {
            _builder.Register<T>(d => d.Role = PageRole.Idle);
            return this;
        }

        public PageRuleBuilder<T> Named(string name)
        {
            _builder.Register<T>(d => d.Name = name);
            return this;
        }

        public PageRuleBuilder<T> Cache(PageReusePolicy policy)
        {
            _builder.Register<T>(d => d.ReusePolicy = policy);
            return this;
        }

        public PageRuleBuilder<T> StrongSingleton() => Cache(PageReusePolicy.StrongSingleton);
        public PageRuleBuilder<T> WeakSingleton() => Cache(PageReusePolicy.WeakSingleton);
        public PageRuleBuilder<T> Transient() => Cache(PageReusePolicy.Transient);

        public PageRuleBuilder<T> LoadMode(NavigationLoadMode mode)
        {
            _builder.Register<T>(d => d.LoadMode = mode);
            return this;
        }

        /// <summary>
        /// Idle timeout, in seconds, for the idle page. Overrides a <c>[PageTimeout]</c>
        /// attribute and the global <c>UseIdleTimeout(ms)</c>. Only valid on the idle
        /// page; the bootstrap throws if the page is not the idle page.
        /// </summary>
        public PageRuleBuilder<T> IdleTimeout(int seconds)
        {
            if (seconds <= 0)
                throw new System.ArgumentOutOfRangeException(
                    nameof(seconds), seconds, "Idle timeout must be greater than zero seconds.");

            _builder.Register<T>(d => d.IdleTimeoutSeconds = seconds);
            return this;
        }

        public PageRuleBuilder<T> Tag(string tag)
        {
            _builder.Register<T>(d => d.AddTag(tag));
            return this;
        }
    }
}