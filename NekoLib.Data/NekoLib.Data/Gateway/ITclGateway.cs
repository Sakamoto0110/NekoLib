using System.Threading;
using System.Threading.Tasks;

namespace NekoLib.Data.Gateway
{
    public interface ITclGateway
    {
        Task<DbSession> OpenSessionAsync(
            CancellationToken ct = default);
    }
}
