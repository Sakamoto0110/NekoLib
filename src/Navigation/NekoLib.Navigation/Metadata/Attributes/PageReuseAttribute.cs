using System;

namespace NekoLib.Navigation.Metadata.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class PageReuseAttribute : Attribute
    {
        public PageReusePolicy Policy { get; }

        public PageReuseAttribute(PageReusePolicy policy)
        {
            Policy = policy;
        }
    }
}