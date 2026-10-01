using UnityEngine;
using Zenject;

namespace DanroJump.Debugging
{
    /// <summary>
    /// Регистрирует глобальный runtime-сервис отладочного вывода в ProjectContext.
    /// </summary>
    public sealed class RuntimeDebugInstaller : MonoInstaller
    {
        [SerializeField] private bool enabledOnStartup = true;
        [SerializeField] private DebugLogChannel enabledChannels = DebugLogChannel.All;
        [SerializeField] private DebugLogOutput enabledOutputs = DebugLogOutput.UnityConsole | DebugLogOutput.CustomHandlers;

        [Header("File Handler")]
        [SerializeField] private bool fileLoggingEnabled;
        [SerializeField] private bool fileLoggingUsePersistentDataPath = true;
        [SerializeField] private string fileLoggingDirectory = "Logs";
        [SerializeField] private string fileLoggingFileName = "debug.log";
        [SerializeField] private bool fileLoggingAppendToFile = true;
        [SerializeField] private bool fileLoggingWriteSessionHeader = true;
        [SerializeField] [Min(1)] private int fileLoggingFlushEveryEntries = 1;

        public override void InstallBindings()
        {
            var service = DebugLogService.GetOrCreate();
            DebugLogOutput resolvedOutputs = fileLoggingEnabled
                ? enabledOutputs | DebugLogOutput.CustomHandlers
                : enabledOutputs;

            service.IsEnabled = enabledOnStartup;
            service.EnabledChannels = enabledChannels;
            service.EnabledOutputs = resolvedOutputs;

            Container.Bind<IDebugLogService>().FromInstance(service).AsSingle();
            if (fileLoggingEnabled)
            {
                Container.BindInterfacesAndSelfTo<FileDebugLogHandler>()
                    .AsSingle()
                    .WithArguments(CreateFileLogOptions())
                    .NonLazy();
            }

            Container.BindInterfacesTo<RuntimeDebugSceneInjector>().AsSingle().NonLazy();
        }

        private DebugFileLogOptions CreateFileLogOptions()
        {
            return new DebugFileLogOptions(
                fileLoggingEnabled,
                fileLoggingUsePersistentDataPath,
                fileLoggingDirectory,
                fileLoggingFileName,
                fileLoggingAppendToFile,
                fileLoggingWriteSessionHeader,
                fileLoggingFlushEveryEntries);
        }
    }
}
