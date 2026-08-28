using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;

namespace NekoLib.Navigation.Bootstrap
{
    /// <summary>
    /// Fluent entry point for applying manual metadata overrides to registered
    /// page types during bootstrap composition.
    /// </summary>
    public sealed class PageBuilderConfigurator
    {
        private readonly PageMetadataBuilder _builder;

        internal PageBuilderConfigurator(PageMetadataBuilder builder)
        {
            _builder = builder ?? throw new System.ArgumentNullException(nameof(builder));
        }

        /// <summary>Selects a page type for fluent metadata configuration.</summary>
        /// <typeparam name="T">Concrete page type to configure.</typeparam>
        /// <returns>A rule builder that records overrides for <typeparamref name="T"/>.</returns>
        public PageRuleBuilder<T> Page<T>() where T : IPageView
            => new PageRuleBuilder<T>(_builder);
    }

    /// <summary>
    /// Applies manual metadata overrides for one page type. Later calls for the
    /// same type are composed in call order and override attribute-derived values.
    /// </summary>
    /// <typeparam name="T">Concrete page type being configured.</typeparam>
    public sealed class PageRuleBuilder<T> where T : IPageView
    {
        private readonly PageMetadataBuilder _builder;

        internal PageRuleBuilder(PageMetadataBuilder builder)
        {
            _builder = builder;
        }

        /// <summary>Marks the page as the context's idle page.</summary>
        /// <returns>This builder for further configuration.</returns>
        public PageRuleBuilder<T> AsIdle()
        {
            _builder.Register<T>(d => d.Role = PageRole.Idle);
            return this;
        }

        /// <summary>Sets the registry name used for name-based lookup and diagnostics.</summary>
        /// <param name="name">Non-empty page name validated when the descriptor is built.</param>
        /// <returns>This builder for further configuration.</returns>
        public PageRuleBuilder<T> Named(string name)
        {
            _builder.Register<T>(d => d.Name = name);
            return this;
        }

        /// <summary>Sets the instance reuse policy for the page.</summary>
        /// <param name="policy">Supported reuse policy validated when the descriptor is built.</param>
        /// <returns>This builder for further configuration.</returns>
        public PageRuleBuilder<T> Cache(PageReusePolicy policy)
        {
            _builder.Register<T>(d => d.ReusePolicy = policy);
            return this;
        }

        /// <summary>Uses one strongly held instance for the context lifetime.</summary>
        /// <returns>This builder for further configuration.</returns>
        public PageRuleBuilder<T> StrongSingleton() => Cache(PageReusePolicy.StrongSingleton);

        /// <summary>Reuses the instance while it remains alive through a weak reference.</summary>
        /// <returns>This builder for further configuration.</returns>
        public PageRuleBuilder<T> WeakSingleton() => Cache(PageReusePolicy.WeakSingleton);

        /// <summary>Creates a new instance for each navigation and disposes it on leave.</summary>
        /// <returns>This builder for further configuration.</returns>
        public PageRuleBuilder<T> Transient() => Cache(PageReusePolicy.Transient);

        /// <summary>Sets when the page becomes visible relative to loading.</summary>
        /// <param name="mode">Supported load mode validated when the descriptor is built.</param>
        /// <returns>This builder for further configuration.</returns>
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
        /// <param name="seconds">Positive inactivity timeout in seconds.</param>
        /// <returns>This builder for further configuration.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// <paramref name="seconds"/> is not positive.
        /// </exception>
        public PageRuleBuilder<T> IdleTimeout(int seconds)
        {
            if (seconds <= 0)
                throw new System.ArgumentOutOfRangeException(
                    nameof(seconds), seconds, "Idle timeout must be greater than zero seconds.");

            _builder.Register<T>(d => d.IdleTimeoutSeconds = seconds);
            return this;
        }

        /// <summary>Adds a classification tag to the page descriptor.</summary>
        /// <param name="tag">Non-empty tag validated when the descriptor is built.</param>
        /// <returns>This builder for further configuration.</returns>
        public PageRuleBuilder<T> Tag(string tag)
        {
            _builder.Register<T>(d => d.AddTag(tag));
            return this;
        }
    }
}
