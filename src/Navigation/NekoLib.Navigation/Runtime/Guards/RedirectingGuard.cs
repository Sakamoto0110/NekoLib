using System;
using System.Threading.Tasks;
using NekoLib.Navigation.Contracts.Guards;

namespace NekoLib.Navigation.Runtime.Guards
{
    internal sealed class RedirectingGuard : IGuard
    {
        private readonly IGuard _inner;
        private readonly Type _redirectPage;

        internal RedirectingGuard(IGuard inner, Type redirectPage)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _redirectPage = redirectPage ?? throw new ArgumentNullException(nameof(redirectPage));
        }

        public async Task<GuardResult> EvaluateAsync(GuardContext context)
        {
            var result = await _inner.EvaluateAsync(context);
            if (result.Allowed || result.RedirectPage != null)
                return result;

            return GuardResult.Redirect(_redirectPage, result.Reason);
        }
    }
}
