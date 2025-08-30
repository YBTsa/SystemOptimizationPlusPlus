using System.IO;
using System.Text;

namespace SOPP.Helpers
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// 日志文件辅助类，负责日志写入与日志文件管理
    /// </summary>
    public class LogHelper : IDisposable
    {
        private static readonly Lazy<LogHelper> _instance = new(() => new LogHelper());
        public static LogHelper Logger => _instance.Value;
        private string LogDirectory { get; }
        private readonly long _maxFileSize = 128 * 1024;
        private readonly Lock _lockObj = new();
        private string? _currentLogFile;
        private FileStream? _fileStream;
        private StreamWriter? _streamWriter;
        private long _currentFileSize = 0;
        private bool _isDisposed;
        private readonly List<string> _inactiveLogFiles = [];
        private readonly Lock _inactiveFilesLock = new();
        private LogHelper()
        {
            LogDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SOPP\\Logs", DateTime.Now.ToString("yyyy-MM-dd"));
            InitializeLogger();
        }

        // 异步初始化（避免构造函数同步等待）
        private void InitializeLogger()
        {
            lock (_lockObj)
            {
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }
            }

            lock (_lockObj)
            {
                _currentLogFile = GetUniqueLogFileName();
                InitializeNewFileResources();
                _currentFileSize = 0;
            }
        }
        private string GetUniqueLogFileName()
        {
            string datePart = DateTime.UtcNow.ToString("yyyy_MM_dd");
            string searchPattern = $"Log_{datePart}_*.log";

            // 读取现有文件并解析最大索引
            var existingFiles = Directory.EnumerateFiles(LogDirectory, searchPattern)
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .ToList();

            int maxIndex = 0;
            foreach (var file in existingFiles)
            {
                if (int.TryParse(file.Split('_').LastOrDefault(), out int index))
                {
                    maxIndex = Math.Max(maxIndex, index);
                }
            }

            // 生成新文件名
            for (int i = maxIndex + 1; i <= maxIndex + 10; i++) // 尝试10个连续索引
            {
                string candidate = Path.Combine(LogDirectory, $"Log_{datePart}_{i}.log");
                if (!File.Exists(candidate))
                    return candidate;
            }

            // 仍冲突则使用GUID确保唯一
            return Path.Combine(LogDirectory, $"Log_{datePart}_unique_{Guid.NewGuid():N}.log");
        }

        private void CheckAndSwitchFile(long nextEntrySize)
        {
            lock (_lockObj)
            {
                if (_isDisposed || string.IsNullOrEmpty(_currentLogFile)) return;

                // 达到容量阈值时切换文件
                if (_currentFileSize + nextEntrySize > _maxFileSize)
                {
                    string oldLogFile = _currentLogFile;
                    ReleaseOldFileResources();

                    _currentLogFile = GetUniqueLogFileName();
                    InitializeNewFileResources();
                    MarkAsInactive(oldLogFile);
                }
            }
        }

        private void ReleaseOldFileResources()
        {
            if (_streamWriter != null)
            {
                _streamWriter.Flush();
                _streamWriter.Dispose();
                _streamWriter = null;
            }

            if (_fileStream != null)
            {
                _fileStream.Dispose();
                _fileStream = null;
            }

            _currentFileSize = 0;
        }

        private void InitializeNewFileResources()
        {
            if (string.IsNullOrEmpty(_currentLogFile))
                throw new InvalidOperationException("新日志文件路径为空");

            try
            {
                _fileStream = new FileStream(
                    _currentLogFile,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.Read,
                    4096,
                    useAsync: true);
            }
            catch (IOException ex) when (ex.HResult == -2147024816) // 0x80070050: 文件已存在
            {
                // 重新生成文件名并再次尝试
                _currentLogFile = GetUniqueLogFileName();
                _fileStream = new FileStream(
                    _currentLogFile,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.Read,
                    4096,
                    useAsync: true);
            }

            // 禁用BOM，避免空文件有额外字节
            _streamWriter = new StreamWriter(_fileStream, new UTF8Encoding(false))
            {
                AutoFlush = true
            };
            _currentFileSize = 0;
        }

        private void MarkAsInactive(string oldFile)
        {
            lock (_inactiveFilesLock)
            {
                if (!_inactiveLogFiles.Contains(oldFile) && File.Exists(oldFile))
                {
                    _inactiveLogFiles.Add(oldFile);
                }
            }
        }

        public List<string> GetInactiveLogFiles()
        {
            lock (_inactiveFilesLock)
            {
                return [.. _inactiveLogFiles];
            }
        }

        public async Task WriteLogAsync(LogLevel level, string message)
        {
            if (string.IsNullOrEmpty(message))
                throw new ArgumentNullException(nameof(message));

            string timestamp = DateTime.UtcNow.ToString("yyyy:MM:dd HH:mm:ss.fff");
            string logEntry = $"{timestamp} [{level}] {message}{Environment.NewLine}";
            long entrySize = Encoding.UTF8.GetByteCount(logEntry);

            CheckAndSwitchFile(entrySize);

            StreamWriter writer;
            lock (_lockObj)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(LogHelper), "日志记录器已释放");
                if (_streamWriter == null || !_streamWriter.BaseStream.CanWrite)
                    throw new InvalidOperationException("日志写入器不可用");

                writer = _streamWriter;
            }

            try
            {
                await writer.WriteAsync(logEntry).ConfigureAwait(false);

                lock (_lockObj)
                {
                    _currentFileSize += entrySize;
                    if (_currentFileSize > _maxFileSize)
                    {
                        CheckAndSwitchFile(0);
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                lock (_lockObj)
                {
                    if (_isDisposed)
                        throw new ObjectDisposedException(nameof(LogHelper), "日志记录器已释放");
                    writer = _streamWriter;
                }
                await writer.WriteAsync(logEntry).ConfigureAwait(false);
                lock (_lockObj)
                {
                    _currentFileSize += entrySize;
                }
            }
        }

        public void WriteLog(LogLevel level, string message)
        {
            // 同步调用异步方法时使用安全上下文
            WriteLogAsync(level, message).GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            lock (_lockObj)
            {
                if (_isDisposed) return;

                if (disposing)
                {
                    _streamWriter?.Flush();
                    _streamWriter?.Dispose();
                    _fileStream?.Dispose();
                }

                _isDisposed = true;
                _currentLogFile = null;
                _streamWriter = null;
                _fileStream = null;
                _currentFileSize = 0;
            }
        }

        ~LogHelper()
        {
            Dispose(false);
        }
    }
}