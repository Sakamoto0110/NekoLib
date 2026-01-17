namespace NekoLib.Diagnostics
{
    public interface ILogSink
    {
        void Write(LogEntry entry);
    }
}
