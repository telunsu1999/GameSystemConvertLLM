using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace GameLoop
{
    public class FileLogSink : ILogSink, IDisposable
    {
        private readonly StreamWriter _writer;
        private readonly object _lock = new object();

        public FileLogSink(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            _writer = new StreamWriter(filePath, append: true) { AutoFlush = true };
        }

        public void Write(LogLevel level, string source, string message, object data)
        {
            var entry = new Dictionary<string, object>
            {
                ["ts"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                ["level"] = level.ToString().ToUpper(),
                ["src"] = source,
                ["msg"] = message
            };
            if (data != null) entry["data"] = data;

            var line = JsonConvert.SerializeObject(entry);
            lock (_lock) _writer.WriteLine(line);
        }

        public void Dispose() => _writer?.Dispose();
    }
}
