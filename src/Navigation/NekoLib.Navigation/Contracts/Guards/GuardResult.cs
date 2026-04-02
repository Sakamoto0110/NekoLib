using System;

namespace NekoLib.Navigation.Contracts.Guards {
    public sealed class GuardResult
    {
        public bool Allowed { get; }

        /// <summary>
        /// If not null, navigation should redirect to this page.
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

        public static GuardResult Allow()
            => new GuardResult(true, null, null);

        public static GuardResult Deny(string reason = null)
            => new GuardResult(false, null, reason);

        public static GuardResult Redirect<TPage>(string reason = null)
            => new GuardResult(false, typeof(TPage), reason);

        public static GuardResult Redirect(Type pageType, string reason = null)
            => new GuardResult(false, pageType, reason);
    }
}
 
