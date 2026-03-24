using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Metadata.Attributes
{

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class PageMetadataAttribute : Attribute
    {
        /// <summary>
        /// Logical role of the page.
        /// </summary>
        public PageRole Role { get; set; } = PageRole.Normal;

        /// <summary>
        /// Presentation behavior relative to stack.
        /// </summary>
        public PagePresentationMode Presentation { get; set; } = PagePresentationMode.Replace;

        /// <summary>
        /// Optional explicit name override.
        /// </summary>
        public string  Name { get; set; }

        /// <summary>
        /// Optional classification tags.
        /// </summary>
        public string[]  Tags { get; set; }
    }
}