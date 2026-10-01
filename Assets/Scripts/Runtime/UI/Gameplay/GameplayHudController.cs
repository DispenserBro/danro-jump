using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DanroJump.Audio;
using DanroJump.Gameplay;
using DanroJump.Hardware.Com;
using DanroJump.Prizes;
using DanroJump.Prizes.Qr;
using DanroJump.SceneFlow;
using DanroJump.Settings;
using UnityEngine;
using UnityEngine.UIElements;
using Zenject;

namespace DanroJump.UI.Gameplay
{
    /// <summary>
    /// Отображает runtime-показатели игрового забега в UI Toolkit HUD.
    /// </summary>
    public sealed class GameplayHudController : MonoBehaviour
    {
        [SerializeField] private VisualTreeAsset visualTreeAsset;
        [SerializeField] private PanelSettings panelSettings;
        [SerializeField] private int sortingOrder = 5000;
        [SerializeField] private string scoreLabelName = "ScoreValue";
        [SerializeField] private string heightLabelName = "HeightValue";
        [SerializeField] private string bestHeightLabelName = "BestHeightValue";
        [SerializeField] private string livesLabelName = "LivesValue";
        [SerializeField] private string coinsLabelName = "CoinsValue";
        [SerializeField] private string deathsLabelName = "DeathsValue";
        [SerializeField] private string stateLabelName = "StateValue";
        [SerializeField] private string developmentStatsRootName = "RightStats";

        private UIDocument document;
        private GameplaySession session;
        private IPrizeFlowService prizeFlow;
        private IPrizeSessionStateService prizeSessionState;
        private IPrizeQrService prizeQrService;
        private ISceneFlowService sceneFlow;
        private IReadOnlyServiceSettings serviceSettings;
        private IGameAudioService audioService;
        private IComSystem comSystem;
        private PrizeResultOverlayWindow prizeResultOverlay;
        private ContinueGameOverlayWindow continueGameOverlay;
        private VisualElement prizeOverlayRoot;
        private VisualElement continueOverlayRoot;
        private VisualElement developmentStatsRoot;
        private Label scoreLabel;
        private Label heightLabel;
        private Label bestHeightLabel;
        private Label livesLabel;
        private Label coinsLabel;
        private Label deathsLabel;
        private Label stateLabel;
        private Label comboLabel;
        private CancellationTokenSource refreshCancellation;
        private CancellationTokenSource qrTimeoutCancellation;
        private CancellationTokenSource physicalDispenseCancellation;
        private bool isSubscribedToSession;
        private bool isSubscribedToPrizeFlow;

        private int lastScore = -1;
        private int lastHeight = -1;
        private int lastRecord = -1;
        private int lastLives = -1;
        private int lastCoins = -1;
        private int lastDeaths = -1;
        private int lastCombo = -1;
        private GameplaySessionState lastState = (GameplaySessionState)(-1);
        private bool isSubscribedToSettings;
        private bool enabledStatsInSettings;

        /// <summary>
        /// Получает игровую сессию из DI-контейнера сцены.
        /// </summary>
        [Inject]
        public void Construct(
            GameplaySession injectedSession,
            [InjectOptional] IPrizeFlowService injectedPrizeFlow = null,
            [InjectOptional] IPrizeSessionStateService injectedPrizeSessionState = null,
            [InjectOptional] IPrizeQrService injectedPrizeQrService = null,
            [InjectOptional] ISceneFlowService injectedSceneFlow = null,
            [InjectOptional] IReadOnlyServiceSettings injectedServiceSettings = null,
            [InjectOptional] IGameAudioService injectedAudioService = null,
            [InjectOptional] IComSystem injectedComSystem = null)
        {
            prizeFlow = injectedPrizeFlow;
            prizeSessionState = injectedPrizeSessionState;
            prizeQrService = injectedPrizeQrService;
            sceneFlow = injectedSceneFlow;
            serviceSettings = injectedServiceSettings;
            audioService = injectedAudioService;
            comSystem = injectedComSystem;
            SetSession(injectedSession);
            SubscribeToPrizeFlow();
        }

        private void Start()
        {
            EnsureDocument();
            BindLabels();
            BindPrizeOverlay();
            BindContinueOverlay();
            ShowLastPrizeResultIfNeeded();
            ResetCache();
            Refresh();
        }

        /// <summary>
        /// Подписывается на изменения сессии и запускает кадровую подстраховку обновления HUD.
        /// </summary>
        private void OnEnable()
        {
            SubscribeToSession();
            SubscribeToPrizeFlow();
            SubscribeToSettings();

            if (refreshCancellation == null)
            {
                refreshCancellation = new CancellationTokenSource();
                RefreshContinuouslyAsync(refreshCancellation.Token).Forget(Debug.LogException);
            }

            ResetCache();
            Refresh();
        }

        /// <summary>
        /// Останавливает обновление HUD и снимает подписку с игровой сессии.
        /// </summary>
        private void OnDisable()
        {
            if (refreshCancellation != null)
            {
                refreshCancellation.Cancel();
                refreshCancellation.Dispose();
                refreshCancellation = null;
            }

            if (qrTimeoutCancellation != null)
            {
                qrTimeoutCancellation.Cancel();
                qrTimeoutCancellation.Dispose();
                qrTimeoutCancellation = null;
            }

            if (physicalDispenseCancellation != null)
            {
                physicalDispenseCancellation.Cancel();
                physicalDispenseCancellation.Dispose();
                physicalDispenseCancellation = null;
            }

            UnsubscribeFromSession();
            UnsubscribeFromPrizeFlow();
            UnsubscribeFromSettings();
            prizeResultOverlay?.Dispose();
            prizeResultOverlay = null;
            prizeOverlayRoot = null;
            continueGameOverlay?.Dispose();
            continueGameOverlay = null;
            continueOverlayRoot = null;
        }

        /// <summary>
        /// Создает или настраивает UIDocument, чтобы HUD мог жить на отдельном GameObject без ручной сборки.
        /// </summary>
        private void EnsureDocument()
        {
            if (document == null && !TryGetComponent(out document))
            {
                document = gameObject.AddComponent<UIDocument>();
            }

            if (document == null)
            {
                return;
            }

            if (document.visualTreeAsset != visualTreeAsset)
            {
                document.visualTreeAsset = visualTreeAsset;
            }
            if (document.panelSettings != panelSettings)
            {
                document.panelSettings = panelSettings;
            }
            if (document.sortingOrder != sortingOrder)
            {
                document.sortingOrder = sortingOrder;
            }
        }

        /// <summary>
        /// Находит текстовые поля в UXML по именам, заданным в инспекторе.
        /// </summary>
        private void BindLabels()
        {
            var root = document != null ? document.rootVisualElement : null;
            if (root == null)
            {
                return;
            }

            developmentStatsRoot = root.Q<VisualElement>(developmentStatsRootName);
            scoreLabel = FindLabel(root, scoreLabelName);
            heightLabel = FindLabel(root, heightLabelName);
            bestHeightLabel = FindLabel(root, bestHeightLabelName);
            livesLabel = FindLabel(root, livesLabelName);
            coinsLabel = FindLabel(root, coinsLabelName);
            deathsLabel = FindLabel(root, deathsLabelName);
            stateLabel = FindLabel(root, stateLabelName);
            comboLabel = FindLabel(root, "ComboValue");
        }

        private void BindPrizeOverlay()
        {
            if (prizeResultOverlay != null && prizeOverlayRoot != null && prizeOverlayRoot.panel != null)
            {
                return;
            }

            var root = document != null ? document.rootVisualElement : null;
            var overlayRoot = root?.Q<VisualElement>("PrizeResultOverlay");
            if (overlayRoot == null)
            {
                return;
            }

            prizeOverlayRoot = overlayRoot;
            prizeResultOverlay = new PrizeResultOverlayWindow(overlayRoot, audioService);
            prizeResultOverlay.ReturnRequested += HandlePrizeReturnRequested;
        }

        private void BindContinueOverlay()
        {
            if (continueGameOverlay != null && continueOverlayRoot != null && continueOverlayRoot.panel != null)
            {
                return;
            }

            var root = document != null ? document.rootVisualElement : null;
            var overlayRoot = root?.Q<VisualElement>("ContinueGameOverlay");
            if (overlayRoot == null)
            {
                return;
            }

            continueOverlayRoot = overlayRoot;
            continueGameOverlay = new ContinueGameOverlayWindow(overlayRoot, audioService);
            continueGameOverlay.ContinueRequested += HandleContinueRequested;
            continueGameOverlay.MenuRequested += HandleContinueMenuRequested;
        }

        /// <summary>
        /// Обновляет все значения HUD из текущей игровой сессии.
        /// </summary>
        private void Refresh()
        {
            RefreshDevelopmentStatsVisibility();

            if (session == null)
            {
                SetLabel(stateLabel, "WAITING");
                return;
            }

            int currentScore = session.Score;
            if (currentScore != lastScore)
            {
                SetLabel(scoreLabel, currentScore.ToString());
                lastScore = currentScore;
            }

            int currentHeight = Mathf.FloorToInt(session.MaxRunHeight);
            if (currentHeight != lastHeight)
            {
                SetLabel(heightLabel, $"{currentHeight} м");
                lastHeight = currentHeight;
            }

            int currentRecord = Mathf.FloorToInt(session.RecordHeight);
            if (currentRecord != lastRecord)
            {
                SetLabel(bestHeightLabel, $"{currentRecord} м");
                lastRecord = currentRecord;
            }

            int currentLives = session.RemainingLives;
            if (currentLives != lastLives)
            {
                SetLabel(livesLabel, currentLives.ToString());
                lastLives = currentLives;
            }

            int currentCoins = session.Coins;
            if (currentCoins != lastCoins)
            {
                SetLabel(coinsLabel, currentCoins.ToString());
                lastCoins = currentCoins;
            }

            int currentDeaths = session.DeathCount;
            if (currentDeaths != lastDeaths)
            {
                SetLabel(deathsLabel, currentDeaths.ToString());
                lastDeaths = currentDeaths;
            }

            GameplaySessionState currentState = session.State;
            if (currentState != lastState)
            {
                SetLabel(stateLabel, FormatState(currentState));
                lastState = currentState;
            }

            if (comboLabel != null)
            {
                int currentCombo = session.CurrentCombo;
                if (currentCombo > 1)
                {
                    if (currentCombo != lastCombo)
                    {
                        var comboText = $"x{currentCombo} COMBO!";
                        comboLabel.text = comboText;
                        comboLabel.style.display = DisplayStyle.Flex;
                        PulseComboLabel();
                        lastCombo = currentCombo;
                    }
                }
                else
                {
                    if (lastCombo != currentCombo)
                    {
                        comboLabel.style.display = DisplayStyle.None;
                        lastCombo = currentCombo;
                    }
                }
            }

            RefreshContinueOverlay();
        }

        private void PulseComboLabel()
        {
            if (comboLabel == null)
            {
                return;
            }

            comboLabel.style.scale = new StyleScale(new Scale(new Vector3(1.4f, 1.4f, 1f)));
            ResetComboScaleAsync().Forget();
        }

        private async UniTaskVoid ResetComboScaleAsync()
        {
            await UniTask.Delay(TimeSpan.FromMilliseconds(80));
            if (comboLabel != null)
            {
                comboLabel.style.scale = new StyleScale(new Scale(new Vector3(1.0f, 1.0f, 1f)));
            }
        }

        /// <summary>
        /// Безопасно обновляет текст label, если элемент найден в UXML.
        /// </summary>
        private static void SetLabel(Label label, string value)
        {
            if (label != null)
            {
                label.text = value;
            }
        }

        /// <summary>
        /// Ищет Label по имени, заданному в настройках контроллера.
        /// </summary>
        private static Label FindLabel(VisualElement root, string labelName)
        {
            return root != null && !string.IsNullOrEmpty(labelName)
                ? root.Q<Label>(labelName)
                : null;
        }

        private void RefreshDevelopmentStatsVisibility()
        {
            if (developmentStatsRoot == null)
            {
                return;
            }

            var targetDisplay = ShouldShowDevelopmentStats ? DisplayStyle.Flex : DisplayStyle.None;
            if (developmentStatsRoot.style.display != targetDisplay)
            {
                developmentStatsRoot.style.display = targetDisplay;
            }
        }

        private bool ShouldShowDevelopmentStats => enabledStatsInSettings && (Debug.isDebugBuild || Application.isEditor);

        /// <summary>
        /// Подстраховывает HUD от событий, которые могли прийти до подписки.
        /// </summary>
        private async UniTask RefreshContinuouslyAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (enabled && !cancellationToken.IsCancellationRequested)
                {
                    Refresh();
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
            }
            catch (System.OperationCanceledException)
            {
            }
        }

        /// <summary>
        /// Преобразует состояние сессии в короткий текст для HUD.
        /// </summary>
        private static string FormatState(GameplaySessionState state)
        {
            return state switch
            {
                GameplaySessionState.Running => "RUN",
                GameplaySessionState.Respawning => "RESPAWN",
                GameplaySessionState.GameOver => "GAME OVER",
                _ => "WAITING",
            };
        }

        private void SetSession(GameplaySession injectedSession)
        {
            if (session == injectedSession)
            {
                return;
            }

            UnsubscribeFromSession();
            session = injectedSession;
            ResetCache();
            SubscribeToSession();
            Refresh();
        }

        private void ResetCache()
        {
            lastScore = -1;
            lastHeight = -1;
            lastRecord = -1;
            lastLives = -1;
            lastCoins = -1;
            lastDeaths = -1;
            lastCombo = -1;
            lastState = (GameplaySessionState)(-1);
        }

        private void SubscribeToSettings()
        {
            if (serviceSettings == null || isSubscribedToSettings)
            {
                return;
            }

            serviceSettings.SettingChanged += HandleSettingChanged;
            isSubscribedToSettings = true;
            UpdateStatsVisibilityCache();
        }

        private void UnsubscribeFromSettings()
        {
            if (serviceSettings == null || !isSubscribedToSettings)
            {
                return;
            }

            serviceSettings.SettingChanged -= HandleSettingChanged;
            isSubscribedToSettings = false;
        }

        private void HandleSettingChanged(string key, DanroJump.Settings.ServiceSettingValue value)
        {
            if (key == DanroJump.Settings.ServiceSettingsKeys.StatisticsButton)
            {
                UpdateStatsVisibilityCache();
                RefreshDevelopmentStatsVisibility();
            }
        }

        private void UpdateStatsVisibilityCache()
        {
            enabledStatsInSettings = serviceSettings != null && serviceSettings.GetBool(DanroJump.Settings.ServiceSettingsKeys.StatisticsButton);
        }

        private void SubscribeToSession()
        {
            if (!isActiveAndEnabled || session == null || isSubscribedToSession)
            {
                return;
            }

            session.Changed += Refresh;
            isSubscribedToSession = true;
        }

        private void UnsubscribeFromSession()
        {
            if (session == null || !isSubscribedToSession)
            {
                return;
            }

            session.Changed -= Refresh;
            isSubscribedToSession = false;
        }

        private void SubscribeToPrizeFlow()
        {
            if (!isActiveAndEnabled || prizeFlow == null || isSubscribedToPrizeFlow)
            {
                return;
            }

            prizeFlow.PrizeFlowStarted += HandlePrizeFlowStarted;
            isSubscribedToPrizeFlow = true;
        }

        private void UnsubscribeFromPrizeFlow()
        {
            if (prizeFlow == null || !isSubscribedToPrizeFlow)
            {
                return;
            }

            prizeFlow.PrizeFlowStarted -= HandlePrizeFlowStarted;
            isSubscribedToPrizeFlow = false;
        }

        private void HandlePrizeFlowStarted(PrizeFlowResult result)
        {
            if (!result.HasPrize)
            {
                return;
            }

            audioService?.Play(result.RewardKind == PrizeRewardKind.QRCode
                ? GameAudioEvent.PrizeQrStarted
                : GameAudioEvent.PrizePhysicalStarted);
            audioService?.Play(GameAudioEvent.PrizeStarted);
            audioService?.PlayMusic(GameMusicCue.Prize);
            EnsureDocument();
            BindPrizeOverlay();

            PrizeQrTicket qrTicket = null;
            var qrError = string.Empty;
            if (result.RewardKind == PrizeRewardKind.QRCode && prizeQrService != null)
            {
                prizeQrService.TryCreateTicket(result, out qrTicket, out qrError);
                if (qrTicket == null)
                {
                    prizeSessionState?.FailActiveSession(string.IsNullOrWhiteSpace(qrError)
                        ? "Не удалось подготовить QR-приз."
                        : qrError);
                }
            }

            prizeResultOverlay?.SetResult(
                result,
                serviceSettings?.GetString(ServiceSettingsKeys.BigPrize),
                serviceSettings?.GetString(ServiceSettingsKeys.PrizePhoneNumber),
                qrTicket,
                qrError);
            prizeResultOverlay?.Open();
            if (qrTicket != null || result.RewardKind != PrizeRewardKind.QRCode)
            {
                prizeSessionState?.MarkActiveSessionShown();
            }

            if (result.RewardKind == PrizeRewardKind.QRCode)
            {
                prizeSessionState?.RecordQrShow();
                if (qrTimeoutCancellation != null)
                {
                    qrTimeoutCancellation.Cancel();
                    qrTimeoutCancellation.Dispose();
                }
                qrTimeoutCancellation = new CancellationTokenSource();
                StartQrTimeoutAsync(qrTimeoutCancellation.Token).Forget(Debug.LogException);
            }
            else if (result.RewardKind == PrizeRewardKind.Hopper || result.RewardKind == PrizeRewardKind.PrizeStand)
            {
                if (physicalDispenseCancellation != null)
                {
                    physicalDispenseCancellation.Cancel();
                    physicalDispenseCancellation.Dispose();
                }
                physicalDispenseCancellation = new CancellationTokenSource();
                DispensePhysicalPrizeAsync(result, physicalDispenseCancellation.Token).Forget(Debug.LogException);
            }
        }

        private void HandlePrizeReturnRequested()
        {
            audioService?.Play(GameAudioEvent.UiBack);
            if (qrTimeoutCancellation != null)
            {
                qrTimeoutCancellation.Cancel();
                qrTimeoutCancellation.Dispose();
                qrTimeoutCancellation = null;
            }
            if (physicalDispenseCancellation != null)
            {
                physicalDispenseCancellation.Cancel();
                physicalDispenseCancellation.Dispose();
                physicalDispenseCancellation = null;
            }
            prizeSessionState?.CompleteActiveSession("Оператор закрыл экран выдачи приза.");
            prizeResultOverlay?.Close();
            sceneFlow?.OpenMainMenu();
        }

        private void ShowLastPrizeResultIfNeeded()
        {
            if (prizeFlow == null || !prizeFlow.LastResult.HasPrize)
            {
                return;
            }

            HandlePrizeFlowStarted(prizeFlow.LastResult);
        }

        private void RefreshContinueOverlay()
        {
            if (session == null || session.State != GameplaySessionState.GameOver || prizeResultOverlay != null && prizeResultOverlay.IsOpen)
            {
                continueGameOverlay?.Close();
                return;
            }

            if (!session.IsContinueEnabled())
            {
                continueGameOverlay?.Close();
                return;
            }

            EnsureDocument();
            BindContinueOverlay();

            if (session.CanContinueGame)
            {
                continueGameOverlay?.SetOffer(session.ContinueGameCost, session.ContinueGameLives);
            }
            else
            {
                continueGameOverlay?.SetUnavailable();
            }

            if (continueGameOverlay != null && !continueGameOverlay.IsOpen)
            {
                continueGameOverlay.Open();
            }
        }

        private void HandleContinueRequested()
        {
            if (session != null && session.TryContinueGame())
            {
                audioService?.Play(GameAudioEvent.UiSubmit);
                continueGameOverlay?.Close();
                return;
            }

            audioService?.Play(GameAudioEvent.UiError);
            continueGameOverlay?.SetUnavailable();
        }

        private void HandleContinueMenuRequested()
        {
            audioService?.Play(GameAudioEvent.UiBack);
            continueGameOverlay?.Close();
            sceneFlow?.OpenMainMenu();
        }

        private async UniTask StartQrTimeoutAsync(CancellationToken token)
        {
            var timeoutSeconds = 30;
            if (serviceSettings != null)
            {
                var val = serviceSettings.GetInt(ServiceSettingsKeys.InactivityTimeout);
                if (val > 0)
                {
                    timeoutSeconds = val;
                }
            }

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(timeoutSeconds), cancellationToken: token);
                if (prizeSessionState != null)
                {
                    prizeSessionState.CompleteActiveSession("QR-код закрыт автоматически по таймауту неактивности.");
                }
                prizeResultOverlay?.Close();
                sceneFlow?.OpenMainMenu();
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async UniTask DispensePhysicalPrizeAsync(PrizeFlowResult result, CancellationToken token)
        {
            if (prizeSessionState == null)
            {
                return;
            }

            try
            {
                if (comSystem == null || !comSystem.IsRunning)
                {
                    var err = "Ошибка: COM-система не запущена.";
                    prizeResultOverlay?.SetSubtitle(err);
                    prizeSessionState.RecordPhysicalDispense(false);
                    prizeSessionState.FailActiveSession(err);
                    return;
                }

                prizeResultOverlay?.SetSubtitle("Выдача приза...");
                var success = false;
                var detailMessage = string.Empty;

                if (result.RewardKind == PrizeRewardKind.Hopper)
                {
                    var hopperResult = await ComHopperDispenser.DispenseAsync(comSystem, token);
                    success = hopperResult.IsSuccess;
                    detailMessage = hopperResult.Message;
                }
                else if (result.RewardKind == PrizeRewardKind.PrizeStand)
                {
                    var statuses = await comSystem.GetPrizeBoxStatusesAsync(token);
                    prizeResultOverlay?.SetPrizeStandStatus(statuses);

                    var readyBoxes = await comSystem.GetReadyPrizeBoxesAsync(token);
                    if (readyBoxes == null || readyBoxes.Length == 0)
                    {
                        success = false;
                        detailMessage = "Ошибка выдачи: нет доступных ячеек на витрине.";
                    }
                    else
                    {
                        var boxNumber = readyBoxes[0];
                        prizeResultOverlay?.SetPrizeStandStatus(statuses, openingBox: boxNumber);
                        prizeResultOverlay?.SetSubtitle($"Открытие ячейки {boxNumber}...");
                        var openResult = await comSystem.OpenPrizeBoxAsync(boxNumber, token);
                        success = openResult.IsSuccess;
                        detailMessage = success
                            ? $"Ячейка {boxNumber} открыта!"
                            : $"Ошибка открытия ячейки {boxNumber}: {openResult.Message}";

                        if (success)
                        {
                            prizeResultOverlay?.SetPrizeStandStatus(statuses, openedBox: boxNumber);
                        }
                        else
                        {
                            prizeResultOverlay?.SetPrizeStandStatus(statuses, errorBox: boxNumber);
                        }
                    }
                }

                prizeResultOverlay?.SetSubtitle(detailMessage);
                prizeSessionState.RecordPhysicalDispense(success);
                if (success)
                {
                    prizeSessionState.CompleteActiveSession(detailMessage);
                }
                else
                {
                    prizeSessionState.FailActiveSession(detailMessage);
                }
            }
            catch (OperationCanceledException)
            {
                prizeResultOverlay?.SetSubtitle("Выдача приза отменена.");
                prizeSessionState.RecordPhysicalDispense(false);
                prizeSessionState.FailActiveSession("Выдача приза отменена.");
            }
            catch (Exception ex)
            {
                var err = $"Внутренняя ошибка выдачи: {ex.Message}";
                prizeResultOverlay?.SetSubtitle(err);
                prizeSessionState.RecordPhysicalDispense(false);
                prizeSessionState.FailActiveSession(err);
            }
        }

    }
}
