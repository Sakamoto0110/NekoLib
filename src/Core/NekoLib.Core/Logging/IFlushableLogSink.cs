namespace NekoLib.Core.Logging
{
    public interface IFlushableLogSink : ILogSink
    {
        void Flush();
    }
}
