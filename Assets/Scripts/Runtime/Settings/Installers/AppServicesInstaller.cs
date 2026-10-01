using UnityEngine;
using Zenject;
using DanroJump.Audio;
using DanroJump.Prizes;
using DanroJump.SceneFlow;

namespace DanroJump.Settings
{
    /// <summary>
    /// ProjectContext/SceneContext installer сервисных настроек.
    /// ProjectContext/SceneContext installer глобальных сервисов.
    /// </summary>
    public sealed class AppServicesInstaller : MonoInstaller
    {
        [SerializeField] private string databaseAddress = ServiceSettingsAddressKeys.Database;
        [SerializeField] private ServiceSettingsDatabase fallbackDatabase;
        [SerializeField] private GameAudioSettings audioSettings;
        [SerializeField] private string fileName = "service-settings.json";
        [SerializeField] private string prizeSessionStateFileName = "prize-session-state.json";
        [SerializeField] private bool loadImmediately = true;
        [SerializeField] private bool bindActionsInScene = true;

        /// <summary>
        /// Загружает базу настроек, создает сервис и регистрирует его в DI-контейнере.
        /// </summary>
        public override void InstallBindings()
        {
            var databaseProvider = new AddressableServiceSettingsDatabaseProvider(databaseAddress, fallbackDatabase);
            var database = databaseProvider.Load();

            if (database == null)
            {
                Debug.LogError($"{nameof(AppServicesInstaller)} could not load {nameof(ServiceSettingsDatabase)}.", this);
                databaseProvider.Dispose();
                Container.BindInterfacesAndSelfTo<SceneFlowService>().AsSingle();
                return;
            }

            var repository = new JsonServiceSettingsRepository(fileName);
            var prizeSessionRepository = new JsonPrizeSessionStateRepository(prizeSessionStateFileName);
            var service = new ServiceSettingsService(database, repository);

            if (loadImmediately)
            {
                service.Load();
            }

            Container.BindInstance(database).AsSingle();
            Container.Bind<IServiceSettingsDatabaseProvider>().FromInstance(databaseProvider).AsSingle();
            Container.Bind<System.IDisposable>().FromInstance(databaseProvider).AsSingle();
            Container.Bind<IServiceSettingsRepository>().FromInstance(repository).AsSingle();
            Container.Bind<IReadOnlyServiceSettings>().FromInstance(service).AsSingle();
            Container.Bind<IServiceSettingsService>().FromInstance(service).AsSingle();
            Container.Bind<IPrizeSessionStateRepository>().FromInstance(prizeSessionRepository).AsSingle();
            Container.BindInterfacesAndSelfTo<PrizeSessionStateService>().AsSingle();
            Container.BindInterfacesAndSelfTo<SceneFlowService>().AsSingle();
            if (audioSettings != null)
            {
                Container.BindInstance(audioSettings).AsSingle();
            }

            Container.BindInterfacesAndSelfTo<GameAudioService>().AsSingle().NonLazy();
            Container.BindInterfacesTo<AppUIFacadeInjector>().AsSingle().WithArguments(bindActionsInScene).NonLazy();
        }
    }
}
