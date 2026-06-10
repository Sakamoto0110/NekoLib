using System;

namespace NekoLib.Navigation.Contracts.Guards {
    /// <summary>
    /// Result returned by a navigation guard. A result either allows navigation,
    /// denies it, or denies and supplies a redirect page.
    /// </summary>
    public sealed class GuardResult
    {
        /// <summary>True when navigation may continue to the requested target page.</summary>
        public bool Allowed { get; }

        /// <summary>
        /// If not null, the runtime should navigate to this page instead.
        /// </summary>
        public Type RedirectPage { get; }

        /// <summary>
        /// Optional diagnostic reason.
        /// </summary>
        public string Reason { get; }

        private GuardResult(bool allowed, Type redirectPage, string reason)
        {
            Allowed = allowed;
            RedirectPage = redirectPage;
            Reason = reason;
        }

        /// <summary>Create an allow result.</summary>
        public static GuardResult Allow()
            => new GuardResult(true, null, null);

        /// <summary>Create a deny result with an optional diagnostic reason.</summary>
        public static GuardResult Deny(string reason = null)
            => new GuardResult(false, null, reason);

        /// <summary>Create a redirect result to <typeparamref name="TPage"/>.</summary>
        public static GuardResult Redirect<TPage>(string reason = null)
            => new GuardResult(false, typeof(TPage), reason);

        /// <summary>Create a redirect result to a runtime page type.</summary>
        public static GuardResult Redirect(Type pageType, string reason = null)
            => new GuardResult(false, pageType, reason);
    }
}
 
