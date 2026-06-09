namespace NekoLib.Core.Diagnostics
{
    public interface ILogSink
    {
        void Write(LogEntry entry);
    }
}
