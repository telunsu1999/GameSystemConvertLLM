using System;

namespace GameLoop
{
    public static class Logger
    {
        private static ILogSink _sink;

        public static void SetSink(ILogSink sink) => _sink = sink;

        public static void Debug(string source, string message, object data = null)
            => _sink?.Write(LogLevel.Debug, source, message, data);

        public static void Info(string source, string message, object data = null)
            => _sink?.Write(LogLevel.Info, source, message, data);

        public static void Warn(string source, string message, object data = null)
            => _sink?.Write(LogLevel.Warn, source, message, data);

        public static void Error(string source, string message, object data = null)
            => _sink?.Write(LogLevel.Error, source, message, data);
    }
}
