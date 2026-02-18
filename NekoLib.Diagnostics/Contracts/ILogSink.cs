namespace NekoLib.Diagnostics.Contracts

{
    public interface ILogSink
    {
        void Write(LogEntry entry);
    }
}
