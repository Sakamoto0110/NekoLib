using NekoLib.Navigation.Contracts.Guards;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Runtime.Guards {
    /// <summary>
    /// Denies navigation unless the session is authenticated. The denial carries a
    /// stable reason so <c>GuardDeniedEvent</c>, Logging, and Inspection can explain
    /// the refusal, matching the reasons the role and permission guards already
    /// report.
    /// </summary>
    public sealed class RequireAuthenticatedGuard : IGuard
    {
        public Task<GuardResult> EvaluateAsync(GuardContext context)
        {
            if (context?.User?.IsAuthenticated == true)
                return Task.FromResult(GuardResult.Allow());

            return Task.FromResult(
                GuardResult.Deny("Authentication required."));
        }
    }
}
