using System.Threading;
using System.Threading.Tasks;

namespace NekoLib.Data.Gateway
{
    /// <summary>Provides the explicit session boundary used for multi-operation transactions.</summary>
    public interface ITclGateway
    {
        /// <summary>Creates and opens a session connection owned by the returned session.</summary>
        /// <param name="ct">Cancellation observed while opening the connection.</param>
        /// <returns>The open session affiliated with this gateway context.</returns>
        Task<DbSession> OpenSessionAsync(
            CancellationToken ct = default);
    }
}
