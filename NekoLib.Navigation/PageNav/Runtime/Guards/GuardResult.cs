using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Runtime.Guards {
    public sealed class GuardResult
    {
        public bool Allowed { get; }
        public Type RedirectPage { get; }
        public string Reason { get; }

        private GuardResult(bool allowed, Type redirect, string reason)
        {
            Allowed = allowed;
            RedirectPage = RedirectPage != null? RedirectPage: redirect;
            Reason = reason;
        }

        public static GuardResult Allow()
            => new GuardResult(true, null, null);

        public static GuardResult Deny(string reason = null)
            => new GuardResult(false, null, reason);

        public static GuardResult Redirect<TPage>(string reason = null)
            => new GuardResult(false, typeof(TPage), reason);
        public static GuardResult Redirect(Type TPage, string reason = null)
           => new GuardResult(false, TPage, null);
    }
}
 
