namespace NekoLib.Data.Gateway
{
#if NET6_0_OR_GREATER
    /// <summary>
    /// Combines the query, command, transaction, and asynchronous-streaming
    /// capability views exposed by a database gateway.
    /// </summary>
    public interface IDatabaseGateway :
        IDqlGateway,
        IDqlStreamingGateway,
        IDmlGateway,
        ITclGateway
    {
    }
#else
    /// <summary>
    /// Combines the query, command, and transaction capability views exposed by
    /// a database gateway on the .NET Framework target.
    /// </summary>
    public interface IDatabaseGateway :
        IDqlGateway,
        IDmlGateway,
        ITclGateway
    {
    }
#endif
}
