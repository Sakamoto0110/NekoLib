using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace NekoLib.Navigation.Metadata.Attributes 
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class PageTimeoutAttribute : Attribute
    {
        public PageTimeoutPolicy Policy { get; }

        public PageTimeoutAttribute(PageTimeoutPolicy policy)
        {
            Policy = policy;
        }
    }
}
