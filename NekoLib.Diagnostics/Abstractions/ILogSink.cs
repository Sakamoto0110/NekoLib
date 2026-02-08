namespace NekoLib.Diagnostics.Abstractions
{
    public interface ILogSink
    {
        void Write(LogEntry entry);
    }
}
