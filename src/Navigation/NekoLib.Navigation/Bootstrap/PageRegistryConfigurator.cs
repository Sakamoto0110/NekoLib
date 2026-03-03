//using NekoLib.Navigation.Contracts.Pages;
//using NekoLib.Navigation.Metadata;
//using NekoLib.Navigation.Runtime.Registry;
//using System;

//namespace NekoLib.Navigation.Bootstrap
//{
//    public sealed class PageRegistryConfigurator
//    {
//        private PageRegistry _registry;
//        public PageRegistryConfigurator(PageRegistry registry) => _registry = registry ?? throw new ArgumentNullException(nameof(registry));
//        public PageRule<T> Page<T>( ) where T : IPageView
//            => new PageRule<T>(_registry);
//    }

//    /// <summary>
//    /// Per-page tweak builder. Each call modifies the registered PageDescriptor.
//    /// </summary>
//    public sealed class PageRule<T> where T : IPageView
//    {
//        private readonly Type _pageType = typeof(T);
//        private PageRegistry _registry;
//        public PageRule(PageRegistry registry)=>_registry = registry ?? throw new ArgumentNullException(nameof(registry)); 
//        private PageDescriptor Get()
//        {
//            PageDescriptor d;
//            if(!_registry.TryGetDescriptor(_pageType, out d))
//                throw new InvalidOperationException("Page not registered: " + _pageType.FullName);
//            return d;
//        }

//        public PageRule<T> Named(string name)
//        {
//            if(string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
//            Get().Name = name;
//            return this;
//        }

//        public PageRule<T> Kind(PageKind kind)
//        {
//            Get().Kind = kind;
//            return this;
//        }

//        public PageRule<T> AsHome()
//            => Kind(PageKind.Home);

//        public PageRule<T> AsModal()
//            => Kind(PageKind.Modal);

//        public PageRule<T> AsPopup()
//            => Kind(PageKind.Popup);

//        public PageRule<T> Cache(PageReusePolicy policy)
//        {
//            Get().ReusePolicy = policy;
//            return this;
//        }

//        public PageRule<T> DisabledCache()
//            => Cache(PageReusePolicy.Transient);

//        public PageRule<T> WeakSingleton()
//            => Cache(PageReusePolicy.Cached);

//        public PageRule<T> StrongSingleton()
//            => Cache(PageReusePolicy.Singleton);

     
//        public PageRule<T> Tag(string tag)
//        {
//            if(string.IsNullOrWhiteSpace(tag)) throw new ArgumentNullException(nameof(tag));
//            Get().Tags.Add(tag);
//            return this;
//        }

        

//        public PageRule<T> LoadMode(NavigationLoadMode mode)
//        {
//            Get().LoadMode = mode;
//            return this;
//        }
//    }
//}
