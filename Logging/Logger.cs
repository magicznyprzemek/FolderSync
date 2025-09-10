using System.Text;

namespace FolderSync.Logging
{
    public sealed class Logger : IDisposable
    {
        private readonly StreamWriter _writer;
        private readonly object _lock = new object();

        public Logger(string filePath)
        {
            var fs = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            _writer = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { AutoFlush = true };
        }

        public void Info(string message) => Log("INFO ", message);
        public void Error(string message) => Log("ERROR", message);

        private void Log(string level, string message)
        {
            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {message}";
            lock (_lock)
            {
                Console.WriteLine(line);
                _writer.WriteLine(line);
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _writer.Dispose();
            }
        }
    }
}