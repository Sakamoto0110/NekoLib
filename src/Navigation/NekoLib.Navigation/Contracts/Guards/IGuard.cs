using System.Threading.Tasks;

namespace NekoLib.Navigation.Contracts.Guards
{
    public interface IGuard
    {
        Task<GuardResult> EvaluateAsync(GuardContext context);
    }
}

