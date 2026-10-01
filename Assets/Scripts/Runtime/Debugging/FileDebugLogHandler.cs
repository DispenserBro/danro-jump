using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using Zenject;

namespace DanroJump.Debugging
{
    /// <summary>
    /// Дополнительный обработчик, который сохраняет отладочные сообщения в файл.
    /// </summary>
    public sealed class FileDebugLogHandler : IDebugLogHandler, IInitializable, IDisposable
    {
        private readonly IDebugLogService debugLogService;
        private readonly DebugFileLogOptions options;
        private readonly object writerLock = new();

        private StreamWriter writer;
        private int entriesSinceFlush;
        private bool isFaulted;

        public FileDebugLogHandler(IDebugLogService debugLogService, DebugFileLogOptions options)
        {
            this.debugLogService = debugLogService;
            this.options = options;
        }

        public string FilePath { get; private set; } = "";

        public void Initialize()
        {
            if (!options.IsEnabled)
            {
                return;
            }

            try
            {
                FilePath = options.ResolveFilePath();
                string directoryPath = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                var stream = new FileStream(
                    FilePath,
                    options.AppendToFile ? FileMode.Append : FileMode.Create,
                    FileAccess.Write,
                    FileShare.ReadWrite);

                writer = new StreamWriter(stream, new UTF8Encoding(false));
                if (options.WriteSessionHeader)
                {
                    writer.WriteLine($"# Danro Jump log session started {DateTime.UtcNow:O}");
                    writer.Flush();
                }

                debugLogService.AddHandler(this);
                Debug.Log($"[Debug] File log handler writes to: {FilePath}");
            }
            catch (Exception exception)
            {
                isFaulted = true;
                Debug.LogWarning($"[Debug] Failed to initialize file log handler: {exception.Message}");
            }
        }

        public void Dispose()
        {
            debugLogService.RemoveHandler(this);

            lock (writerLock)
            {
                writer?.Flush();
                writer?.Dispose();
                writer = null;
            }
        }

        public void Handle(DebugLogEntry entry)
        {
            if (isFaulted)
            {
                return;
            }

            try
            {
                lock (writerLock)
                {
                    if (writer == null)
                    {
                        return;
                    }

                    writer.WriteLine(FormatEntry(entry));
                    entriesSinceFlush++;

                    if (entriesSinceFlush >= options.FlushEveryEntries)
                    {
                        writer.Flush();
                        entriesSinceFlush = 0;
                    }
                }
            }
            catch (Exception exception)
            {
                isFaulted = true;
                Debug.LogWarning($"[Debug] File log handler disabled after write failure: {exception.Message}");
            }
        }

        private static string FormatEntry(DebugLogEntry entry)
        {
            string timestamp = entry.TimestampUtc.ToString("O", CultureInfo.InvariantCulture);
            string contextName = entry.Context == null ? "" : entry.Context.name;
            return string.IsNullOrEmpty(contextName)
                ? $"{timestamp} [{entry.Level}] [{entry.Channel}] {Normalize(entry.Message)}"
                : $"{timestamp} [{entry.Level}] [{entry.Channel}] {Normalize(entry.Message)} | context={Normalize(contextName)}";
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrEmpty(value)
                ? ""
                : value.Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }
}
