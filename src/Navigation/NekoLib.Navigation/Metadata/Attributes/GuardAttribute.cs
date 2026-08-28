using NekoLib.Navigation.Contracts.Guards;
using System;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Runtime.Guards;

namespace NekoLib.Navigation.Metadata.Attributes
{
    /// <summary>
    /// Base class for page attributes that create an <see cref="IGuard"/> during
    /// registry construction. Built-in attributes apply <see cref="RedirectTo"/>
    /// to their guards; a consumer-defined attribute must implement any redirect
    /// behavior in the guard it returns.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public abstract class GuardAttribute : Attribute
    {
        private Type? _redirectTo;

        /// <summary>
        /// Gets or sets an optional concrete <see cref="IPageView"/> redirect target.
        /// The assignment is validated immediately.
        /// </summary>
        public Type? RedirectTo
        {
            get => _redirectTo;
            set
            {
                if (value != null &&
                    (!typeof(IPageView).IsAssignableFrom(value) || value.IsAbstract))
                {
                    throw new ArgumentException(
                        "A guard redirect target must be a concrete IPageView type.",
                        nameof(value));
                }

                _redirectTo = value;
            }
        }

        /// <summary>Creates the guard represented by this attribute.</summary>
        /// <returns>The guard added to the page descriptor.</returns>
        public abstract IGuard CreateGuard();

        internal IGuard ApplyRedirect(IGuard guard)
        {
            if (guard == null)
                throw new ArgumentNullException(nameof(guard));
            if (RedirectTo == null)
                return guard;
            return new RedirectingGuard(guard, RedirectTo);
        }
    }

}
