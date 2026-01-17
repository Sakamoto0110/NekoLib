using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;
using NekoLib.Navigation.Runtime.Registry;
using System;

namespace NekoLib.Navigation.Bootstrap
{
    /// <summary>
    /// Fluent config helper used by PageNavBootstrap.ConfigurePages(...).
    /// Works on top of PageRegistry (attributes already applied).
    /// </summary>
    public sealed class PageRegistryConfigurator
    {
        public PageRule<T> Page<T>() where T : IPageView
            => new PageRule<T>();
    }

    /// <summary>
    /// Per-page tweak builder. Each call modifies the registered PageDescriptor.
    /// </summary>
    public sealed class PageRule<T> where T : IPageView
    {
        private readonly Type _pageType = typeof(T);

        private PageDescriptor Get()
        {
            PageDescriptor d;
            if(!PageRegistry.TryGetDescriptor(_pageType, out d))
                throw new InvalidOperationException("Page not registered: " + _pageType.FullName);
            return d;
        }

        public PageRule<T> Named(string name)
        {
            if(string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
            Get().Name = name;
            return this;
        }

        public PageRule<T> Kind(PageKind kind)
        {
            Get().Kind = kind;
            return this;
        }

        public PageRule<T> AsHome()
            => Kind(PageKind.Home);

        public PageRule<T> AsModal()
            => Kind(PageKind.Modal);

        public PageRule<T> AsPopup()
            => Kind(PageKind.Popup);

        public PageRule<T> Cache(PageCachePolicy policy)
        {
            Get().CachePolicy = policy;
            return this;
        }

        public PageRule<T> DisabledCache()
            => Cache(PageCachePolicy.Disabled);

        public PageRule<T> WeakSingleton()
            => Cache(PageCachePolicy.WeakSingleton);

        public PageRule<T> StrongSingleton()
            => Cache(PageCachePolicy.StrongSingleton);

        public PageRule<T> Stackable()
            => Cache(PageCachePolicy.Stackable);

        public PageRule<T> Tag(string tag)
        {
            if(string.IsNullOrWhiteSpace(tag)) throw new ArgumentNullException(nameof(tag));
            Get().Tags.Add(tag);
            return this;
        }

        public PageRule<T> Timeout(PageTimeoutBehavior behavior)
        {
            Get().Timeout = behavior;
            return this;
        }

        public PageRule<T> LoadMode(NavigationLoadMode mode)
        {
            Get().WaitCompletionBeforeShow = mode;
            return this;
        }
    }
}
