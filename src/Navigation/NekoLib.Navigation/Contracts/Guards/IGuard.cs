using System.Threading.Tasks;

namespace NekoLib.Navigation.Contracts.Guards
{
    /// <summary>
    /// Navigation authorization hook evaluated before a target page is shown.
    /// Guards may allow, deny, or redirect the navigation.
    /// </summary>
    public interface IGuard
    {
        /// <summary>Evaluate access for the target page described by <paramref name="context"/>.</summary>
        Task<GuardResult> EvaluateAsync(GuardContext context);
    }
}

