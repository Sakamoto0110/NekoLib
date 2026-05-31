namespace NekoLib.Data.Gateway
{
#if NET6_0_OR_GREATER
    public interface IDatabaseGateway :
        IDqlGateway,
        IDqlStreamingGateway,
        IDmlGateway,
        ITclGateway
    {
    }
#else
    public interface IDatabaseGateway :
        IDqlGateway,
        IDmlGateway,
        ITclGateway
    {
    }
#endif
}
