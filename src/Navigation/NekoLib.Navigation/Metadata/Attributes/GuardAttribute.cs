using NekoLib.Navigation.Contracts.Guards;
using System;
using NekoLib.Navigation.Contracts.Pages;
using NekoLib.Navigation.Runtime.Guards;

namespace NekoLib.Navigation.Metadata.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public abstract class GuardAttribute : Attribute
    {
        private Type? _redirectTo;

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
