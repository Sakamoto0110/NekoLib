using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public PageRuleBuilder<T> AsHome()
        {
            _builder.Register<T>(d => d.Role = PageRole.Home);
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

        public PageRuleBuilder<T> AsModal()
        {
            _builder.Register<T>(d =>
                d.Presentation = PagePresentationMode.ModalOverlay);
            return this;
        }

        public PageRuleBuilder<T> Tag(string tag)
        {
            _builder.Register<T>(d => d.AddTag(tag));
            return this;
        }
    }
}