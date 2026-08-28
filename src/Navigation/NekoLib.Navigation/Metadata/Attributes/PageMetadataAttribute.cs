using System;

namespace NekoLib.Navigation.Metadata.Attributes
{

    /// <summary>Declares a page's role, optional registry name, and classification tags.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class PageMetadataAttribute : Attribute
    {
        /// <summary>
        /// Logical role of the page.
        /// </summary>
        public PageRole Role { get; set; } = PageRole.Normal;

        /// <summary>
        /// Optional explicit name override.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Optional classification tags.
        /// </summary>
        public string[]? Tags { get; set; }
    }
}
