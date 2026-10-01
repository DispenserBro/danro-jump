using System;
using System.IO;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DanroJump.Debugging
{
    /// <summary>
    /// Логгер для записи сообщений в файл. Совместим с CompositeLogger.
    /// </summary>
    public class FileLogger : ILogger, IDisposable
    {
        public ILogHandler logHandler { get; set; }
        public LogType filterLogType { get; set; }

        public bool logEnabled { get; set; } = true;

        private readonly string _filePath;
        private readonly object _lock = new object();
        private StreamWriter _writer;
        private bool _disposed;
        private bool _footerWritten;

        /// <summary>
        /// Создает файловый логгер с указанным путём к файлу.
        /// </summary>
        /// <param name="filePath">Полный путь к файлу лога. Если null, используется Application.persistentDataPath/game.log</param>
        /// <param name="append">Если true, добавляет к существующему файлу, иначе перезаписывает</param>
        public FileLogger(string filePath = null, bool append = true)
        {
            _filePath = filePath ?? Path.Combine(Application.persistentDataPath, "game.log");
            filterLogType = LogType.Exception;

            try
            {
                // Создаем директорию если её нет
                string directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Открываем файл для записи
                _writer = new StreamWriter(_filePath, append, Encoding.UTF8)
                {
                    AutoFlush = true // Автоматически сбрасывать буфер для надёжности
                };

                // Записываем заголовок
                WriteHeader();
                
                // Подписываемся на завершение приложения для гарантированной записи
                Application.quitting += OnApplicationQuitting;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FileLogger] Не удалось открыть файл лога: {_filePath}\n{ex}");
            }
        }

        public string FilePath => _filePath;

        private void WriteHeader()
        {
            if (_writer != null)
            {
                _writer.WriteLine("================================================================================");
                _writer.WriteLine($"Новая сессия логирования начата: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                _writer.WriteLine($"Unity версия: {Application.unityVersion}");
                _writer.WriteLine($"Платформа: {Application.platform}");
                _writer.WriteLine($"Путь к логу: {_filePath}");
                _writer.WriteLine("================================================================================");
                _writer.WriteLine();
            }
        }

        public bool IsLogTypeAllowed(LogType logType)
        {
            return logEnabled && logType <= filterLogType;
        }

        private void WriteToFile(LogType logType, string message)
        {
            if (!logEnabled || _disposed || _writer == null)
                return;

            if (!IsLogTypeAllowed(logType))
                return;

            lock (_lock)
            {
                try
                {
                    string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                    string logTypeStr = GetLogTypeString(logType);
                    _writer.WriteLine($"[{timestamp}] [{logTypeStr}] {message}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[FileLogger] Ошибка записи в файл: {ex.Message}");
                }
            }
        }

        private string GetLogTypeString(LogType logType)
        {
            return logType switch
            {
                LogType.Error => "ERROR",
                LogType.Assert => "ASSERT",
                LogType.Warning => "WARNING",
                LogType.Log => "INFO",
                LogType.Exception => "EXCEPTION",
                _ => "UNKNOWN"
            };
        }

        private string FormatMessage(object message, Object context = null)
        {
            string msg = message?.ToString() ?? "null";
            if (context != null)
            {
                msg += $" (Context: {context.name})";
            }

            return msg;
        }

        private string FormatMessage(string tag, object message, Object context = null)
        {
            string msg = $"[{tag}] {message?.ToString() ?? "null"}";
            if (context != null)
            {
                msg += $" (Context: {context.name})";
            }

            return msg;
        }

#region ILogger Implementation

        public void Log(LogType logType, object message)
        {
            WriteToFile(logType, FormatMessage(message));
        }

        public void Log(LogType logType, object message, Object context)
        {
            WriteToFile(logType, FormatMessage(message, context));
        }

        public void Log(LogType logType, string tag, object message)
        {
            WriteToFile(logType, FormatMessage(tag, message));
        }

        public void Log(LogType logType, string tag, object message, Object context)
        {
            WriteToFile(logType, FormatMessage(tag, message, context));
        }

        public void Log(object message)
        {
            WriteToFile(LogType.Log, FormatMessage(message));
        }

        public void Log(string tag, object message)
        {
            WriteToFile(LogType.Log, FormatMessage(tag, message));
        }

        public void Log(string tag, object message, Object context)
        {
            WriteToFile(LogType.Log, FormatMessage(tag, message, context));
        }

        public void LogWarning(string tag, object message)
        {
            WriteToFile(LogType.Warning, FormatMessage(tag, message));
        }

        public void LogWarning(string tag, object message, Object context)
        {
            WriteToFile(LogType.Warning, FormatMessage(tag, message, context));
        }

        public void LogError(string tag, object message)
        {
            WriteToFile(LogType.Error, FormatMessage(tag, message));
        }

        public void LogError(string tag, object message, Object context)
        {
            WriteToFile(LogType.Error, FormatMessage(tag, message, context));
        }

        public void LogFormat(LogType logType, string format, params object[] args)
        {
            try
            {
                string message = string.Format(format, args);
                WriteToFile(logType, message);
            }
            catch (FormatException ex)
            {
                WriteToFile(LogType.Error, $"Ошибка форматирования: {ex.Message}");
            }
        }

        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            try
            {
                string message = string.Format(format, args);
                WriteToFile(logType, FormatMessage(message, context));
            }
            catch (FormatException ex)
            {
                WriteToFile(LogType.Error, $"Ошибка форматирования: {ex.Message}");
            }
        }

        public void LogException(Exception exception)
        {
            if (exception == null)
                return;

            WriteToFile(LogType.Exception, $"{exception.GetType().Name}: {exception.Message}");
            WriteToFile(LogType.Exception, $"StackTrace:\n{exception.StackTrace}");

            if (exception.InnerException != null)
            {
                WriteToFile(LogType.Exception,
                    $"Inner Exception: {exception.InnerException.GetType().Name}: {exception.InnerException.Message}");
            }
        }

        public void LogException(Exception exception, Object context)
        {
            if (exception == null)
                return;

            string contextInfo = context != null ? $" (Context: {context.name})" : "";
            WriteToFile(LogType.Exception, $"{exception.GetType().Name}: {exception.Message}{contextInfo}");
            WriteToFile(LogType.Exception, $"StackTrace:\n{exception.StackTrace}");

            if (exception.InnerException != null)
            {
                WriteToFile(LogType.Exception,
                    $"Inner Exception: {exception.InnerException.GetType().Name}: {exception.InnerException.Message}");
            }
        }

#endregion

        /// <summary>
        /// Обработчик для Application.logMessageReceived — перенаправляет все Debug.Log в файл.
        /// </summary>
        public void HandleUnityLog(string logString, string stackTrace, LogType type)
        {
            if (type == LogType.Exception)
            {
                WriteToFile(type, $"{logString}\n{stackTrace}");
            }
            else
            {
                WriteToFile(type, logString);
            }
        }

        private void OnApplicationQuitting()
        {
            WriteFooter();
            Dispose();
        }

        private void WriteFooter()
        {
            if (_footerWritten || _disposed || _writer == null) return;

            lock (_lock)
            {
                if (_footerWritten) return;
                _footerWritten = true;

                try
                {
                    _writer.WriteLine();
                    _writer.WriteLine("================================================================================");
                    _writer.WriteLine($"Сессия логирования завершена: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    _writer.WriteLine("================================================================================");
                    _writer.Flush();
                }
                catch (ObjectDisposedException) { }
                catch (Exception ex)
                {
                    Debug.LogError($"[FileLogger] Ошибка при записи footer: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Принудительно сбрасывает буфер на диск
        /// </summary>
        public void Flush()
        {
            lock (_lock)
            {
                try
                {
                    _writer?.Flush();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[FileLogger] Ошибка при сбросе буфера: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Очищает файл лога
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                try
                {
                    _writer?.Close();
                    File.WriteAllText(_filePath, string.Empty);
                    _writer = new StreamWriter(_filePath, true, Encoding.UTF8) { AutoFlush = true };
                    WriteHeader();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[FileLogger] Ошибка при очистке файла: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            lock (_lock)
            {
                if (_disposed)
                    return;

                try
                {
                    // Гарантированно записываем footer
                    WriteFooter();

                    if (disposing)
                    {
                        Application.quitting -= OnApplicationQuitting;
                    }

                    if (_writer != null)
                    {

                        _writer.Dispose();
                        _writer = null;
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Поток уже закрыт — игнорируем.
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[FileLogger] Ошибка при закрытии файла: {ex.Message}");
                }
                finally
                {
                    _disposed = true;
                }
            }
        }

        ~FileLogger()
        {
            Dispose(false);
        }
    }
}
