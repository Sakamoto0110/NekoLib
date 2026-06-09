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

        public PagePresentationMode Presentation { get; set; }
            = PagePresentationMode.Replace;

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

        public PageDescriptorBuilder(Type type)
        {
            PageType = type;
            Name = type.Name;
        }

        public void AddTag(string tag)
        {
            if (!string.IsNullOrWhiteSpace(tag))
                _tags.Add(tag);
        }

        public void AddGuard(IGuard guard)
        {
            if (guard != null)
                _guards.Add(guard);
        }

        public PageDescriptor Build()
        {
            IGuard? combined = null;

            if (_guards.Count == 1)
                combined = _guards[0];
            else if (_guards.Count > 1)
                combined = new AndGuard(_guards.ToArray());

            return new PageDescriptor(
                PageType,
                Name,
                Role,
                Presentation,
                ReusePolicy,
                IdleTimeoutSeconds,
                LoadMode,
                AllowAnonymous,
                _tags.AsReadOnly(),
                combined ,
                KeepAttachedWhenHidden
            );
        }
    }
}