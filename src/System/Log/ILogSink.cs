using System;

namespace GameLoop
{
    public enum LogLevel { Debug, Info, Warn, Error }

    public interface ILogSink
    {
        void Write(LogLevel level, string source, string message, object data);
    }
}
