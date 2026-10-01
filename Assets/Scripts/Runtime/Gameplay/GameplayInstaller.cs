using UnityEngine;
using Zenject;
using DanroJump.Prizes;
using DanroJump.Prizes.Qr;
using DanroJump.UI.Gameplay;
using DanroJump.Vfx;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Регистрирует зависимости игровой сцены в SceneContext Extenject.
    /// Является единой точкой связывания gameplay-сервисов и сценовых объектов.
    /// </summary>
    public sealed class GameplayInstaller : MonoInstaller
    {
        [SerializeField] private JumpPlayerController player;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private PlatformSpawner platformSpawner;
        [SerializeField] private GameplayDifficultyProgression difficultyProgression;

        /// <summary>
        /// Привязывает игрока, камеру, спавнер, прогрессию сложности и runtime-сервисы.
        /// </summary>
        public override void InstallBindings()
        {
            if (!ValidateReferences())
            {
                throw new ZenjectException($"{nameof(GameplayInstaller)} has missing required scene references.");
            }

            Container.BindInstance(player).AsSingle();
            Container.Bind<IPlayerController>().FromInstance(player).AsSingle();
            Container.BindInstance(gameplayCamera).AsSingle();
            Container.BindInstance(platformSpawner).AsSingle();
            Container.BindInstance(difficultyProgression).AsSingle();
            Container.Bind<IGameplayDifficultyProgression>().FromInstance(difficultyProgression).AsSingle();
            Container.BindInterfacesAndSelfTo<PrizeFlowService>().AsSingle();
            Container.Bind<TelegramPrizeQrPayloadBuilder>().AsSingle();
            Container.Bind<QrCodeTextureRenderer>().AsSingle();
            Container.Bind<IPrizeQrService>().To<PrizeQrService>().AsSingle();
            Container.Bind<GameplaySession>().AsSingle().NonLazy();
            Container.Bind<PlatformContentSpawner>().AsSingle();
            Container.Bind<IVfxService>().To<GameVfxService>().AsSingle();
            Container.Bind<GameplayHudController>().FromComponentInHierarchy().AsSingle().NonLazy();

            // Сценовые MonoBehaviour уже существуют, поэтому явно ставим их в очередь на injection.
            Container.QueueForInject(player);
            Container.QueueForInject(difficultyProgression);
            Container.QueueForInject(platformSpawner);

            if (gameplayCamera != null && gameplayCamera.TryGetComponent(out VerticalCameraFollow cameraFollow))
            {
                Container.QueueForInject(cameraFollow);
            }
        }

        /// <summary>
        /// Проверяет обязательные ссылки сцены до регистрации зависимостей.
        /// </summary>
        /// <returns>True, если SceneContext настроен достаточно для запуска gameplay.</returns>
        private bool ValidateReferences()
        {
            var isValid = true;

            isValid &= ValidateReference(player, nameof(player));
            isValid &= ValidateReference(gameplayCamera, nameof(gameplayCamera));
            isValid &= ValidateReference(platformSpawner, nameof(platformSpawner));
            isValid &= ValidateReference(difficultyProgression, nameof(difficultyProgression));

            return isValid;
        }

        /// <summary>
        /// Валидирует одну сценовую ссылку и пишет понятную ошибку в Unity Console.
        /// </summary>
        private bool ValidateReference(Object reference, string fieldName)
        {
            if (reference != null)
            {
                return true;
            }

            Debug.LogError($"{nameof(GameplayInstaller)} is missing '{fieldName}' reference.", this);
            return false;
        }

    }
}
