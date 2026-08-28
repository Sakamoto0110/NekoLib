using NekoLib.Navigation.Contracts.Guards;
using NekoLib.Navigation.Runtime.Guards;
using System;
using System.Collections.Generic;

namespace NekoLib.Navigation.Metadata
{
    /// <summary>
    /// Mutable composition object used to construct one validated immutable
    /// <see cref="PageDescriptor"/>. Manual configuration runs after attributes.
    /// </summary>
    public sealed class PageDescriptorBuilder
    {
        /// <summary>Gets the concrete page type being described.</summary>
        public Type PageType { get; }

        /// <summary>Gets or sets the unique case-insensitive registry name.</summary>
        public string Name { get; set; }

        /// <summary>Gets or sets the page's logical role.</summary>
        public PageRole Role { get; set; } = PageRole.Normal;

        /// <summary>Gets or sets the page instance reuse policy.</summary>
        public PageReusePolicy ReusePolicy { get; set; }
            = PageReusePolicy.Transient;

        /// <summary>
        /// Idle timeout in seconds for the idle page; <c>null</c> means "not declared".
        /// Set by <c>[PageTimeout]</c> (attribute phase) or by the DSL
        /// <c>.IdleTimeout(seconds)</c> (manual phase, which overrides the attribute).
        /// </summary>
        public int? IdleTimeoutSeconds { get; set; }

        /// <summary>Gets or sets when the page becomes visible relative to loading.</summary>
        public NavigationLoadMode LoadMode { get; set; }
            = NavigationLoadMode.ShowImmediately;

        /// <summary>Gets or sets whether the implicit authentication guard is omitted.</summary>
        public bool AllowAnonymous { get; set; } = false;

        /// <summary>Gets or sets whether the host retains the native view while the page is hidden.</summary>
        public bool KeepAttachedWhenHidden { get; set; } = false;


        private readonly List<string> _tags = new();
        private readonly List<IGuard> _guards = new();

        internal PageDescriptorBuilder(Type type)
        {
            PageType = type ?? throw new ArgumentNullException(nameof(type));
            Name = type.Name;
        }

        /// <summary>Adds a classification tag to the descriptor.</summary>
        /// <param name="tag">Non-empty tag; duplicates are retained in insertion order.</param>
        /// <exception cref="ArgumentException"><paramref name="tag"/> is null, empty, or whitespace.</exception>
        public void AddTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                throw new ArgumentException("A page tag cannot be null, empty, or whitespace.", nameof(tag));

            _tags.Add(tag);
        }

        /// <summary>Adds a guard to the ordered conjunction evaluated for the page.</summary>
        /// <param name="guard">Guard instance retained by the resulting descriptor.</param>
        /// <exception cref="ArgumentNullException"><paramref name="guard"/> is <see langword="null"/>.</exception>
        public void AddGuard(IGuard guard)
        {
            if (guard == null)
                throw new ArgumentNullException(nameof(guard));

            _guards.Add(guard);
        }

        /// <summary>Validates the accumulated metadata and creates an immutable descriptor.</summary>
        /// <returns>The descriptor, with multiple guards composed in insertion order by <see cref="AndGuard"/>.</returns>
        /// <exception cref="InvalidOperationException">The page type or a configured enum, name, or idle timeout is invalid.</exception>
        public PageDescriptor Build()
        {
            if (!typeof(NekoLib.Navigation.Contracts.Pages.IPageView).IsAssignableFrom(PageType) || PageType.IsAbstract)
                throw new InvalidOperationException($"Not a valid concrete page: {PageType.FullName}");
            if (string.IsNullOrWhiteSpace(Name))
                throw new InvalidOperationException("A page descriptor must have a non-empty name.");
            if (!Enum.IsDefined(typeof(PageRole), Role))
                throw new InvalidOperationException($"Unsupported page role: {Role}.");
            if (!Enum.IsDefined(typeof(PageReusePolicy), ReusePolicy))
                throw new InvalidOperationException($"Unsupported page reuse policy: {ReusePolicy}.");
            if (!Enum.IsDefined(typeof(NavigationLoadMode), LoadMode))
                throw new InvalidOperationException($"Unsupported navigation load mode: {LoadMode}.");
            if (IdleTimeoutSeconds.HasValue && IdleTimeoutSeconds.Value <= 0)
                throw new InvalidOperationException("Idle timeout must be greater than zero seconds.");

            IGuard? combined = null;

            if (_guards.Count == 1)
                combined = _guards[0];
            else if (_guards.Count > 1)
                combined = new AndGuard(_guards.ToArray());

            return new PageDescriptor(
                PageType,
                Name,
                Role,
                ReusePolicy,
                IdleTimeoutSeconds,
                LoadMode,
                AllowAnonymous,
                new List<string>(_tags).AsReadOnly(),
                combined ,
                KeepAttachedWhenHidden
            );
        }
    }
}
