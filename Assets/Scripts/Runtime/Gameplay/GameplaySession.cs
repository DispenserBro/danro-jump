using System;
using DanroJump.Audio;
using DanroJump.Hardware.Com;
using DanroJump.Prizes;
using DanroJump.SceneFlow;
using DanroJump.Settings;
using UnityEngine;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Хранит состояние текущего забега: счет, монеты, жизни, высоту и рекорд.
    /// </summary>
    public sealed class GameplaySession : IDisposable
    {
        private const string RecordHeightPrefsKey = "DanroJump.Gameplay.RecordHeight";
        private const int DefaultStartingLives = 3;
        private const int DefaultAdditionalLives = 1;
        private const float HeightMetersPerScorePoint = 10f;
        private static readonly TimeSpan GameOverOverlayReturnDelay = TimeSpan.FromSeconds(8f);

        private readonly IPlayerController player;
        private readonly IReadOnlyServiceSettings serviceSettings;
        private readonly ISceneFlowService sceneFlow;
        private readonly IPrizeFlowService prizeFlow;
        private readonly IComCreditWallet creditWallet;
        private readonly IGameAudioService audioService;
        private readonly int startingLives;
        private int baseScore;
        private float savedRecordHeight;

        public string SessionId { get; private set; } = CreateSessionId();

        /// <summary>
        /// Текущий комбо-множитель очков (увеличивается при сборе монет/врагах, сбрасывается на платформах).
        /// </summary>
        public int CurrentCombo { get; private set; } = 1;

        /// <summary>
        /// Итоговые очки текущего забега: игровые начисления плюс бонус за максимальную высоту.
        /// </summary>
        public int Score => baseScore + HeightScore;

        /// <summary>
        /// Очки, начисленные за максимальную высоту текущего забега.
        /// </summary>
        public int HeightScore => Mathf.FloorToInt(MaxRunHeight / HeightMetersPerScorePoint);

        /// <summary>
        /// Монеты, собранные в текущем забеге.
        /// </summary>
        public int Coins { get; private set; }

        /// <summary>
        /// Количество смертей в текущем забеге.
        /// </summary>
        public int DeathCount { get; private set; }

        /// <summary>
        /// Оставшиеся жизни до завершения забега.
        /// </summary>
        public int RemainingLives { get; private set; }

        /// <summary>
        /// Текущее состояние gameplay-сессии.
        /// </summary>
        public GameplaySessionState State { get; private set; }

        /// <summary>
        /// Фактическая текущая высота игрока над стартом.
        /// </summary>
        public float CurrentHeight => player != null ? Mathf.Max(0f, player.Transform.position.y) : 0f;

        /// <summary>
        /// Максимальная высота, достигнутая в текущем забеге.
        /// </summary>
        public float MaxRunHeight => player != null ? Mathf.Max(0f, player.HighestY) : 0f;

        /// <summary>
        /// Лучшая высота текущего забега.
        /// </summary>
        public float BestHeight => MaxRunHeight;

        /// <summary>
        /// Абсолютный рекорд с учетом сохраненного значения и текущего забега.
        /// </summary>
        public float RecordHeight => Mathf.Max(savedRecordHeight, MaxRunHeight);

        /// <summary>
        /// Показывает, находится ли игрок в состоянии смерти или завершенного забега.
        /// </summary>
        public bool IsPlayerDead => State == GameplaySessionState.Respawning || State == GameplaySessionState.GameOver;

        public int ContinueGameCost => ResolveContinueCost();

        public int ContinueGameLives => ResolveAdditionalLives();

        public bool CanContinueGame => State == GameplaySessionState.GameOver &&
            IsContinueEnabled() &&
            HasContinueBalance(ContinueGameCost);

        public event Action Changed;

        /// <summary>
        /// Создает сессию и подписывается на события смерти/respawn игрока.
        /// </summary>
        public GameplaySession(
            IPlayerController player,
            [Zenject.InjectOptional] IReadOnlyServiceSettings serviceSettings = null,
            [Zenject.InjectOptional] ISceneFlowService sceneFlow = null,
            [Zenject.InjectOptional] IPrizeFlowService prizeFlow = null,
            [Zenject.InjectOptional] IComCreditWallet creditWallet = null,
            [Zenject.InjectOptional] IGameAudioService audioService = null)
        {
            this.player = player;
            this.serviceSettings = serviceSettings;
            this.sceneFlow = sceneFlow;
            this.prizeFlow = prizeFlow;
            this.creditWallet = creditWallet;
            this.audioService = audioService;
            startingLives = ResolveStartingLives();
            RemainingLives = startingLives;
            State = GameplaySessionState.Running;
            savedRecordHeight = PlayerPrefs.GetFloat(RecordHeightPrefsKey, 0f);

            if (this.player != null)
            {
                this.player.Died += HandlePlayerDied;
                this.player.Respawned += HandlePlayerRespawned;
                this.player.LandedOnNormalPlatform += ResetCombo;
            }
        }

        /// <summary>
        /// Увеличивает комбо-множитель.
        /// </summary>
        public void IncrementCombo()
        {
            CurrentCombo++;
            Changed?.Invoke();
        }

        /// <summary>
        /// Сбрасывает комбо-множитель в 1.
        /// </summary>
        public void ResetCombo()
        {
            if (CurrentCombo != 1)
            {
                CurrentCombo = 1;
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// Добавляет очки в текущий забег (с учетом комбо-множителя).
        /// </summary>
        public void AddScore(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            baseScore += amount * CurrentCombo;
            CurrentCombo++;
            Changed?.Invoke();
        }

        /// <summary>
        /// Добавляет монеты в текущий забег.
        /// </summary>
        public void AddCoins(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Coins += amount;
            Changed?.Invoke();
        }

        /// <summary>
        /// Полностью сбрасывает забег, сохранив рекорд при необходимости.
        /// </summary>
        public void ResetSession()
        {
            SaveRecordIfNeeded();
            SessionId = CreateSessionId();
            baseScore = 0;
            Coins = 0;
            DeathCount = 0;
            RemainingLives = startingLives;
            State = GameplaySessionState.Running;
            CurrentCombo = 1;
            player?.ResetRun();
            Changed?.Invoke();
        }

        public bool TryContinueGame()
        {
            if (!CanContinueGame)
            {
                return false;
            }

            if (!TrySpendContinueBalance(ContinueGameCost))
            {
                Changed?.Invoke();
                return false;
            }

            audioService?.Play(GameAudioEvent.ContinueAccepted);
            sceneFlow?.CancelGameOverReturn();
            RemainingLives = ContinueGameLives;
            State = GameplaySessionState.Respawning;
            CurrentCombo = 1;

            if (player != null)
            {
                player.AllowRespawn();
                player.Respawn();
            }
            else
            {
                State = GameplaySessionState.Running;
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Сохраняет рекорд и отписывается от событий игрока.
        /// </summary>
        public void Dispose()
        {
            SaveRecordIfNeeded();

            if (player == null)
            {
                return;
            }

            player.Died -= HandlePlayerDied;
            player.Respawned -= HandlePlayerRespawned;
            player.LandedOnNormalPlatform -= ResetCombo;
        }

        /// <summary>
        /// Обновляет состояние сессии после смерти игрока.
        /// </summary>
        private void HandlePlayerDied(IPlayerController _)
        {
            audioService?.Play(GameAudioEvent.PlayerDeath);
            DeathCount++;
            RemainingLives = Mathf.Max(0, RemainingLives - 1);
            State = RemainingLives > 0 ? GameplaySessionState.Respawning : GameplaySessionState.GameOver;
            CurrentCombo = 1;

            if (State == GameplaySessionState.GameOver)
            {
                audioService?.Play(GameAudioEvent.GameOver);
                audioService?.PlayMusic(GameMusicCue.GameOver);
                player?.BlockPendingRespawn();
                var prizeResult = prizeFlow?.StartPrizeFlow(this) ?? default;
                if (!prizeResult.HasPrize)
                {
                    StartGameOverReturn(GameOverOverlayReturnDelay);
                }
            }
            else
            {
                player?.ScheduleRespawn(player.DeathRespawnDelay);
            }

            SaveRecordIfNeeded();
            Changed?.Invoke();
        }

        /// <summary>
        /// Возвращает сессию в Running после respawn, если игра еще не закончена.
        /// </summary>
        private void HandlePlayerRespawned(IPlayerController _)
        {
            if (State != GameplaySessionState.GameOver)
            {
                State = GameplaySessionState.Running;
                audioService?.Play(GameAudioEvent.PlayerRespawn);
                audioService?.PlayMusic(GameMusicCue.Gameplay);
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Читает стартовое количество жизней из сервисных настроек.
        /// </summary>
        private int ResolveStartingLives()
        {
            if (serviceSettings == null)
            {
                return DefaultStartingLives;
            }

            var configuredLives = serviceSettings.GetInt(ServiceSettingsKeys.StartingLives);
            return Mathf.Max(1, configuredLives);
        }

        private int ResolveAdditionalLives()
        {
            if (serviceSettings == null)
            {
                return DefaultAdditionalLives;
            }

            return Mathf.Max(1, serviceSettings.GetInt(ServiceSettingsKeys.AdditionalLives));
        }

        private int ResolveContinueCost()
        {
            return serviceSettings != null
                ? Mathf.Max(0, serviceSettings.GetInt(ServiceSettingsKeys.ContinueGameCost))
                : 0;
        }

        public bool IsContinueEnabled()
        {
            return serviceSettings != null && serviceSettings.GetBool(ServiceSettingsKeys.CanContinueGame);
        }

        private bool HasContinueBalance(int cost)
        {
            return IsFreePlay() || cost <= 0 || creditWallet != null && creditWallet.Credits >= cost;
        }

        private bool TrySpendContinueBalance(int cost)
        {
            if (IsFreePlay() || cost <= 0)
            {
                return true;
            }

            return creditWallet != null && creditWallet.TrySpendCredits(cost);
        }

        private bool IsFreePlay()
        {
            return serviceSettings != null && serviceSettings.GetBool(ServiceSettingsKeys.FreePlay);
        }

        /// <summary>
        /// Сохраняет лучший результат по высоте в PlayerPrefs.
        /// </summary>
        private void SaveRecordIfNeeded()
        {
            if (MaxRunHeight <= savedRecordHeight)
            {
                return;
            }

            savedRecordHeight = MaxRunHeight;
            PlayerPrefs.SetFloat(RecordHeightPrefsKey, savedRecordHeight);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Запускает возврат в главное меню после финальной смерти.
        /// </summary>
        private void StartGameOverReturn(TimeSpan delay)
        {
            if (sceneFlow == null)
            {
                Debug.LogWarning($"{nameof(GameplaySession)} cannot return to main menu because {nameof(ISceneFlowService)} is not bound.");
                return;
            }

            sceneFlow.ReturnToMainMenuAfterGameOver(delay);
        }

        private static string CreateSessionId()
        {
            return Guid.NewGuid().ToString("N");
        }
    }

    /// <summary>
    /// Состояние игрового забега.
    /// </summary>
    public enum GameplaySessionState
    {
        Waiting = 0,
        Running = 1,
        Respawning = 2,
        GameOver = 3,
    }
}
