using System.Collections.Generic;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DanroJump.Audio;
using DanroJump.Bootstrap;
using DanroJump.Hardware.Com;
using DanroJump.Prizes;
using DanroJump.Prizes.Qr;
using DanroJump.SceneFlow;
using DanroJump.Settings;
using DanroJump.UI.ArcadeInput;
using DanroJump.UI.Modals;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UIElements;
using Zenject;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DanroJump.UI.MainMenu
{
    /// <summary>
    /// Управляет главным меню: модальными окнами, запуском игры и сервисными настройками.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainMenuController : MonoBehaviour
    {
        private const string SupportPhonePrefix = "тех. поддержка";

        [SerializeField] private string gameplaySceneName = "Game";
        [SerializeField] private string settingsSceneName = "";
        [SerializeField] private string defaultFocusedButtonName = "RulesButton";
        [SerializeField] private Color normalButtonImageTint = Color.white;
        [SerializeField] private Color focusedButtonImageTint = new Color32(0xCE, 0xFF, 0x1A, 0xFF);
#if ENABLE_INPUT_SYSTEM
        private const string LegacySecretActionPath = "UI/Secret";

        [SerializeField] private InputActionReference secretActionReference;
        [SerializeField, FormerlySerializedAs("inputActionsAsset")] private InputActionAsset legacyInputActionsAsset;
#endif

        private UIDocument document;
        private VisualElement root;
        private Button startButton;
        private Button rulesButton;
        private Button settingsButton;
        private Button serviceButton;
        private Button quitButton;
        private Label livesValueLabel;
        private Label creditsValueLabel;
        private Label exchangeLivesValueLabel;
        private Label priceTextLabel;
        private Label versionLabel;
        private Label supportPhoneLabel;
        private ModalStackController modalStack;
        private RulesModalWindow rulesModal;
        private SettingsModalWindow settingsModal;
        private ServiceInfoModalWindow serviceInfoModal;
        private ServiceTextInputModalWindow serviceTextInputModal;
        private QrCodeSettingsModalWindow qrCodeSettingsModal;
        private PrizeLevelsModalWindow prizeLevelsModal;
        private PrizeLevelAddModalWindow prizeLevelAddModal;
        private readonly List<Button> menuButtons = new();
        private IReadOnlyServiceSettings serviceSettings;
        private IServiceSettingsService serviceSettingsService;
        private ISceneFlowService sceneFlow;
        private IComSystem comSystem;
        private IComCreditWallet creditWallet;
        private IGameAudioService audioService;
        private IPrizeSessionStateService prizeSessionState;
        private bool creditsChangedSubscribed;
        private bool serviceSettingsChangedSubscribed;
#if ENABLE_INPUT_SYSTEM
        private InputAction secretAction;
        private bool secretActionWasEnabled;
#endif

        /// <summary>
        /// Получает сервисные настройки через Extenject.
        /// Если DI еще не готов, контроллер попробует найти сервис в ProjectContext позже.
        /// </summary>
        [Inject]
        public void Construct(
            [InjectOptional] IReadOnlyServiceSettings injectedServiceSettings,
            [InjectOptional] IServiceSettingsService injectedSettingsService,
            [InjectOptional] ISceneFlowService injectedSceneFlow,
            [InjectOptional] IComSystem injectedComSystem,
            [InjectOptional] IGameAudioService injectedAudioService,
            [InjectOptional] IPrizeSessionStateService injectedPrizeSessionState)
        {
            serviceSettingsService = injectedSettingsService;
            serviceSettings = injectedServiceSettings ?? injectedSettingsService;
            sceneFlow = injectedSceneFlow;
            comSystem = injectedComSystem;
            creditWallet = injectedComSystem;
            audioService = injectedAudioService;
            prizeSessionState = injectedPrizeSessionState;
        }

        /// <summary>
        /// Кэширует UIDocument, от которого строится всё главное меню.
        /// </summary>
        private void Awake()
        {
            document = GetComponent<UIDocument>();
        }

        /// <summary>
        /// Находит элементы UI, подключает обработчики и подготавливает фокус главного меню.
        /// </summary>
        private void OnEnable()
        {
            root = document.rootVisualElement;

            // Находим элементы по именам из MainMenu.uxml.
            startButton = root.Q<Button>("StartButton");
            rulesButton = root.Q<Button>("RulesButton");
            settingsButton = root.Q<Button>("SettingsButton");
            serviceButton = root.Q<Button>("ServiceButton");
            quitButton = root.Q<Button>("QuitButton");
            livesValueLabel = root.Q<Label>("LivesValue");
            creditsValueLabel = root.Q<Label>("CreditsValue");
            exchangeLivesValueLabel = root.Q<Label>("ExchangeLivesValue");
            priceTextLabel = root.Q<Label>("PriceText");
            versionLabel = root.Q<Label>("VersionLabel");
            supportPhoneLabel = root.Q<Label>("SupportPhoneText");
            menuButtons.Clear();
            root.Query<Button>(className: "menu-button").ForEach(menuButtons.Add);

            // Настройки могут прийти через DI или через ProjectContext, если меню создано раньше инъекции.
            ResolveServiceSettingsIfNeeded();
            ResolveCreditWalletIfNeeded();
            ResolveSceneFlowIfNeeded();
            SetupModals(root);
            RegisterCallbacks();
            RegisterButtonFocusCallbacks();
            RegisterServiceSettingsCallbacks();
            RegisterCreditCallbacks();
            RegisterInputActions();
            RefreshStaticLabels();
            audioService?.PlayMusic(GameMusicCue.Menu);
            FocusDefaultButtonNextFrameAsync(this.GetCancellationTokenOnDestroy()).Forget(Debug.LogException);
        }

        /// <summary>
        /// Освобождает обработчики UI и Input System при выключении документа.
        /// </summary>
        private void OnDisable()
        {
            UnregisterInputActions();
            UnregisterCreditCallbacks();
            UnregisterServiceSettingsCallbacks();
            modalStack?.Dispose();
            if (settingsModal != null)
            {
                settingsModal.ServiceActionActivated -= HandleServiceActionActivated;
            }

            if (prizeLevelsModal != null)
            {
                prizeLevelsModal.AddRequested -= OpenPrizeLevelAddModal;
            }

            if (qrCodeSettingsModal != null)
            {
                qrCodeSettingsModal.TextEditRequested -= OpenQrPrizeTextInput;
            }

            if (prizeLevelAddModal != null)
            {
                prizeLevelAddModal.Saved -= HandlePrizeLevelSaved;
            }

            rulesModal?.Dispose();
            settingsModal?.Dispose();
            serviceInfoModal?.Dispose();
            serviceTextInputModal?.Dispose();
            qrCodeSettingsModal?.Dispose();
            prizeLevelsModal?.Dispose();
            prizeLevelAddModal?.Dispose();
            modalStack = null;
            rulesModal = null;
            settingsModal = null;
            serviceInfoModal = null;
            serviceTextInputModal = null;
            qrCodeSettingsModal = null;
            prizeLevelsModal = null;
            prizeLevelAddModal = null;
            UnregisterButtonFocusCallbacks();
            UnregisterCallbacks();
            menuButtons.Clear();
            root = null;
        }

        /// <summary>
        /// Создает runtime-обертки над модальными окнами, описанными в UXML.
        /// </summary>
        /// <param name="root">Корневой элемент UIDocument.</param>
        private void SetupModals(VisualElement root)
        {
            var modalLayer = root.Q<VisualElement>("ModalLayer");
            var modalBackdrop = root.Q<VisualElement>("ModalBackdrop");
            var rulesModalRoot = root.Q<VisualElement>("RulesModal");
            var settingsModalRoot = root.Q<VisualElement>("SettingsModal");
            var serviceInfoModalRoot = root.Q<VisualElement>("ServiceInfoModal");
            var serviceTextInputModalRoot = root.Q<VisualElement>("ServiceTextInputModal");
            var qrCodeSettingsModalRoot = root.Q<VisualElement>("QrSettingsModal");
            var prizeLevelsModalRoot = root.Q<VisualElement>("PrizeLevelsModal");
            var prizeLevelAddModalRoot = root.Q<VisualElement>("PrizeLevelAddModal");

            modalStack = new ModalStackController(modalLayer, modalBackdrop);
            rulesModal = new RulesModalWindow(rulesModalRoot);
            settingsModal = new SettingsModalWindow(settingsModalRoot, serviceSettingsService, prizeSessionState);
            serviceInfoModal = new ServiceInfoModalWindow(serviceInfoModalRoot);
            serviceTextInputModal = new ServiceTextInputModalWindow(serviceTextInputModalRoot);
            qrCodeSettingsModal = new QrCodeSettingsModalWindow(qrCodeSettingsModalRoot, serviceSettingsService);
            prizeLevelsModal = new PrizeLevelsModalWindow(prizeLevelsModalRoot, serviceSettingsService);
            prizeLevelAddModal = new PrizeLevelAddModalWindow(prizeLevelAddModalRoot);
            settingsModal.ServiceActionActivated += HandleServiceActionActivated;
            qrCodeSettingsModal.TextEditRequested += OpenQrPrizeTextInput;
            prizeLevelsModal.AddRequested += OpenPrizeLevelAddModal;
            prizeLevelAddModal.Saved += HandlePrizeLevelSaved;
            rulesModal.Close();
            settingsModal.Close();
            serviceInfoModal.Close();
            serviceTextInputModal.Close();
            qrCodeSettingsModal.Close();
            prizeLevelsModal.Close();
            prizeLevelAddModal.Close();
        }

        private void HandleServiceActionActivated(
            ServiceSettingDefinition definition,
            ServiceSettingsActionResult result)
        {
            if (!result.Handled)
            {
                return;
            }

            if (TryOpenPrizeLevels(result))
            {
                return;
            }

            if (TryOpenQrSettings(result))
            {
                return;
            }

            if (TryOpenServiceTextInput(result))
            {
                return;
            }

            if (TryRunHopperTest(result))
            {
                return;
            }

            if (serviceInfoModal == null || serviceInfoModal.Root == null)
            {
                return;
            }

            serviceInfoModal.SetResult(result);
            modalStack?.Open(serviceInfoModal);
        }

        private bool TryRunHopperTest(ServiceSettingsActionResult result)
        {
            if (!string.Equals(result.FocusKey, ServiceSettingsKeys.HopperTestButton, StringComparison.Ordinal))
            {
                return false;
            }

            if (serviceInfoModal == null || serviceInfoModal.Root == null)
            {
                return false;
            }

            serviceInfoModal.SetContent(
                "Тест выдачи",
                "Запускается тест хоппера...",
                "Хоппер");
            modalStack?.Open(serviceInfoModal);
            RunHopperTestAsync(this.GetCancellationTokenOnDestroy()).Forget(Debug.LogException);
            return true;
        }

        private async UniTask RunHopperTestAsync(CancellationToken cancellationToken)
        {
            ResolveComSystemIfNeeded();

            if (comSystem == null)
            {
                audioService?.Play(GameAudioEvent.UiError);
                serviceInfoModal?.SetContent(
                    "Тест выдачи",
                    "Ошибка: COM-система недоступна. Проверь подключение платы и запуск COM-сервиса.",
                    "Хоппер");
                return;
            }

            try
            {
                serviceInfoModal?.SetContent(
                    "Тест выдачи",
                    "Хоппер включен. Ожидаю срабатывание датчика выдачи...",
                    "Хоппер");

                var result = await ComHopperDispenser.DispenseAsync(comSystem, cancellationToken);
                audioService?.Play(result.IsSuccess ? GameAudioEvent.UiSubmit : GameAudioEvent.UiError);
                serviceInfoModal?.SetContent(
                    "Тест выдачи",
                    result.Message,
                    "Хоппер");
            }
            catch (OperationCanceledException)
            {
                serviceInfoModal?.SetContent(
                    "Тест выдачи",
                    "Тест хоппера отменен.",
                    "Хоппер");
            }
        }

        private bool TryOpenServiceTextInput(ServiceSettingsActionResult result)
        {
            if (serviceSettingsService == null ||
                serviceTextInputModal == null ||
                serviceTextInputModal.Root == null ||
                string.IsNullOrWhiteSpace(result.FocusKey) ||
                !serviceSettingsService.TryGetDefinition(result.FocusKey, out var targetDefinition) ||
                targetDefinition.ValueType != ServiceSettingValueType.String)
            {
                return false;
            }

            var currentValue = serviceSettingsService.GetString(targetDefinition.Key);
            var profile = ResolveTextInputProfile(targetDefinition.Key);
            serviceTextInputModal.SetContext(
                result.Title,
                targetDefinition.DisplayName,
                currentValue,
                profile,
                value =>
                {
                    if (serviceSettingsService.SetString(targetDefinition.Key, value))
                    {
                        serviceSettingsService.Save();
                        RefreshStaticLabels();
                    }
                });
            modalStack?.Open(serviceTextInputModal);
            return true;
        }

        private bool TryOpenQrSettings(ServiceSettingsActionResult result)
        {
            if (!string.Equals(result.FocusKey, ServiceSettingsKeys.QrPrizeTextSettings, System.StringComparison.Ordinal))
            {
                return false;
            }

            ResolveServiceSettingsIfNeeded();
            if (qrCodeSettingsModal == null || qrCodeSettingsModal.Root == null)
            {
                return false;
            }

            qrCodeSettingsModal.SetSettingsService(serviceSettingsService);
            modalStack?.Open(qrCodeSettingsModal);
            return true;
        }

        private bool TryOpenPrizeLevels(ServiceSettingsActionResult result)
        {
            if (!string.Equals(result.FocusKey, ServiceSettingsKeys.PrizeLevels, System.StringComparison.Ordinal))
            {
                return false;
            }

            ResolveServiceSettingsIfNeeded();
            if (prizeLevelsModal == null || prizeLevelsModal.Root == null)
            {
                return false;
            }

            prizeLevelsModal.SetSettingsService(serviceSettingsService);
            modalStack?.Open(prizeLevelsModal);
            return true;
        }

        private void OpenPrizeLevelAddModal()
        {
            if (prizeLevelAddModal == null || prizeLevelAddModal.Root == null)
            {
                return;
            }

            prizeLevelAddModal.SetInitialScore(prizeLevelsModal != null ? prizeLevelsModal.GetNextSuggestedScore() : 100);
            modalStack?.Open(prizeLevelAddModal);
        }

        private void OpenQrPrizeTextInput(QrPrizeTier tier, string settingKey)
        {
            if (serviceSettingsService == null ||
                serviceTextInputModal == null ||
                serviceTextInputModal.Root == null ||
                string.IsNullOrWhiteSpace(settingKey) ||
                !serviceSettingsService.TryGetDefinition(settingKey, out var targetDefinition) ||
                targetDefinition.ValueType != ServiceSettingValueType.String)
            {
                return;
            }

            var currentValue = QrPrizeTextSettings.NormalizePrizeText(serviceSettingsService.GetString(settingKey));
            var profile = ArcadeStringInputProfile.Text(QrPrizeTextSettings.MaxPrizeTextLength);
            serviceTextInputModal.SetContext(
                targetDefinition,
                currentValue,
                profile,
                value =>
                {
                    var normalized = QrPrizeTextSettings.NormalizePrizeText(value);
                    if (serviceSettingsService.SetString(settingKey, normalized))
                    {
                        serviceSettingsService.Save();
                        qrCodeSettingsModal?.SetSettingsService(serviceSettingsService);
                        RefreshStaticLabels();
                    }
                });
            modalStack?.Open(serviceTextInputModal);
        }

        private void HandlePrizeLevelSaved(int score, PrizeRewardKind rewardKind)
        {
            prizeLevelsModal?.SetSettingsService(serviceSettingsService);
            prizeLevelsModal?.AddOrUpdateLevel(score, rewardKind);
        }

        private static ArcadeStringInputProfile ResolveTextInputProfile(string settingKey)
        {
            return settingKey switch
            {
                ServiceSettingsKeys.SettingsPin => new ArcadeStringInputProfile("0123456789", 4),
                ServiceSettingsKeys.SupportNumber => ArcadeStringInputProfile.Phone(),
                ServiceSettingsKeys.PrizePhoneNumber => ArcadeStringInputProfile.Phone(),
                ServiceSettingsKeys.Currency => ArcadeStringInputProfile.ShortText(maxLength: 10),
                ServiceSettingsKeys.BigPrize => ArcadeStringInputProfile.Text(maxLength: 28),
                ServiceSettingsKeys.QrPrizeTextBronze => ArcadeStringInputProfile.Text(
                    maxLength: QrPrizeTextSettings.MaxPrizeTextLength),
                ServiceSettingsKeys.QrPrizeTextSilver => ArcadeStringInputProfile.Text(
                    maxLength: QrPrizeTextSettings.MaxPrizeTextLength),
                ServiceSettingsKeys.QrPrizeTextGold => ArcadeStringInputProfile.Text(
                    maxLength: QrPrizeTextSettings.MaxPrizeTextLength),
                ServiceSettingsKeys.QrMessageTemplate => ArcadeStringInputProfile.Template(),
                _ => ArcadeStringInputProfile.Text()
            };
        }

        /// <summary>
        /// Подписывает кнопки главного меню на действия.
        /// </summary>
        private void RegisterCallbacks()
        {
            if (startButton != null)
            {
                startButton.clicked += StartGame;
            }

            if (rulesButton != null)
            {
                rulesButton.clicked += OpenRules;
            }

            if (settingsButton != null)
            {
                settingsButton.clicked += OpenSettings;
            }

            if (serviceButton != null)
            {
                serviceButton.clicked += OpenServicePanel;
            }

            if (quitButton != null)
            {
                quitButton.clicked += QuitApplication;
            }
        }

        /// <summary>
        /// Отписывает кнопки главного меню от действий.
        /// </summary>
        private void UnregisterCallbacks()
        {
            if (startButton != null)
            {
                startButton.clicked -= StartGame;
            }

            if (rulesButton != null)
            {
                rulesButton.clicked -= OpenRules;
            }

            if (settingsButton != null)
            {
                settingsButton.clicked -= OpenSettings;
            }

            if (serviceButton != null)
            {
                serviceButton.clicked -= OpenServicePanel;
            }

            if (quitButton != null)
            {
                quitButton.clicked -= QuitApplication;
            }
        }

        /// <summary>
        /// Подключает визуальную реакцию кнопок на фокус.
        /// </summary>
        private void RegisterButtonFocusCallbacks()
        {
            foreach (var button in menuButtons)
            {
                button.focusable = true;
                button.style.unityBackgroundImageTintColor = new StyleColor(normalButtonImageTint);
                button.RegisterCallback<FocusInEvent>(HandleButtonFocusIn);
                button.RegisterCallback<FocusOutEvent>(HandleButtonFocusOut);
            }
        }

        /// <summary>
        /// Отключает обработчики фокуса, чтобы повторное включение меню не создавало дубликаты.
        /// </summary>
        private void UnregisterButtonFocusCallbacks()
        {
            foreach (var button in menuButtons)
            {
                button.UnregisterCallback<FocusInEvent>(HandleButtonFocusIn);
                button.UnregisterCallback<FocusOutEvent>(HandleButtonFocusOut);
            }
        }

        /// <summary>
        /// Подсвечивает кнопку, на которую попал UI-фокус.
        /// </summary>
        private void HandleButtonFocusIn(FocusInEvent evt)
        {
            if (evt.currentTarget is Button button)
            {
                audioService?.Play(GameAudioEvent.UiNavigate);
                button.style.unityBackgroundImageTintColor = new StyleColor(focusedButtonImageTint);
            }
        }

        /// <summary>
        /// Возвращает обычный tint кнопки после потери UI-фокуса.
        /// </summary>
        private void HandleButtonFocusOut(FocusOutEvent evt)
        {
            if (evt.currentTarget is Button button)
            {
                button.style.unityBackgroundImageTintColor = new StyleColor(normalButtonImageTint);
            }
        }

        /// <summary>
        /// Ждет один кадр, чтобы UI Toolkit успел построить панель перед установкой фокуса.
        /// </summary>
        private async UniTask FocusDefaultButtonNextFrameAsync(CancellationToken cancellationToken)
        {
            try
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                if (!isActiveAndEnabled || root == null)
                {
                    return;
                }

                StartupWindowFocusController.FocusGameWindow();
                FocusDefaultButton();
            }
            catch (System.OperationCanceledException)
            {
            }
        }

        /// <summary>
        /// Ставит фокус на кнопку по умолчанию.
        /// </summary>
        private void FocusDefaultButton()
        {
            if (menuButtons.Count == 0)
            {
                return;
            }

            var defaultButton = !string.IsNullOrWhiteSpace(defaultFocusedButtonName)
                ? menuButtons.Find(button => button.name == defaultFocusedButtonName)
                : null;

            var buttonToFocus = defaultButton ?? menuButtons[0];
            buttonToFocus.Focus();
            EventSystem.current?.SetSelectedGameObject(document != null ? document.gameObject : gameObject);
        }

        /// <summary>
        /// Обновляет статические подписи главного меню из версии приложения и сервисных настроек.
        /// </summary>
        private void RefreshStaticLabels()
        {
            if (versionLabel != null)
            {
                versionLabel.text = $"Версия: {Application.version}";
            }

            if (serviceSettings == null)
            {
                UpdateCreditsLabel();
                RefreshSupportPhoneLabel();
                RefreshStartButtonState();
                return;
            }

            var startingLives = Mathf.Max(0, serviceSettings.GetInt(ServiceSettingsKeys.StartingLives));
            var isFreePlay = serviceSettings.GetBool(ServiceSettingsKeys.FreePlay);
            var gamePrice = isFreePlay ? 0 : Mathf.Max(0, serviceSettings.GetInt(ServiceSettingsKeys.StartGameCost));

            if (livesValueLabel != null)
            {
                livesValueLabel.text = startingLives.ToString();
            }

            if (creditsValueLabel != null)
            {
                UpdateCreditsLabel();
            }

            if (exchangeLivesValueLabel != null)
            {
                exchangeLivesValueLabel.text = $"= {startingLives}";
            }

            if (priceTextLabel != null)
            {
                priceTextLabel.text = gamePrice <= 0 ? "БЕСПЛАТНО" : $"{gamePrice} РУБ.";
            }

            RefreshSupportPhoneLabel();
            RefreshStartButtonState();
        }

        private void RefreshSupportPhoneLabel()
        {
            if (supportPhoneLabel == null || serviceSettings == null)
            {
                return;
            }

            var showSupportNumber = serviceSettings.GetBool(ServiceSettingsKeys.ShowSupportNumber);
            var supportNumber = serviceSettings.GetString(ServiceSettingsKeys.SupportNumber);
            var hasSupportNumber = !string.IsNullOrWhiteSpace(supportNumber);

            supportPhoneLabel.style.display = showSupportNumber && hasSupportNumber
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            if (showSupportNumber && hasSupportNumber)
            {
                supportPhoneLabel.text = $"{SupportPhonePrefix} {supportNumber.Trim()}";
            }
        }

        /// <summary>
        /// Ищет сервис настроек в ProjectContext, если он не был внедрен напрямую через Extenject.
        /// </summary>
        private void ResolveServiceSettingsIfNeeded()
        {
            if (serviceSettings != null && serviceSettingsService != null)
            {
                return;
            }

            try
            {
                var projectContext = ProjectContext.Instance;
                if (projectContext == null || projectContext.Container == null)
                {
                    return;
                }

                if (serviceSettingsService == null &&
                    projectContext.Container.HasBinding<IServiceSettingsService>())
                {
                    serviceSettingsService = projectContext.Container.Resolve<IServiceSettingsService>();
                }

                if (serviceSettings == null && serviceSettingsService != null)
                {
                    serviceSettings = serviceSettingsService;
                }

                if (serviceSettings == null &&
                    projectContext.Container.HasBinding<IReadOnlyServiceSettings>())
                {
                    serviceSettings = projectContext.Container.Resolve<IReadOnlyServiceSettings>();
                }

                if (sceneFlow == null &&
                    projectContext.Container.HasBinding<ISceneFlowService>())
                {
                    sceneFlow = projectContext.Container.Resolve<ISceneFlowService>();
                }

                if (creditWallet == null && projectContext.Container.HasBinding<IComSystem>())
                {
                    comSystem = projectContext.Container.Resolve<IComSystem>();
                    creditWallet = comSystem;
                }

                if (comSystem == null && projectContext.Container.HasBinding<IComSystem>())
                {
                    comSystem = projectContext.Container.Resolve<IComSystem>();
                }

                if (creditWallet == null && projectContext.Container.HasBinding<IComCreditWallet>())
                {
                    creditWallet = projectContext.Container.Resolve<IComCreditWallet>();
                }

                if (audioService == null && projectContext.Container.HasBinding<IGameAudioService>())
                {
                    audioService = projectContext.Container.Resolve<IGameAudioService>();
                }

                if (prizeSessionState == null &&
                    projectContext.Container.HasBinding<IPrizeSessionStateService>())
                {
                    prizeSessionState = projectContext.Container.Resolve<IPrizeSessionStateService>();
                    settingsModal?.SetPrizeSessionStateService(prizeSessionState);
                }
            }
            catch (ZenjectException exception)
            {
                Debug.LogWarning($"[{nameof(MainMenuController)}] Service settings are not available yet: {exception.Message}", this);
            }
        }

        private void ResolveCreditWalletIfNeeded()
        {
            if (creditWallet != null)
            {
                return;
            }

            ResolveServiceSettingsIfNeeded();
            creditWallet ??= comSystem ?? ComSystem.Instance;
        }

        private void ResolveComSystemIfNeeded()
        {
            if (comSystem != null)
            {
                return;
            }

            ResolveServiceSettingsIfNeeded();
            comSystem ??= ComSystem.Instance;
        }

        private void RegisterCreditCallbacks()
        {
            if (creditsChangedSubscribed)
            {
                return;
            }

            ResolveCreditWalletIfNeeded();
            if (creditWallet == null)
            {
                UpdateCreditsLabel();
                return;
            }

            creditWallet.CreditsChanged.AddListener(HandleCreditsChanged);
            creditsChangedSubscribed = true;
            UpdateCreditsLabel(creditWallet.Credits);
            RefreshStartButtonState();
        }

        private void UnregisterCreditCallbacks()
        {
            if (!creditsChangedSubscribed || creditWallet == null)
            {
                creditsChangedSubscribed = false;
                return;
            }

            creditWallet.CreditsChanged.RemoveListener(HandleCreditsChanged);
            creditsChangedSubscribed = false;
        }

        private void HandleCreditsChanged(int credits)
        {
            UpdateCreditsLabel(credits);
            RefreshStartButtonState(credits);
        }

        private void UpdateCreditsLabel()
        {
            UpdateCreditsLabel(creditWallet != null ? creditWallet.Credits : 0);
        }

        private void UpdateCreditsLabel(int credits)
        {
            if (creditsValueLabel != null)
            {
                creditsValueLabel.text = Mathf.Max(0, credits).ToString();
            }
        }

        private void RegisterServiceSettingsCallbacks()
        {
            if (serviceSettingsChangedSubscribed)
            {
                return;
            }

            ResolveServiceSettingsIfNeeded();
            if (serviceSettings == null)
            {
                RefreshStartButtonState();
                return;
            }

            serviceSettings.SettingChanged += HandleServiceSettingChanged;
            serviceSettings.SettingsLoaded += HandleServiceSettingsLoaded;
            serviceSettingsChangedSubscribed = true;
        }

        private void UnregisterServiceSettingsCallbacks()
        {
            if (!serviceSettingsChangedSubscribed || serviceSettings == null)
            {
                serviceSettingsChangedSubscribed = false;
                return;
            }

            serviceSettings.SettingChanged -= HandleServiceSettingChanged;
            serviceSettings.SettingsLoaded -= HandleServiceSettingsLoaded;
            serviceSettingsChangedSubscribed = false;
        }

        private void HandleServiceSettingsLoaded()
        {
            RefreshStaticLabels();
        }

        private void HandleServiceSettingChanged(string key, ServiceSettingValue value)
        {
            if (!IsMainMenuDisplaySetting(key))
            {
                return;
            }

            RefreshStaticLabels();
        }

        private static bool IsMainMenuDisplaySetting(string key)
        {
            return string.Equals(key, ServiceSettingsKeys.FreePlay, System.StringComparison.Ordinal) ||
                string.Equals(key, ServiceSettingsKeys.StartGameCost, System.StringComparison.Ordinal) ||
                string.Equals(key, ServiceSettingsKeys.StartingLives, System.StringComparison.Ordinal) ||
                string.Equals(key, ServiceSettingsKeys.ShowSupportNumber, System.StringComparison.Ordinal) ||
                string.Equals(key, ServiceSettingsKeys.SupportNumber, System.StringComparison.Ordinal);
        }

        private void RefreshStartButtonState()
        {
            RefreshStartButtonState(creditWallet != null ? creditWallet.Credits : 0);
        }

        private void RefreshStartButtonState(int balance)
        {
            if (startButton == null)
            {
                return;
            }

            var canStart = CanStartGame(Mathf.Max(0, balance));
            startButton.SetEnabled(canStart);
        }

        private bool CanStartGame(int balance)
        {
            var startCost = ResolveStartGameCost();
            return startCost <= 0 || balance >= startCost;
        }

        private int ResolveStartGameCost()
        {
            if (serviceSettings == null || serviceSettings.GetBool(ServiceSettingsKeys.FreePlay))
            {
                return 0;
            }

            return Mathf.Max(0, serviceSettings.GetInt(ServiceSettingsKeys.StartGameCost));
        }

        private bool TrySpendStartBalance()
        {
            var startCost = ResolveStartGameCost();
            if (startCost <= 0)
            {
                return true;
            }

            ResolveCreditWalletIfNeeded();
            if (creditWallet == null)
            {
                RefreshStartButtonState(0);
                return false;
            }

            if (creditWallet.TrySpendCredits(startCost))
            {
                return true;
            }

            RefreshStartButtonState(creditWallet.Credits);
            return false;
        }

        /// <summary>
        /// Ищет централизованный scene-flow сервис в ProjectContext.
        /// </summary>
        private void ResolveSceneFlowIfNeeded()
        {
            if (sceneFlow != null)
            {
                return;
            }

            ResolveServiceSettingsIfNeeded();
            if (sceneFlow != null)
            {
                return;
            }

            if (sceneFlow == null)
            {
                Debug.LogWarning($"{nameof(MainMenuController)} requires {nameof(ISceneFlowService)} from ProjectContext DI.", this);
            }
        }

        /// <summary>
        /// Загружает игровую сцену из главного меню.
        /// </summary>
        private void StartGame()
        {
            ResolveCreditWalletIfNeeded();
            if (!CanStartGame(creditWallet != null ? creditWallet.Credits : 0))
            {
                RefreshStartButtonState();
                return;
            }

            ResolveSceneFlowIfNeeded();
            if (sceneFlow == null || !sceneFlow.SceneExists(gameplaySceneName))
            {
                Debug.LogWarning($"Gameplay scene '{gameplaySceneName}' is not available in Build Settings yet.");
                return;
            }

            if (!TrySpendStartBalance())
            {
                audioService?.Play(GameAudioEvent.UiError);
                return;
            }

            audioService?.Play(GameAudioEvent.GameStart);
            audioService?.PlayMusic(GameMusicCue.Gameplay);
            sceneFlow.StartGameplay(gameplaySceneName);
        }

        /// <summary>
        /// Открывает окно правил.
        /// </summary>
        private void OpenRules()
        {
            audioService?.Play(GameAudioEvent.UiSubmit);
            modalStack?.Open(rulesModal);
        }

        /// <summary>
        /// Открывает модальное окно настроек или fallback-сцену настроек, если окно недоступно.
        /// </summary>
        private void OpenSettings()
        {
            audioService?.Play(GameAudioEvent.UiSubmit);
            ResolveServiceSettingsIfNeeded();

            bool requirePin = serviceSettings == null || serviceSettings.GetBool(ServiceSettingsKeys.RequireSettingsPin);
            string correctPin = serviceSettings != null ? serviceSettings.GetString(ServiceSettingsKeys.SettingsPin) : "1234";
            if (string.IsNullOrEmpty(correctPin))
            {
                correctPin = "1234";
            }

            if (requirePin && serviceTextInputModal != null && serviceTextInputModal.Root != null)
            {
                var profile = new ArcadeStringInputProfile("0123456789", 4);
                serviceTextInputModal.SetContext(
                    "Авторизация",
                    "Введите PIN-код для доступа к настройкам",
                    "",
                    profile,
                    pin =>
                    {
                        if (pin == correctPin)
                        {
                            if (settingsModal != null && settingsModal.Root != null)
                            {
                                settingsModal.SetSettingsService(serviceSettingsService);
                                modalStack?.Open(settingsModal);
                            }
                        }
                        else
                        {
                            audioService?.Play(GameAudioEvent.UiError);
                        }
                    });
                modalStack?.Open(serviceTextInputModal);
                return;
            }

            if (settingsModal != null && settingsModal.Root != null)
            {
                settingsModal.SetSettingsService(serviceSettingsService);
                modalStack?.Open(settingsModal);
                return;
            }

            ResolveSceneFlowIfNeeded();
            if (!string.IsNullOrWhiteSpace(settingsSceneName) &&
                sceneFlow != null &&
                sceneFlow.SceneExists(settingsSceneName))
            {
                sceneFlow.OpenSettingsScene(settingsSceneName);
                return;
            }

            Debug.Log("Main menu settings entry selected. Service settings UI will be connected later.");
        }

        /// <summary>
        /// Открывает сервисную панель. Сейчас использует то же окно, что и настройки.
        /// </summary>
        private void OpenServicePanel()
        {
            OpenSettings();
        }

        /// <summary>
        /// Подключает сервисные Input System actions главного меню.
        /// </summary>
        private void RegisterInputActions()
        {
#if ENABLE_INPUT_SYSTEM
            if (secretAction != null)
            {
                return;
            }

            secretAction = ResolveInputAction(secretActionReference, LegacySecretActionPath);
            if (secretAction == null)
            {
                Debug.LogWarning($"[{nameof(MainMenuController)}] Secret action is not assigned.", this);
                return;
            }

            secretActionWasEnabled = secretAction.enabled;
            secretAction.performed += HandleSecretActionPerformed;

            // Включаем action только если он не был активен до регистрации меню.
            if (!secretAction.enabled)
            {
                secretAction.Enable();
            }
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private InputAction ResolveInputAction(InputActionReference reference, string legacyActionPath)
        {
            if (reference != null && reference.action != null)
            {
                return reference.action;
            }

            return legacyInputActionsAsset != null
                ? legacyInputActionsAsset.FindAction(legacyActionPath, false)
                : null;
        }
#endif

        /// <summary>
        /// Отключает Input System actions и возвращает их исходное состояние.
        /// </summary>
        private void UnregisterInputActions()
        {
#if ENABLE_INPUT_SYSTEM
            if (secretAction == null)
            {
                return;
            }

            secretAction.performed -= HandleSecretActionPerformed;

            if (!secretActionWasEnabled)
            {
                secretAction.Disable();
            }

            secretAction = null;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        /// <summary>
        /// Открывает окно настроек по action path UI/Secret.
        /// </summary>
        private void HandleSecretActionPerformed(InputAction.CallbackContext context)
        {
            OpenSettings();
        }
#endif

        /// <summary>
        /// Завершает приложение или останавливает Play Mode в редакторе.
        /// </summary>
        private void QuitApplication()
        {
            audioService?.Play(GameAudioEvent.UiBack);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

    }
}
