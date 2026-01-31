namespace NekoLib.Data.Gateway
{
    public interface IDatabaseGateway :
        IDqlGateway,
        IDqlStreamingGateway,
        IDmlGateway,
        ITclGateway
    {
    }
}