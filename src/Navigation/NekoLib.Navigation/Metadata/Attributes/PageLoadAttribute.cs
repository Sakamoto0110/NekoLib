using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Metadata.Attributes
{

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class PageLoadAttribute : Attribute
    {
        public NavigationLoadMode Mode { get; }

        public PageLoadAttribute(NavigationLoadMode mode)
        {
            Mode = mode;
        }
    }
}