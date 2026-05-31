using NekoLib.Navigation.Contracts.Guards;
using System.Threading.Tasks;

namespace NekoLib.Navigation.Runtime.Guards {
    public sealed class RequireAuthenticatedGuard : IGuard
    {
        public async Task<GuardResult> EvaluateAsync(GuardContext context)
        {
            if (context.User?.IsAuthenticated == true)
                return GuardResult.Allow();

            return GuardResult.Deny();
        }
    }
}
