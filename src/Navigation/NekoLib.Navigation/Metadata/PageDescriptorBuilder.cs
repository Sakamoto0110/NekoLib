using NekoLib.Navigation.Contracts.Guards;
using NekoLib.Navigation.Runtime.Guards;
using System;
using System.Collections.Generic;

namespace NekoLib.Navigation.Metadata
{
    public sealed class PageDescriptorBuilder
    {
        public Type PageType { get; }

        public string Name { get; set; }

        public PageRole Role { get; set; } = PageRole.Normal;

        public PageReusePolicy ReusePolicy { get; set; }
            = PageReusePolicy.Transient;

        /// <summary>
        /// Idle timeout in seconds for the idle page; <c>null</c> means "not declared".
        /// Set by <c>[PageTimeout]</c> (attribute phase) or by the DSL
        /// <c>.IdleTimeout(seconds)</c> (manual phase, which overrides the attribute).
        /// </summary>
        public int? IdleTimeoutSeconds { get; set; }

        public NavigationLoadMode LoadMode { get; set; }
            = NavigationLoadMode.ShowImmediately;

        public bool AllowAnonymous { get; set; } = false;

        public bool KeepAttachedWhenHidden { get; set; } = false;


        private readonly List<string> _tags = new();
        private readonly List<IGuard> _guards = new();

        internal PageDescriptorBuilder(Type type)
        {
            PageType = type ?? throw new ArgumentNullException(nameof(type));
            Name = type.Name;
        }

        public void AddTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                throw new ArgumentException("A page tag cannot be null, empty, or whitespace.", nameof(tag));

            _tags.Add(tag);
        }

        public void AddGuard(IGuard guard)
        {
            if (guard == null)
                throw new ArgumentNullException(nameof(guard));

            _guards.Add(guard);
        }

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
