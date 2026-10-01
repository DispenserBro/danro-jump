using System.IO;
using UnityEngine;

namespace DanroJump.Debugging
{
    /// <summary>
    /// Настройки файлового обработчика отладочных сообщений.
    /// </summary>
    public readonly struct DebugFileLogOptions
    {
        private const string DefaultDirectory = "Logs";
        private const string DefaultFileName = "debug.log";

        public DebugFileLogOptions(
            bool isEnabled,
            bool usePersistentDataPath,
            string directoryPath,
            string fileName,
            bool appendToFile,
            bool writeSessionHeader,
            int flushEveryEntries)
        {
            IsEnabled = isEnabled;
            UsePersistentDataPath = usePersistentDataPath;
            DirectoryPath = string.IsNullOrWhiteSpace(directoryPath) ? DefaultDirectory : directoryPath.Trim();
            FileName = string.IsNullOrWhiteSpace(fileName) ? DefaultFileName : fileName.Trim();
            AppendToFile = appendToFile;
            WriteSessionHeader = writeSessionHeader;
            FlushEveryEntries = Mathf.Max(1, flushEveryEntries);
        }

        public bool IsEnabled { get; }
        public bool UsePersistentDataPath { get; }
        public string DirectoryPath { get; }
        public string FileName { get; }
        public bool AppendToFile { get; }
        public bool WriteSessionHeader { get; }
        public int FlushEveryEntries { get; }

        public string ResolveFilePath()
        {
            string root = UsePersistentDataPath ? Application.persistentDataPath : Application.dataPath;
            string directory = Path.IsPathRooted(DirectoryPath)
                ? DirectoryPath
                : Path.Combine(root, DirectoryPath);

            return Path.Combine(directory, FileName);
        }
    }
}
