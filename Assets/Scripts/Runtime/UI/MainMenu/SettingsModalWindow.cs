using System;
using System.Collections.Generic;
using DanroJump.Prizes;
using DanroJump.Settings;
using DanroJump.UI.Modals;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DanroJump.UI.MainMenu
{
    /// <summary>
    /// Модальное окно сервисных настроек главного меню.
    /// Строит список полей динамически по базе <see cref="ServiceSettingsDatabase"/>.
    /// </summary>
    public sealed class SettingsModalWindow : ModalWindowBase, IDisposable
    {
        private const string GroupHeaderTemplateAssetPath = "Assets/UI/MainMenu/Templates/SettingsGroupHeader.uxml";
        private const string BoolControlTemplateAssetPath = "Assets/UI/MainMenu/Templates/SettingsBoolControl.uxml";
        private const string StepperControlTemplateAssetPath = "Assets/UI/MainMenu/Templates/SettingsStepperControl.uxml";
        private const string TextControlTemplateAssetPath = "Assets/UI/MainMenu/Templates/SettingsTextControl.uxml";
        private const string ButtonControlTemplateAssetPath = "Assets/UI/MainMenu/Templates/SettingsButtonControl.uxml";
        private const string GroupHeaderRootName = "SettingsGroupHeaderRoot";
        private const string GroupHeaderTitleName = "SettingsGroupTitle";
        private const string GroupHeaderDisclosureName = "SettingsGroupDisclosure";
        private const string BoolControlRootName = "SettingsBoolControlRoot";
        private const string StepperControlRootName = "SettingsStepperControlRoot";
        private const string StepperPreviousButtonName = "SettingsStepperPreviousButton";
        private const string StepperValueLabelName = "SettingsStepperValueLabel";
        private const string StepperNextButtonName = "SettingsStepperNextButton";
        private const string TextControlRootName = "SettingsTextControlRoot";
        private const string TextFieldName = "SettingsTextField";
        private const string ButtonControlRootName = "SettingsButtonControlRoot";
        private const string ActionButtonName = "SettingsActionButton";
        private const string ControlTitleName = "SettingsControlTitle";
        private const string BoolIndicatorName = "SettingsBoolIndicator";
        private const float FocusPointerGap = 18f;
        private const float FocusPointerMinLeft = 12f;
        private const float FocusPointerWidth = 46f;
        private const float FocusPointerHeight = 64f;
        private const float FocusPointerAnimationDuration = 0.12f;
        private const int FocusPointerAnimationFrameMs = 16;
        private const float ScrollVisibilityPadding = 18f;
        private const float ScrollOffsetEpsilon = 0.5f;
        private const float VerticalNavigationCooldown = 0.18f;
        private const float DuplicateNavigationSourceWindow = 0.3f;
        private const float RelativeSpacingFallbackWidth = 620f;
        private const float GroupHeaderTopSpacingRatio = 0.035f;
        private const float GroupHeaderBottomSpacingRatio = 0.019f;
        private const float SettingControlVerticalSpacingRatio = 0.01f;
        private const float ActionButtonVerticalSpacingRatio = 0.019f;
        private const float GroupHeaderTopSpacingMin = 16f;
        private const float GroupHeaderTopSpacingMax = 34f;
        private const float GroupHeaderBottomSpacingMin = 8f;
        private const float GroupHeaderBottomSpacingMax = 20f;
        private const float SettingControlVerticalSpacingMin = 4f;
        private const float SettingControlVerticalSpacingMax = 10f;
        private const float ActionButtonVerticalSpacingMin = 8f;
        private const float ActionButtonVerticalSpacingMax = 20f;

        private static VisualTreeAsset groupHeaderTemplate;
        private static VisualTreeAsset boolControlTemplate;
        private static VisualTreeAsset stepperControlTemplate;
        private static VisualTreeAsset textControlTemplate;
        private static VisualTreeAsset buttonControlTemplate;
        private static AsyncOperationHandle<VisualTreeAsset>? groupHeaderTemplateHandle;
        private static AsyncOperationHandle<VisualTreeAsset>? boolControlTemplateHandle;
        private static AsyncOperationHandle<VisualTreeAsset>? stepperControlTemplateHandle;
        private static AsyncOperationHandle<VisualTreeAsset>? textControlTemplateHandle;
        private static AsyncOperationHandle<VisualTreeAsset>? buttonControlTemplateHandle;

        private readonly ScrollView settingsList;
        private readonly Label statusLabel;
        private readonly Label currentGroupTitleLabel;
        private readonly Button saveButton;
        private readonly Button resetButton;
        private readonly Button reloadButton;
        private readonly Button closeButton;
        private readonly Dictionary<string, VisualElement> groupHeaders = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<VisualElement>> groupRows = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Label> groupDisclosureLabels = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> collapsedGroups = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<VisualElement, SettingAdjustment> settingAdjustments = new();
        private readonly Dictionary<VisualElement, Action> settingActivations = new();
        private readonly List<VisualElement> settingsNavigationTargets = new();
        private readonly List<VisualElement> settingControls = new();
        private readonly Label focusPointer;
        private VisualElement firstFocusableControl;
        private VisualElement currentFocusPointerTarget;
        private IVisualElementScheduledItem focusPointerAnimation;
        private Vector2 focusPointerPosition;
        private bool hasFocusPointerPosition;
        private int lastNavigationFrame = -1;
        private SettingsNavigationCommand lastNavigationCommand;
        private SettingsNavigationSource lastNavigationSource;
        private float lastNavigationCommandTime = -DuplicateNavigationSourceWindow;
        private float lastVerticalNavigationTime = -VerticalNavigationCooldown;
        private IServiceSettingsService settingsService;
        private IPrizeSessionStateService prizeSessionState;

        public event Action<ServiceSettingDefinition, ServiceSettingsActionResult> ServiceActionActivated;

        /// <summary>
        /// Контейнер реального содержимого ScrollView.
        /// Нужен, чтобы не очищать внутреннюю служебную структуру UI Toolkit.
        /// </summary>
        private VisualElement SettingsContainer => settingsList?.contentContainer ?? settingsList;

        /// <summary>
        /// Создает окно настроек и привязывает кнопки управления к сервису настроек.
        /// </summary>
        /// <param name="root">Корневой VisualElement окна в UXML.</param>
        /// <param name="settingsService">Сервис чтения, применения и сохранения настроек.</param>
        public SettingsModalWindow(
            VisualElement root,
            IServiceSettingsService settingsService,
            IPrizeSessionStateService prizeSessionState = null)
            : base(root)
        {
            if (root == null)
            {
                return;
            }

            settingsList = root.Q<ScrollView>("SettingsList");
            statusLabel = root.Q<Label>("SettingsStatusLabel");
            currentGroupTitleLabel = root.Q<Label>("SettingsGroupTitleLabel");
            saveButton = root.Q<Button>("SettingsSaveButton");
            resetButton = root.Q<Button>("SettingsResetButton");
            reloadButton = root.Q<Button>("SettingsReloadButton");
            closeButton = root.Q<Button>("SettingsCloseButton");
            focusPointer = CreateModalFocusPointer(root);
            root.RegisterCallback<FocusInEvent>(HandleRootFocusIn, TrickleDown.TrickleDown);
            root.RegisterCallback<FocusOutEvent>(HandleRootFocusOut, TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationSubmitEvent>(HandleNavigationSubmit, TrickleDown.TrickleDown);
            settingsList?.RegisterCallback<GeometryChangedEvent>(HandleSettingsListGeometryChanged);

            // Кнопки работают с текущим сервисом настроек, который может прийти позже через DI fallback.
            if (saveButton != null)
            {
                saveButton.clicked += SaveSettings;
            }

            if (resetButton != null)
            {
                resetButton.clicked += ResetSettings;
            }

            if (reloadButton != null)
            {
                reloadButton.clicked += ReloadSettings;
            }

            if (closeButton != null)
            {
                closeButton.clicked += RequestClose;
            }

            this.prizeSessionState = prizeSessionState;
            SetSettingsService(settingsService);
        }

        public void SetPrizeSessionStateService(IPrizeSessionStateService service)
        {
            prizeSessionState = service;
        }

        /// <summary>
        /// Обновляет ссылку на сервис настроек перед открытием окна.
        /// </summary>
        /// <param name="service">Актуальный сервис настроек из ProjectContext.</param>
        public void SetSettingsService(IServiceSettingsService service)
        {
            settingsService = service;
        }

        /// <summary>
        /// Перестраивает список полей при каждом открытии и затем показывает модальное окно.
        /// </summary>
        public override void Open()
        {
            BuildSettingsList();
            base.Open();
            ScheduleRelativeSpacingUpdate();
            Root?.schedule.Execute(ShowFocusPointerOnCurrentOrDefault);
        }

        /// <summary>
        /// Закрывает окно настроек и скрывает общий указатель фокуса.
        /// </summary>
        public override void Close()
        {
            currentFocusPointerTarget = null;
            hasFocusPointerPosition = false;
            ResetNavigationState();
            focusPointerAnimation?.Pause();
            HideFocusPointer();
            base.Close();
        }

        /// <summary>
        /// Отписывает обработчики кнопок, когда окно больше не используется контроллером меню.
        /// </summary>
        public void Dispose()
        {
            if (saveButton != null)
            {
                saveButton.clicked -= SaveSettings;
            }

            if (resetButton != null)
            {
                resetButton.clicked -= ResetSettings;
            }

            if (reloadButton != null)
            {
                reloadButton.clicked -= ReloadSettings;
            }

            if (closeButton != null)
            {
                closeButton.clicked -= RequestClose;
            }

            if (Root != null)
            {
                Root.UnregisterCallback<FocusInEvent>(HandleRootFocusIn, TrickleDown.TrickleDown);
                Root.UnregisterCallback<FocusOutEvent>(HandleRootFocusOut, TrickleDown.TrickleDown);
                Root.UnregisterCallback<NavigationSubmitEvent>(HandleNavigationSubmit, TrickleDown.TrickleDown);
            }

            settingsList?.UnregisterCallback<GeometryChangedEvent>(HandleSettingsListGeometryChanged);
        }

        /// <summary>
        /// Возвращает первый редактируемый контрол, чтобы ModalStackController удерживал фокус внутри окна.
        /// </summary>
        /// <returns>Первый доступный контрол настроек или кнопка сохранения/закрытия.</returns>
        protected override VisualElement GetDefaultFocusElement()
        {
            return firstFocusableControl ?? saveButton ?? closeButton;
        }

        /// <summary>
        /// Полностью пересобирает UI настроек из базы данных.
        /// </summary>
        private void BuildSettingsList()
        {
            firstFocusableControl = null;
            ResetNavigationState();
            currentFocusPointerTarget = null;
            hasFocusPointerPosition = false;
            groupHeaders.Clear();
            groupRows.Clear();
            groupDisclosureLabels.Clear();
            collapsedGroups.Clear();
            settingAdjustments.Clear();
            settingActivations.Clear();
            settingsNavigationTargets.Clear();
            settingControls.Clear();

            if (settingsList == null)
            {
                return;
            }

            SettingsContainer.Clear();

            if (settingsService == null || settingsService.Database == null)
            {
                AddStatus("Сервисные настройки пока недоступны.");
                return;
            }

            var groupedDefinitions = new Dictionary<string, List<ServiceSettingDefinition>>(StringComparer.OrdinalIgnoreCase);
            var groupOrder = new List<string>();

            // Порядок и видимость настроек задаются в ServiceSettingsDatabase, а не в коде окна.
            foreach (var definition in settingsService.Database.Entries)
            {
                if (!CanRender(definition))
                {
                    continue;
                }

                var groupName = GetGroupName(definition.Group);
                if (!groupedDefinitions.TryGetValue(groupName, out var definitions))
                {
                    definitions = new List<ServiceSettingDefinition>();
                    groupedDefinitions[groupName] = definitions;
                    groupOrder.Add(groupName);
                }

                definitions.Add(definition);
            }

            if (groupOrder.Count == 0)
            {
                AddStatus("В базе настроек нет параметров, разрешённых для сервисного меню.");
                return;
            }

            foreach (var groupName in groupOrder)
            {
                AddGroupHeader(groupName);

                foreach (var definition in groupedDefinitions[groupName])
                {
                    AddSettingRow(definition, groupName);
                }
            }

            SetStatus("Изменения применяются сразу. Кнопка «Сохранить» записывает их в файл настроек.");
            AddFooterNavigationTargets();
            UpdateRelativeSettingsSpacing();
            SetCurrentSelection(groupOrder[0], groupOrder[0], null, isGroup: true);
        }

        /// <summary>
        /// В окне настроек горизонтальная навигация меняет значение настройки, а не фокус.
        /// </summary>
        /// <param name="current">Текущий сфокусированный элемент.</param>
        /// <param name="direction">Направление UI Toolkit navigation-события.</param>
        /// <returns>True, если событие обработано окном настроек.</returns>
        public override bool HandleNavigationMove(VisualElement current, NavigationMoveEvent.Direction direction)
        {
            if (IsInsideTextField(current))
            {
                return false;
            }

            return TryGetNavigationCommand(direction, out var command) &&
                HandleSettingsNavigationCommand(current, command, SettingsNavigationSource.NavigationMove);
        }

        /// <summary>
        /// Клавиши Left/Right и A/D меняют значение текущей настройки.
        /// </summary>
        /// <param name="current">Текущий сфокусированный элемент.</param>
        /// <param name="keyCode">Код нажатой клавиши.</param>
        /// <returns>True, если клавиша обработана окном настроек.</returns>
        public override bool HandleNavigationKey(VisualElement current, KeyCode keyCode)
        {
            if (IsInsideTextField(current))
            {
                return false;
            }

            return TryGetNavigationCommand(keyCode, out var command) &&
                HandleSettingsNavigationKey(current, command);
        }

        /// <summary>
        /// Обрабатывает Submit от UI Toolkit/геймпада на фокусируемой строке настройки.
        /// </summary>
        private void HandleNavigationSubmit(NavigationSubmitEvent evt)
        {
            if (evt.target is VisualElement current &&
                !IsInsideTextField(current) &&
                ActivateFocusedSetting(current))
            {
                evt.StopImmediatePropagation();
            }
        }

        /// <summary>
        /// Переводит направление UI Toolkit в команду сервисного меню.
        /// </summary>
        private static bool TryGetNavigationCommand(
            NavigationMoveEvent.Direction direction,
            out SettingsNavigationCommand command)
        {
            switch (direction)
            {
                case NavigationMoveEvent.Direction.Up:
                    command = SettingsNavigationCommand.Up;
                    return true;
                case NavigationMoveEvent.Direction.Down:
                    command = SettingsNavigationCommand.Down;
                    return true;
                case NavigationMoveEvent.Direction.Left:
                    command = SettingsNavigationCommand.Left;
                    return true;
                case NavigationMoveEvent.Direction.Right:
                    command = SettingsNavigationCommand.Right;
                    return true;
                default:
                    command = default;
                    return false;
            }
        }

        /// <summary>
        /// Переводит клавиши управления в команду сервисного меню.
        /// </summary>
        private static bool TryGetNavigationCommand(KeyCode keyCode, out SettingsNavigationCommand command)
        {
            switch (keyCode)
            {
                case KeyCode.UpArrow:
                case KeyCode.W:
                    command = SettingsNavigationCommand.Up;
                    return true;
                case KeyCode.DownArrow:
                case KeyCode.S:
                    command = SettingsNavigationCommand.Down;
                    return true;
                case KeyCode.LeftArrow:
                case KeyCode.A:
                    command = SettingsNavigationCommand.Left;
                    return true;
                case KeyCode.RightArrow:
                case KeyCode.D:
                    command = SettingsNavigationCommand.Right;
                    return true;
                default:
                    command = default;
                    return false;
            }
        }

        /// <summary>
        /// Проверяет, находится ли фокус внутри TextField или его внутреннего текстового элемента.
        /// </summary>
        private static bool IsInsideTextField(VisualElement element)
        {
            for (var current = element; current != null; current = current.parent)
            {
                if (current is TextField)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Проверяет, можно ли показать настройку в сервисном меню.
        /// </summary>
        /// <param name="definition">Описание настройки из базы.</param>
        /// <returns>True, если настройка валидна и разрешена для сервисного меню.</returns>
        private static bool CanRender(ServiceSettingDefinition definition)
        {
            return definition != null &&
                definition.ShownInServiceMenu &&
                !string.IsNullOrWhiteSpace(definition.Key);
        }

        /// <summary>
        /// Добавляет визуальный заголовок группы настроек.
        /// </summary>
        /// <param name="groupName">Название группы из ServiceSettingsDatabase.</param>
        private void AddGroupHeader(string groupName)
        {
            var normalizedName = GetGroupName(groupName);
            var header = CreateGroupHeader(normalizedName, out var disclosure);
            header.RegisterCallback<FocusInEvent>(
                _ => SetCurrentSelection(normalizedName, normalizedName, null, isGroup: true),
                TrickleDown.TrickleDown);
            header.clicked += () => ToggleGroup(normalizedName);

            firstFocusableControl ??= header;
            groupHeaders[normalizedName] = header;
            groupRows[normalizedName] = new List<VisualElement>();
            settingsNavigationTargets.Add(header);

            if (disclosure != null)
            {
                groupDisclosureLabels[normalizedName] = disclosure;
            }

            SettingsContainer.Add(header);
        }

        /// <summary>
        /// Создает заголовок группы из UXML-шаблона, похожего на prefab для UI Toolkit.
        /// </summary>
        /// <param name="groupName">Название группы, которое будет подставлено в шаблон.</param>
        /// <param name="disclosure">Индикатор свернутого или раскрытого состояния группы.</param>
        /// <returns>Готовый корневой элемент заголовка группы.</returns>
        private static Button CreateGroupHeader(string groupName, out Label disclosure)
        {
            var template = GetGroupHeaderTemplate();
            if (template == null)
            {
                return CreateFallbackGroupHeader(groupName, out disclosure);
            }

            var templateContainer = template.Instantiate();
            var header = templateContainer.Q<Button>(GroupHeaderRootName);
            if (header == null)
            {
                return CreateFallbackGroupHeader(groupName, out disclosure);
            }

            header.RemoveFromHierarchy();
            var title = header.Q<Label>(GroupHeaderTitleName);
            if (title != null)
            {
                title.text = groupName.ToUpperInvariant();
            }

            disclosure = header.Q<Label>(GroupHeaderDisclosureName);
            if (disclosure != null)
            {
                disclosure.text = "▼";
            }

            return header;
        }

        /// <summary>
        /// Загружает UXML-шаблон заголовка группы из Addressables один раз за сессию.
        /// </summary>
        private static VisualTreeAsset GetGroupHeaderTemplate()
        {
            return GetTemplate(
                ref groupHeaderTemplate,
                ref groupHeaderTemplateHandle,
                UiTemplateAddressKeys.SettingsGroupHeader,
                GroupHeaderTemplateAssetPath);
        }

        /// <summary>
        /// Загружает UXML-шаблон bool-контрола из Addressables один раз за сессию.
        /// </summary>
        private static VisualTreeAsset GetBoolControlTemplate()
        {
            return GetTemplate(
                ref boolControlTemplate,
                ref boolControlTemplateHandle,
                UiTemplateAddressKeys.SettingsBoolControl,
                BoolControlTemplateAssetPath);
        }

        /// <summary>
        /// Загружает UXML-шаблон stepper-контрола из Addressables один раз за сессию.
        /// </summary>
        private static VisualTreeAsset GetStepperControlTemplate()
        {
            return GetTemplate(
                ref stepperControlTemplate,
                ref stepperControlTemplateHandle,
                UiTemplateAddressKeys.SettingsStepperControl,
                StepperControlTemplateAssetPath);
        }

        /// <summary>
        /// Загружает UXML-шаблон текстового контрола из Addressables один раз за сессию.
        /// </summary>
        private static VisualTreeAsset GetTextControlTemplate()
        {
            return GetTemplate(
                ref textControlTemplate,
                ref textControlTemplateHandle,
                UiTemplateAddressKeys.SettingsTextControl,
                TextControlTemplateAssetPath);
        }

        /// <summary>
        /// Загружает UXML-шаблон кнопочного контрола из Addressables один раз за сессию.
        /// </summary>
        private static VisualTreeAsset GetButtonControlTemplate()
        {
            return GetTemplate(
                ref buttonControlTemplate,
                ref buttonControlTemplateHandle,
                UiTemplateAddressKeys.SettingsButtonControl,
                ButtonControlTemplateAssetPath);
        }

        /// <summary>
        /// Лениво загружает UI Toolkit-шаблон и кэширует ссылку в статическом поле.
        /// </summary>
        private static VisualTreeAsset GetTemplate(
            ref VisualTreeAsset template,
            ref AsyncOperationHandle<VisualTreeAsset>? handle,
            string address,
            string editorFallbackPath)
        {
            if (template == null)
            {
                template = LoadAddressableTemplate(ref handle, address);
#if UNITY_EDITOR
                if (template == null)
                {
                    template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(editorFallbackPath);
                }
#endif
            }

            return template;
        }

        private static VisualTreeAsset LoadAddressableTemplate(
            ref AsyncOperationHandle<VisualTreeAsset>? cachedHandle,
            string address)
        {
            if (cachedHandle.HasValue && cachedHandle.Value.IsValid())
            {
                return cachedHandle.Value.Result;
            }

            if (string.IsNullOrWhiteSpace(address))
            {
                return null;
            }

            try
            {
                var handle = Addressables.LoadAssetAsync<VisualTreeAsset>(address);
                var template = handle.WaitForCompletion();
                if (handle.Status == AsyncOperationStatus.Succeeded && template != null)
                {
                    cachedHandle = handle;
                    return template;
                }

                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[{nameof(SettingsModalWindow)}] Failed to load UI template '{address}' from Addressables: {exception.Message}");
            }

            return null;
        }

        /// <summary>
        /// Резервная сборка заголовка, если UXML-шаблон не найден или поврежден.
        /// </summary>
        private static Button CreateFallbackGroupHeader(string groupName, out Label disclosure)
        {
            var header = new Button { focusable = true };
            header.AddToClassList("settings-group-title");
            header.AddToClassList("settings-focus-target");

            var title = new Label(groupName.ToUpperInvariant());
            title.AddToClassList("settings-group-label");

            disclosure = new Label("▼");
            disclosure.AddToClassList("settings-group-disclosure");

            header.Add(title);
            header.Add(disclosure);
            return header;
        }

        /// <summary>
        /// Создает строку настройки и подходящий UI-контрол с подписью внутри шаблона.
        /// </summary>
        /// <param name="definition">Описание настройки.</param>
        private void AddSettingRow(ServiceSettingDefinition definition, string groupName)
        {
            var row = new VisualElement { focusable = true };
            row.AddToClassList("settings-row");
            row.AddToClassList("settings-focus-target");
            var settingTitle = GetSettingTitle(definition);
            row.RegisterCallback<FocusInEvent>(
                _ => SetCurrentSelection(settingTitle, groupName, definition.Description),
                TrickleDown.TrickleDown);

            var control = CreateSettingControl(definition, settingTitle, out var adjustment, out var activation);
            if (control != null)
            {
                row.Add(control);
                settingControls.Add(control);
                control.RegisterCallback<FocusInEvent>(
                    _ => SetCurrentSelection(settingTitle, groupName, definition.Description),
                    TrickleDown.TrickleDown);
            }

            if (adjustment != null)
            {
                settingAdjustments[row] = adjustment;
            }

            if (activation != null)
            {
                settingActivations[row] = activation;
            }

            SettingsContainer.Add(row);
            if (!groupRows.TryGetValue(groupName, out var rows))
            {
                rows = new List<VisualElement>();
                groupRows[groupName] = rows;
            }

            rows.Add(row);
            settingsNavigationTargets.Add(row);
            row.style.display = collapsedGroups.Contains(groupName) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>
        /// Добавляет нижние кнопки окна в логический список навигации настроек.
        /// </summary>
        private void AddFooterNavigationTargets()
        {
            AddFooterNavigationTarget(saveButton);
            AddFooterNavigationTarget(reloadButton);
            AddFooterNavigationTarget(resetButton);
            AddFooterNavigationTarget(closeButton);
        }

        /// <summary>
        /// Добавляет кнопку в список навигации, если она доступна.
        /// </summary>
        private void AddFooterNavigationTarget(VisualElement target)
        {
            if (target != null && target.focusable)
            {
                settingsNavigationTargets.Add(target);
            }
        }

        /// <summary>
        /// Пересчитывает вертикальные интервалы как долю ширины видимой области списка.
        /// Так отступы адаптируются к размеру окна, но не зависят от высоты свернутого контента.
        /// </summary>
        private void UpdateRelativeSettingsSpacing()
        {
            var baseWidth = ResolveRelativeSpacingBaseWidth();
            var headerTop = ResolveRelativeSpacing(baseWidth, GroupHeaderTopSpacingRatio, GroupHeaderTopSpacingMin, GroupHeaderTopSpacingMax);
            var headerBottom = ResolveRelativeSpacing(baseWidth, GroupHeaderBottomSpacingRatio, GroupHeaderBottomSpacingMin, GroupHeaderBottomSpacingMax);
            var controlVertical = ResolveRelativeSpacing(baseWidth, SettingControlVerticalSpacingRatio, SettingControlVerticalSpacingMin, SettingControlVerticalSpacingMax);
            var actionButtonVertical = ResolveRelativeSpacing(baseWidth, ActionButtonVerticalSpacingRatio, ActionButtonVerticalSpacingMin, ActionButtonVerticalSpacingMax);

            foreach (var header in groupHeaders.Values)
            {
                if (header == null)
                {
                    continue;
                }

                header.style.marginTop = headerTop;
                header.style.marginBottom = headerBottom;
            }

            foreach (var control in settingControls)
            {
                ApplyRelativeControlSpacing(control, controlVertical, actionButtonVertical);
            }

            if (currentFocusPointerTarget != null && IsSettingsNavigationTargetVisible(currentFocusPointerTarget))
            {
                PlaceFocusPointerAtCurrentTarget(currentFocusPointerTarget);
            }
        }

        private void ApplyRelativeControlSpacing(
            VisualElement control,
            float controlVertical,
            float actionButtonVertical)
        {
            if (control == null)
            {
                return;
            }

            if (control.ClassListContains("settings-button-control"))
            {
                control.style.marginTop = 0f;
                control.style.marginBottom = 0f;

                var actionButton = control.Q<Button>(ActionButtonName);
                if (actionButton != null)
                {
                    actionButton.style.marginTop = actionButtonVertical;
                    actionButton.style.marginBottom = actionButtonVertical;
                }

                return;
            }

            control.style.marginTop = controlVertical;
            control.style.marginBottom = controlVertical;
        }

        private float ResolveRelativeSpacingBaseWidth()
        {
            var width = settingsList?.resolvedStyle.width ?? 0f;
            return float.IsNaN(width) || width <= 0f ? RelativeSpacingFallbackWidth : width;
        }

        private static float ResolveRelativeSpacing(float baseWidth, float ratio, float min, float max)
        {
            return Mathf.Clamp(baseWidth * ratio, min, max);
        }

        private void ScheduleRelativeSpacingUpdate()
        {
            Root?.schedule.Execute(UpdateRelativeSettingsSpacing);
        }

        private void HandleSettingsListGeometryChanged(GeometryChangedEvent evt)
        {
            UpdateRelativeSettingsSpacing();
        }

        /// <summary>
        /// Сворачивает или раскрывает настройки внутри выбранной группы.
        /// </summary>
        /// <param name="groupName">Название группы, заголовок которой нажал оператор.</param>
        private void ToggleGroup(string groupName)
        {
            var normalizedName = GetGroupName(groupName);
            if (!groupRows.ContainsKey(normalizedName) ||
                !groupHeaders.TryGetValue(normalizedName, out var header))
            {
                return;
            }

            currentFocusPointerTarget = header;
            header.Focus();

            if (!collapsedGroups.Add(normalizedName))
            {
                collapsedGroups.Remove(normalizedName);
            }

            ApplyGroupCollapsedState(normalizedName);
            SetCurrentSelection(normalizedName, normalizedName, null, isGroup: true);
            header.schedule.Execute(() =>
            {
                if (currentFocusPointerTarget == header && IsSettingsNavigationTargetVisible(header))
                {
                    PlaceFocusPointerAtCurrentTarget(header);
                }
            });
        }

        /// <summary>
        /// Применяет текущее свернутое состояние к строкам группы и ее заголовку.
        /// </summary>
        private void ApplyGroupCollapsedState(string groupName)
        {
            var isCollapsed = collapsedGroups.Contains(groupName);

            if (groupHeaders.TryGetValue(groupName, out var header))
            {
                if (isCollapsed)
                {
                    header.AddToClassList("settings-group-title--collapsed");
                }
                else
                {
                    header.RemoveFromClassList("settings-group-title--collapsed");
                }
            }

            if (groupDisclosureLabels.TryGetValue(groupName, out var disclosure))
            {
                disclosure.text = isCollapsed ? "▶" : "▼";
            }

            if (!groupRows.TryGetValue(groupName, out var rows))
            {
                return;
            }

            foreach (var row in rows)
            {
                row.style.display = isCollapsed ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (isCollapsed && currentFocusPointerTarget != null && rows.Contains(currentFocusPointerTarget) &&
                groupHeaders.TryGetValue(groupName, out var fallbackHeader))
            {
                fallbackHeader.Focus();
                currentFocusPointerTarget = fallbackHeader;
                MoveFocusPointerTo(fallbackHeader);
            }
        }

        /// <summary>
        /// Меняет значение настройки, на строке которой сейчас находится фокус.
        /// </summary>
        /// <param name="current">Текущий сфокусированный элемент.</param>
        /// <param name="increase">True для движения вправо, false для движения влево.</param>
        /// <returns>Всегда true, чтобы горизонтальная навигация не двигала фокус.</returns>
        private bool AdjustFocusedSetting(VisualElement current, bool increase)
        {
            var target = ResolveFocusPointerTarget(current) ??
                ResolveFocusPointerTarget(Root?.panel?.focusController?.focusedElement as VisualElement) ??
                currentFocusPointerTarget;
            if (target != null && settingAdjustments.TryGetValue(target, out var adjustment))
            {
                _ = increase ? adjustment.Increment() : adjustment.Decrement();
                MoveFocusPointerTo(target);
            }

            return true;
        }

        /// <summary>
        /// Запускает действие настройки, если текущая строка поддерживает кнопку.
        /// </summary>
        private bool ActivateFocusedSetting(VisualElement current)
        {
            var target = ResolveFocusPointerTarget(current) ??
                ResolveFocusPointerTarget(Root?.panel?.focusController?.focusedElement as VisualElement) ??
                currentFocusPointerTarget;
            if (target != null && settingActivations.TryGetValue(target, out var activation))
            {
                activation();
                MoveFocusPointerTo(target);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Обрабатывает одну команду навигации и отсекает дубль, если Unity прислал key и navigation в одном кадре.
        /// </summary>
        private bool HandleSettingsNavigationCommand(
            VisualElement current,
            SettingsNavigationCommand command,
            SettingsNavigationSource source)
        {
            var now = Time.unscaledTime;
            if (lastNavigationFrame == Time.frameCount || IsDuplicateNavigationSource(command, source, now))
            {
                KeepFocusPointerOnCurrentTarget();
                return true;
            }

            lastNavigationFrame = Time.frameCount;
            lastNavigationCommand = command;
            lastNavigationSource = source;
            lastNavigationCommandTime = now;

            return command switch
            {
                SettingsNavigationCommand.Up => MoveSettingsFocus(current, direction: -1),
                SettingsNavigationCommand.Down => MoveSettingsFocus(current, direction: 1),
                SettingsNavigationCommand.Left => AdjustFocusedSetting(current, increase: false),
                SettingsNavigationCommand.Right => AdjustFocusedSetting(current, increase: true),
                _ => true,
            };
        }

        /// <summary>
        /// Обрабатывает KeyDown через общий фильтр дублей навигации.
        /// </summary>
        private bool HandleSettingsNavigationKey(VisualElement current, SettingsNavigationCommand command)
        {
            return HandleSettingsNavigationCommand(current, command, SettingsNavigationSource.KeyDown);
        }

        /// <summary>
        /// Отсекает второй источник одного физического нажатия, не мешая повторным нажатиям того же источника.
        /// </summary>
        private bool IsDuplicateNavigationSource(
            SettingsNavigationCommand command,
            SettingsNavigationSource source,
            float now)
        {
            return lastNavigationCommand == command &&
                lastNavigationSource != source &&
                now - lastNavigationCommandTime <= DuplicateNavigationSourceWindow;
        }

        /// <summary>
        /// Сбрасывает временное состояние навигации при закрытии или пересборке окна.
        /// </summary>
        private void ResetNavigationState()
        {
            lastNavigationFrame = -1;
            lastNavigationCommand = default;
            lastNavigationSource = default;
            lastNavigationCommandTime = -DuplicateNavigationSourceWindow;
            lastVerticalNavigationTime = -VerticalNavigationCooldown;
        }

        /// <summary>
        /// Двигает фокус по настройкам только с небольшой задержкой между командами.
        /// </summary>
        /// <param name="current">Текущий сфокусированный элемент.</param>
        /// <param name="direction">-1 для движения вверх, 1 для движения вниз.</param>
        /// <returns>Всегда true, чтобы стандартный стек не продублировал движение.</returns>
        private bool MoveSettingsFocus(VisualElement current, int direction)
        {
            var now = Time.unscaledTime;
            if (now - lastVerticalNavigationTime < VerticalNavigationCooldown)
            {
                KeepFocusPointerOnCurrentTarget();
                return true;
            }

            lastVerticalNavigationTime = now;
            var anchor = ResolveFocusPointerTarget(current) ?? currentFocusPointerTarget ?? current;
            FocusSettingsNavigationTarget(anchor, direction);
            return true;
        }

        /// <summary>
        /// Переводит фокус по собственному списку логических элементов окна настроек.
        /// </summary>
        private void FocusSettingsNavigationTarget(VisualElement anchor, int direction)
        {
            var nextTarget = GetNextSettingsNavigationTarget(anchor, direction);
            if (nextTarget == null)
            {
                KeepFocusPointerOnCurrentTarget();
                return;
            }

            nextTarget.Focus();
            currentFocusPointerTarget = nextTarget;
            SchedulePlaceFocusPointerAt(nextTarget);
        }

        /// <summary>
        /// Возвращает следующую видимую цель навигации без внутренних контролов строки.
        /// </summary>
        private VisualElement GetNextSettingsNavigationTarget(VisualElement anchor, int direction)
        {
            if (settingsNavigationTargets.Count == 0)
            {
                return null;
            }

            var anchorIndex = settingsNavigationTargets.IndexOf(anchor);
            if (anchorIndex < 0)
            {
                anchorIndex = direction > 0 ? -1 : settingsNavigationTargets.Count;
            }

            for (var offset = 1; offset <= settingsNavigationTargets.Count; offset++)
            {
                var targetIndex = (anchorIndex + direction * offset + settingsNavigationTargets.Count) % settingsNavigationTargets.Count;
                var candidate = settingsNavigationTargets[targetIndex];
                if (IsSettingsNavigationTargetVisible(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Проверяет, можно ли сейчас перейти фокусом на элемент настроек.
        /// </summary>
        private static bool IsSettingsNavigationTargetVisible(VisualElement target)
        {
            return target != null &&
                target.panel != null &&
                target.enabledInHierarchy &&
                target.resolvedStyle.display != DisplayStyle.None &&
                target.resolvedStyle.visibility != Visibility.Hidden;
        }

        /// <summary>
        /// Подбирает UI Toolkit контрол по типу значения настройки.
        /// </summary>
        /// <param name="definition">Описание настройки.</param>
        /// <returns>Готовый VisualElement для редактирования значения.</returns>
        private VisualElement CreateSettingControl(
            ServiceSettingDefinition definition,
            string settingTitle,
            out SettingAdjustment adjustment,
            out Action activation)
        {
            var key = definition.Key;
            var value = settingsService.Get(key);
            activation = null;

            return definition.ValueType switch
            {
                ServiceSettingValueType.Bool => CreateBoolControl(settingTitle, key, value.boolValue, out adjustment, out activation),
                ServiceSettingValueType.Int => CreateIntControl(settingTitle, definition, key, value.intValue, out adjustment),
                ServiceSettingValueType.Float => CreateFloatControl(settingTitle, definition, key, value.floatValue, out adjustment),
                ServiceSettingValueType.String => CreateStringControl(settingTitle, key, value.stringValue, out adjustment),
                ServiceSettingValueType.Button => CreateButtonControl(settingTitle, definition, out adjustment, out activation),
                ServiceSettingValueType.Option => CreateOptionControl(settingTitle, definition, key, value.intValue, out adjustment),
                _ => CreateEmptyControl(out adjustment),
            };
        }

        /// <summary>
        /// Создает переключатель для bool-настроек.
        /// </summary>
        private Button CreateBoolControl(
            string settingTitle,
            string key,
            bool value,
            out SettingAdjustment adjustment,
            out Action activation)
        {
            var currentValue = value;
            var control = CreateBoolControlFromTemplate(out var indicator);
            SetControlTitle(control, settingTitle);
            ApplyCheckboxState(indicator, currentValue);

            bool ApplyValue(bool nextValue)
            {
                currentValue = nextValue;
                settingsService.SetBool(key, currentValue);
                ApplyCheckboxState(indicator, currentValue);
                return true;
            }

            adjustment = new SettingAdjustment(
                () => ApplyValue(false),
                () => ApplyValue(true));
            activation = () => ApplyValue(!currentValue);
            control.clicked += activation;
            return control;
        }

        /// <summary>
        /// Создает поле или слайдер для целочисленных настроек.
        /// </summary>
        private VisualElement CreateIntControl(
            string settingTitle,
            ServiceSettingDefinition definition,
            string key,
            int value,
            out SettingAdjustment adjustment)
        {
            var currentValue = Mathf.Clamp(value, definition.MinInt, definition.MaxInt);
            var control = CreateStepperControl(
                settingTitle,
                FormatIntValue(currentValue),
                () =>
                {
                    currentValue = Mathf.Clamp(currentValue - definition.IntStep, definition.MinInt, definition.MaxInt);
                    settingsService.SetInt(key, currentValue);
                    return FormatIntValue(currentValue);
                },
                () =>
                {
                    currentValue = Mathf.Clamp(currentValue + definition.IntStep, definition.MinInt, definition.MaxInt);
                    settingsService.SetInt(key, currentValue);
                    return FormatIntValue(currentValue);
                },
                out adjustment);

            return control;
        }

        /// <summary>
        /// Создает поле или слайдер для дробных настроек.
        /// </summary>
        private VisualElement CreateFloatControl(
            string settingTitle,
            ServiceSettingDefinition definition,
            string key,
            float value,
            out SettingAdjustment adjustment)
        {
            var currentValue = Mathf.Clamp(value, definition.MinFloat, definition.MaxFloat);
            var control = CreateStepperControl(
                settingTitle,
                FormatFloatValue(definition, currentValue),
                () =>
                {
                    currentValue = Mathf.Clamp(currentValue - definition.FloatStep, definition.MinFloat, definition.MaxFloat);
                    settingsService.SetFloat(key, currentValue);
                    return FormatFloatValue(definition, currentValue);
                },
                () =>
                {
                    currentValue = Mathf.Clamp(currentValue + definition.FloatStep, definition.MinFloat, definition.MaxFloat);
                    settingsService.SetFloat(key, currentValue);
                    return FormatFloatValue(definition, currentValue);
                },
                out adjustment);

            return control;
        }

        /// <summary>
        /// Создает текстовое поле для строковых настроек.
        /// </summary>
        private VisualElement CreateStringControl(string settingTitle, string key, string value, out SettingAdjustment adjustment)
        {
            adjustment = null;
            var control = CreateTextControlFromTemplate(out var textField);
            SetControlTitle(control, settingTitle);
            textField.value = value ?? string.Empty;
            textField.RegisterValueChangedCallback(evt => settingsService.SetString(key, evt.newValue));
            return control;
        }

        /// <summary>
        /// Создает кнопку действия из legacy-настройки и маршрутизирует ее в сервисный action-router.
        /// </summary>
        private VisualElement CreateButtonControl(
            string settingTitle,
            ServiceSettingDefinition definition,
            out SettingAdjustment adjustment,
            out Action activation)
        {
            adjustment = null;
            var control = CreateButtonControlFromTemplate(out var button);
            SetControlTitle(control, settingTitle);
            button.text = string.IsNullOrWhiteSpace(definition.ButtonText)
                ? definition.DisplayName
                : definition.ButtonText;
            ApplyButtonVisual(button, definition);

            activation = () =>
            {
                var result = ServiceSettingsActionRouter.Execute(definition, settingsService, prizeSessionState);
                Debug.Log(
                    $"[{nameof(SettingsModalWindow)}] Button setting '{definition.Key}' clicked. handled={result.Handled} data='{definition.ButtonData}'.");
                SetCurrentSelection(result.Title, definition.Group, result.Status);
                SetStatus(result.Status);
                ServiceActionActivated?.Invoke(definition, result);
            };
            button.clicked += activation;
            return control;
        }

        /// <summary>
        /// Применяет цвет/спрайт кнопки из базы настроек, чтобы шаблон отвечал за форму, а данные - за вариант.
        /// </summary>
        private static void ApplyButtonVisual(Button button, ServiceSettingDefinition definition)
        {
            if (button == null || definition == null)
            {
                return;
            }

            if (definition.ButtonSprite != null)
            {
                button.style.backgroundImage = new StyleBackground(definition.ButtonSprite);
            }

            if (definition.ButtonColor.a > 0f)
            {
                button.style.unityBackgroundImageTintColor = definition.ButtonColor;
            }
        }

        /// <summary>
        /// Создает выпадающий список для настроек с фиксированными вариантами.
        /// </summary>
        private VisualElement CreateOptionControl(
            string settingTitle,
            ServiceSettingDefinition definition,
            string key,
            int value,
            out SettingAdjustment adjustment)
        {
            var choices = new List<string>(definition.Options);
            if (choices.Count == 0)
            {
                adjustment = null;
                var empty = CreateTextControlFromTemplate(out var textField);
                SetControlTitle(empty, settingTitle);
                textField.value = "Нет вариантов";
                textField.SetEnabled(false);
                return empty;
            }

            var currentIndex = Mathf.Clamp(value, 0, choices.Count - 1);
            return CreateStepperControl(
                settingTitle,
                choices[currentIndex],
                () =>
                {
                    currentIndex = (currentIndex - 1 + choices.Count) % choices.Count;
                    settingsService.SetOptionIndex(key, currentIndex);
                    return choices[currentIndex];
                },
                () =>
                {
                    currentIndex = (currentIndex + 1) % choices.Count;
                    settingsService.SetOptionIndex(key, currentIndex);
                    return choices[currentIndex];
                },
                out adjustment);
        }

        /// <summary>
        /// Создает операторский stepper-контрол со стрелками по единому шаблону.
        /// </summary>
        private static VisualElement CreateStepperControl(
            string settingTitle,
            string initialValue,
            Func<string> decrement,
            Func<string> increment,
            out SettingAdjustment adjustment)
        {
            var container = CreateStepperControlFromTemplate(out var previousButton, out var valueLabel, out var nextButton);
            SetControlTitle(container, settingTitle);
            valueLabel.text = initialValue;

            adjustment = new SettingAdjustment(
                () =>
                {
                    valueLabel.text = decrement();
                    return true;
                },
                () =>
                {
                    valueLabel.text = increment();
                    return true;
                });

            var stepperAdjustment = adjustment;
            previousButton.clicked += () => stepperAdjustment.Decrement();
            nextButton.clicked += () => stepperAdjustment.Increment();

            return container;
        }

        /// <summary>
        /// Создает bool-контрол из UXML-шаблона, а при проблеме возвращает кодовый fallback.
        /// </summary>
        private static Button CreateBoolControlFromTemplate(out VisualElement indicator)
        {
            var template = GetBoolControlTemplate();
            if (template == null)
            {
                return CreateFallbackBoolControl(out indicator);
            }

            var templateContainer = template.Instantiate();
            var control = templateContainer.Q<Button>(BoolControlRootName);
            indicator = control?.Q<VisualElement>(BoolIndicatorName);
            if (control == null || indicator == null)
            {
                return CreateFallbackBoolControl(out indicator);
            }

            control.RemoveFromHierarchy();
            return control;
        }

        /// <summary>
        /// Создает кнопочный контрол из UXML-шаблона, а при проблеме возвращает кодовый fallback.
        /// </summary>
        private static VisualElement CreateButtonControlFromTemplate(out Button actionButton)
        {
            var template = GetButtonControlTemplate();
            if (template == null)
            {
                return CreateFallbackButtonControl(out actionButton);
            }

            var templateContainer = template.Instantiate();
            var control = templateContainer.Q<VisualElement>(ButtonControlRootName);
            actionButton = control?.Q<Button>(ActionButtonName);
            if (control == null || actionButton == null)
            {
                return CreateFallbackButtonControl(out actionButton);
            }

            control.RemoveFromHierarchy();
            return control;
        }

        /// <summary>
        /// Создает stepper-контрол из UXML-шаблона, а при проблеме возвращает кодовый fallback.
        /// </summary>
        private static VisualElement CreateStepperControlFromTemplate(
            out Button previousButton,
            out Label valueLabel,
            out Button nextButton)
        {
            var template = GetStepperControlTemplate();
            if (template == null)
            {
                return CreateFallbackStepperControl(out previousButton, out valueLabel, out nextButton);
            }

            var templateContainer = template.Instantiate();
            var container = templateContainer.Q<VisualElement>(StepperControlRootName);
            previousButton = container?.Q<Button>(StepperPreviousButtonName);
            valueLabel = container?.Q<Label>(StepperValueLabelName);
            nextButton = container?.Q<Button>(StepperNextButtonName);
            if (container == null || previousButton == null || valueLabel == null || nextButton == null)
            {
                return CreateFallbackStepperControl(out previousButton, out valueLabel, out nextButton);
            }

            container.RemoveFromHierarchy();
            return container;
        }

        /// <summary>
        /// Создает текстовый контрол из UXML-шаблона, а при проблеме возвращает кодовый fallback.
        /// </summary>
        private static VisualElement CreateTextControlFromTemplate(out TextField textField)
        {
            var template = GetTextControlTemplate();
            if (template == null)
            {
                return CreateFallbackTextControl(out textField);
            }

            var templateContainer = template.Instantiate();
            var control = templateContainer.Q<VisualElement>(TextControlRootName);
            textField = control?.Q<TextField>(TextFieldName);
            if (control == null || textField == null)
            {
                return CreateFallbackTextControl(out textField);
            }

            control.RemoveFromHierarchy();
            return control;
        }

        /// <summary>
        /// Резервная сборка кнопочного контрола, если UXML-шаблон не найден или поврежден.
        /// </summary>
        private static VisualElement CreateFallbackButtonControl(out Button actionButton)
        {
            var container = CreateFallbackSettingControlContainer("Действие");
            actionButton = new Button { text = "Выполнить", focusable = false };
            actionButton.name = ActionButtonName;
            actionButton.AddToClassList("settings-action-button");
            container.Add(actionButton);
            return container;
        }

        /// <summary>
        /// Резервная сборка checkbox-контрола, если UXML-шаблон не найден или поврежден.
        /// </summary>
        private static Button CreateFallbackBoolControl(out VisualElement indicator)
        {
            var control = new Button { focusable = false };
            control.AddToClassList("settings-control");
            control.AddToClassList("settings-bool-control");
            control.Add(CreateFallbackControlTitle("Переключатель"));

            indicator = new VisualElement();
            indicator.AddToClassList("settings-checkbox");
            control.Add(indicator);
            return control;
        }

        /// <summary>
        /// Резервная сборка stepper-контрола, если UXML-шаблон не найден или поврежден.
        /// </summary>
        private static VisualElement CreateFallbackStepperControl(
            out Button previousButton,
            out Label valueLabel,
            out Button nextButton)
        {
            var container = new VisualElement();
            container.AddToClassList("settings-control");
            container.AddToClassList("settings-stepper");
            container.Add(CreateFallbackControlTitle("Значение"));

            var valueContainer = new VisualElement();
            valueContainer.AddToClassList("settings-stepper-value");

            previousButton = new Button { text = "◀", focusable = false };
            previousButton.AddToClassList("settings-arrow-button");

            valueLabel = new Label();
            valueLabel.AddToClassList("settings-value-label");

            nextButton = new Button { text = "▶", focusable = false };
            nextButton.AddToClassList("settings-arrow-button");

            valueContainer.Add(previousButton);
            valueContainer.Add(valueLabel);
            valueContainer.Add(nextButton);
            container.Add(valueContainer);
            return container;
        }

        /// <summary>
        /// Резервная сборка текстового поля, если UXML-шаблон не найден или поврежден.
        /// </summary>
        private static VisualElement CreateFallbackTextControl(out TextField textField)
        {
            var container = CreateFallbackSettingControlContainer("Текст");
            textField = new TextField();
            textField.AddToClassList("settings-text-field");
            container.Add(textField);
            return container;
        }

        /// <summary>
        /// Создает fallback-контейнер настройки с подписью, если UXML-шаблон недоступен.
        /// </summary>
        private static VisualElement CreateFallbackSettingControlContainer(string title)
        {
            var container = new VisualElement();
            container.AddToClassList("settings-control");
            container.Add(CreateFallbackControlTitle(title));
            return container;
        }

        /// <summary>
        /// Создает fallback-подпись настройки в той же роли, что и label внутри UXML-шаблонов.
        /// </summary>
        private static Label CreateFallbackControlTitle(string title)
        {
            var label = new Label(title);
            label.name = ControlTitleName;
            label.AddToClassList("settings-name");
            return label;
        }

        /// <summary>
        /// Подставляет название настройки в label, который является частью UXML-шаблона.
        /// </summary>
        private static void SetControlTitle(VisualElement control, string title)
        {
            var titleLabel = control?.Q<Label>(ControlTitleName);
            if (titleLabel != null)
            {
                titleLabel.text = title;
            }
        }

        /// <summary>
        /// Возвращает пустой контрол для неизвестного типа настройки.
        /// </summary>
        private static VisualElement CreateEmptyControl(out SettingAdjustment adjustment)
        {
            adjustment = null;
            return null;
        }

        /// <summary>
        /// Обновляет визуальное состояние checkbox-кнопки.
        /// </summary>
        private static void ApplyCheckboxState(VisualElement control, bool isChecked)
        {
            if (control == null)
            {
                return;
            }

            control.style.backgroundColor = isChecked
                ? new StyleColor(new Color(0.8078f, 1f, 0.102f, 1f))
                : new StyleColor(Color.clear);

            if (isChecked)
            {
                control.AddToClassList("settings-checkbox--checked");
                return;
            }

            control.RemoveFromClassList("settings-checkbox--checked");
        }

        /// <summary>
        /// Форматирует int-значение в текст для центрального поля stepper.
        /// </summary>
        private static string FormatIntValue(int value)
        {
            return value.ToString();
        }

        /// <summary>
        /// Форматирует float-значение с учетом процентного режима настройки.
        /// </summary>
        private static string FormatFloatValue(ServiceSettingDefinition definition, float value)
        {
            return definition.DisplayFloatAsPercentage
                ? $"{value * 100f:0}%"
                : $"{value:0.##}";
        }

        /// <summary>
        /// Обновляет правую область с названием выбранной группы/настройки и описанием настройки.
        /// </summary>
        private void SetCurrentSelection(string title, string groupName, string description, bool isGroup = false)
        {
            var normalizedName = GetGroupName(groupName);

            if (currentGroupTitleLabel != null)
            {
                var displayTitle = string.IsNullOrWhiteSpace(title) ? normalizedName : title;
                currentGroupTitleLabel.text = isGroup ? $"Группа {displayTitle}" : displayTitle;
            }

            foreach (var header in groupHeaders.Values)
            {
                header.RemoveFromClassList("settings-group-title--active");
            }

            if (groupHeaders.TryGetValue(normalizedName, out var activeHeader))
            {
                activeHeader.AddToClassList("settings-group-title--active");
            }

            if (!string.IsNullOrWhiteSpace(description))
            {
                SetStatus(description);
            }
        }

        /// <summary>
        /// Возвращает понятное название настройки для правой панели и строки списка.
        /// </summary>
        private static string GetSettingTitle(ServiceSettingDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.Key : definition.DisplayName;
        }

        /// <summary>
        /// Создает единый указатель фокуса, который перемещается поверх всего окна настроек.
        /// </summary>
        /// <param name="root">Корневой элемент модального окна.</param>
        private static Label CreateModalFocusPointer(VisualElement root)
        {
            var pointer = new Label("▶");
            pointer.AddToClassList("settings-focus-pointer");
            pointer.pickingMode = PickingMode.Ignore;
            pointer.style.display = DisplayStyle.None;
            root.Add(pointer);
            pointer.BringToFront();
            return pointer;
        }

        /// <summary>
        /// Перемещает общий указатель к элементу, который получил фокус.
        /// </summary>
        private void HandleRootFocusIn(FocusInEvent evt)
        {
            if (evt.target is not VisualElement focused)
            {
                return;
            }

            var target = ResolveFocusPointerTarget(focused);
            if (target == null)
            {
                if (currentFocusPointerTarget != null)
                {
                    MoveFocusPointerTo(currentFocusPointerTarget);
                }

                return;
            }

            currentFocusPointerTarget = target;
            target.schedule.Execute(() =>
            {
                if (target == currentFocusPointerTarget && IsOpen && IsSettingsNavigationTargetVisible(target))
                {
                    MoveFocusPointerTo(target);
                }
            });
        }

        /// <summary>
        /// Прячет общий указатель, если фокус ушел с элементов окна настроек.
        /// </summary>
        private void HandleRootFocusOut(FocusOutEvent evt)
        {
            Root?.schedule.Execute(() =>
            {
                var focused = Root?.panel?.focusController?.focusedElement as VisualElement;
                var target = ResolveFocusPointerTarget(focused);
                if (target != null)
                {
                    currentFocusPointerTarget = target;
                    MoveFocusPointerTo(target);
                    return;
                }

                if (currentFocusPointerTarget != null)
                {
                    MoveFocusPointerTo(currentFocusPointerTarget);
                }
            });
        }

        /// <summary>
        /// Ищет ближайший элемент, рядом с которым должен стоять указатель.
        /// </summary>
        private VisualElement ResolveFocusPointerTarget(VisualElement element)
        {
            for (var current = element; current != null && current != Root; current = current.parent)
            {
                if (current.ClassListContains("settings-focus-target"))
                {
                    return current;
                }
            }

            return element == saveButton || element == reloadButton || element == resetButton || element == closeButton
                ? element
                : null;
        }

        /// <summary>
        /// Ставит общий указатель слева от выбранного элемента в координатах модального окна.
        /// </summary>
        private void MoveFocusPointerTo(VisualElement target)
        {
            if (focusPointer == null || Root == null || target == null || target.resolvedStyle.display == DisplayStyle.None)
            {
                return;
            }

            if (EnsureTargetVisibleInSettingsList(target))
            {
                SchedulePlaceFocusPointerAt(target);
                return;
            }

            PlaceFocusPointerAtCurrentTarget(target);
        }

        /// <summary>
        /// Откладывает позиционирование указателя до следующего прохода layout.
        /// </summary>
        private void SchedulePlaceFocusPointerAt(VisualElement target)
        {
            Root?.schedule.Execute(() => PlaceFocusPointerAtCurrentTarget(target));
        }

        /// <summary>
        /// Ставит указатель только если цель по-прежнему является текущей и видимой.
        /// </summary>
        private void PlaceFocusPointerAtCurrentTarget(VisualElement target)
        {
            if (target == currentFocusPointerTarget && IsOpen && IsSettingsNavigationTargetVisible(target))
            {
                PlaceFocusPointerAt(target);
            }
        }

        /// <summary>
        /// Ставит указатель по текущей геометрии элемента без повторной попытки прокрутки.
        /// </summary>
        private void PlaceFocusPointerAt(VisualElement target)
        {
            if (focusPointer == null || Root == null || target == null || target.resolvedStyle.display == DisplayStyle.None)
            {
                return;
            }

            var rootBounds = Root.worldBound;
            var targetBounds = target.worldBound;
            if (rootBounds.width <= 0f || targetBounds.width <= 0f || targetBounds.height <= 0f)
            {
                SchedulePlaceFocusPointerAt(target);
                return;
            }

            focusPointer.style.display = DisplayStyle.Flex;
            focusPointer.style.visibility = Visibility.Visible;
            focusPointer.style.opacity = 1f;
            currentFocusPointerTarget = target;
            var targetPosition = new Vector2(
                Mathf.Max(FocusPointerMinLeft, targetBounds.xMin - rootBounds.xMin - FocusPointerWidth - FocusPointerGap),
                targetBounds.center.y - rootBounds.yMin - (FocusPointerHeight * 0.5f));

            AnimateFocusPointerTo(targetPosition);
            focusPointer.BringToFront();
        }

        /// <summary>
        /// Показывает указатель после открытия окна, даже если стартовый FocusInEvent уже прошел.
        /// </summary>
        private void ShowFocusPointerOnCurrentOrDefault()
        {
            if (!IsOpen)
            {
                return;
            }

            var focused = Root?.panel?.focusController?.focusedElement as VisualElement;
            var target = ResolveFocusPointerTarget(focused) ??
                currentFocusPointerTarget ??
                firstFocusableControl;
            if (target != null && IsSettingsNavigationTargetVisible(target))
            {
                currentFocusPointerTarget = target;
                MoveFocusPointerTo(target);
            }
        }

        /// <summary>
        /// Прокручивает список настроек так, чтобы активная строка оставалась внутри видимой области ScrollView.
        /// </summary>
        /// <param name="target">Элемент, к которому должен привязаться указатель.</param>
        /// <returns>True, если список был прокручен и позицию указателя нужно пересчитать позже.</returns>
        private bool EnsureTargetVisibleInSettingsList(VisualElement target)
        {
            if (settingsList == null || target == null || !ContainsElement(settingsList, target))
            {
                return false;
            }

            var viewport = settingsList.contentViewport ?? settingsList;
            var viewportBounds = viewport.worldBound;
            var targetBounds = target.worldBound;
            if (viewportBounds.height <= 0f || targetBounds.height <= 0f)
            {
                settingsList.ScrollTo(target);
                return true;
            }

            var topLimit = viewportBounds.yMin + ScrollVisibilityPadding;
            var bottomLimit = viewportBounds.yMax - ScrollVisibilityPadding;
            var nextOffsetY = settingsList.scrollOffset.y;
            var targetNeedsScroll = false;

            if (targetBounds.yMin < topLimit)
            {
                nextOffsetY -= topLimit - targetBounds.yMin;
                targetNeedsScroll = true;
            }
            else if (targetBounds.yMax > bottomLimit)
            {
                nextOffsetY += targetBounds.yMax - bottomLimit;
                targetNeedsScroll = true;
            }

            var maxOffsetY = Mathf.Max(0f, GetElementHeight(SettingsContainer) - GetElementHeight(viewport));
            nextOffsetY = Mathf.Clamp(nextOffsetY, 0f, maxOffsetY);
            if (maxOffsetY <= ScrollOffsetEpsilon && targetNeedsScroll)
            {
                settingsList.ScrollTo(target);
                return true;
            }

            var currentOffset = settingsList.scrollOffset;
            if (Mathf.Abs(currentOffset.y - nextOffsetY) <= ScrollOffsetEpsilon)
            {
                if (targetNeedsScroll)
                {
                    settingsList.ScrollTo(target);
                    return true;
                }

                return false;
            }

            settingsList.scrollOffset = new Vector2(currentOffset.x, nextOffsetY);
            return true;
        }

        /// <summary>
        /// Возвращает полную высоту элемента для расчета прокрутки ScrollView.
        /// </summary>
        private static float GetElementHeight(VisualElement element)
        {
            if (element == null)
            {
                return 0f;
            }

            if (element.layout.height > 0f)
            {
                return element.layout.height;
            }

            if (element.resolvedStyle.height > 0f)
            {
                return element.resolvedStyle.height;
            }

            return element.worldBound.height;
        }

        /// <summary>
        /// Проверяет, находится ли элемент внутри указанного визуального дерева.
        /// </summary>
        private static bool ContainsElement(VisualElement root, VisualElement element)
        {
            for (var current = element; current != null; current = current.parent)
            {
                if (current == root)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Плавно перемещает общий указатель к новой позиции.
        /// </summary>
        /// <param name="targetPosition">Целевая позиция в координатах модального окна.</param>
        private void AnimateFocusPointerTo(Vector2 targetPosition)
        {
            if (!hasFocusPointerPosition)
            {
                SetFocusPointerPosition(targetPosition);
                return;
            }

            focusPointerAnimation?.Pause();
            var startPosition = focusPointerPosition;
            var startedAt = Time.unscaledTime;
            focusPointerAnimation = focusPointer.schedule.Execute(() =>
            {
                var progress = Mathf.Clamp01((Time.unscaledTime - startedAt) / FocusPointerAnimationDuration);
                var easedProgress = Mathf.SmoothStep(0f, 1f, progress);
                SetFocusPointerPosition(Vector2.Lerp(startPosition, targetPosition, easedProgress));
            }).Every(FocusPointerAnimationFrameMs).Until(() => Time.unscaledTime - startedAt >= FocusPointerAnimationDuration);
        }

        /// <summary>
        /// Ставит общий указатель в конкретную позицию без дополнительных вычислений.
        /// </summary>
        /// <param name="position">Позиция в координатах модального окна.</param>
        private void SetFocusPointerPosition(Vector2 position)
        {
            focusPointerPosition = position;
            hasFocusPointerPosition = true;
            focusPointer.style.left = position.x;
            focusPointer.style.top = position.y;
        }

        /// <summary>
        /// Скрывает общий указатель фокуса.
        /// </summary>
        private void HideFocusPointer()
        {
            if (focusPointer != null)
            {
                focusPointer.style.display = DisplayStyle.None;
            }
        }

        /// <summary>
        /// Возвращает указатель к последней известной строке, не меняя фокус.
        /// </summary>
        private void KeepFocusPointerOnCurrentTarget()
        {
            if (currentFocusPointerTarget != null && IsSettingsNavigationTargetVisible(currentFocusPointerTarget))
            {
                MoveFocusPointerTo(currentFocusPointerTarget);
            }
        }

        /// <summary>
        /// Команды навигации, которые окно настроек обрабатывает отдельно от общего стека модалок.
        /// </summary>
        private enum SettingsNavigationCommand
        {
            Up,
            Down,
            Left,
            Right,
        }

        /// <summary>
        /// Источник команды навигации, нужен для отсечения дублей от UI Toolkit.
        /// </summary>
        private enum SettingsNavigationSource
        {
            NavigationMove,
            KeyDown,
        }

        /// <summary>
        /// Команды изменения значения настройки при горизонтальной навигации.
        /// </summary>
        private sealed class SettingAdjustment
        {
            public SettingAdjustment(Func<bool> decrement, Func<bool> increment)
            {
                Decrement = decrement;
                Increment = increment;
            }

            /// <summary>
            /// Уменьшает значение или выбирает предыдущий вариант.
            /// </summary>
            public Func<bool> Decrement { get; }

            /// <summary>
            /// Увеличивает значение или выбирает следующий вариант.
            /// </summary>
            public Func<bool> Increment { get; }
        }

        /// <summary>
        /// Возвращает безопасное название группы настроек.
        /// </summary>
        private static string GetGroupName(string groupName)
        {
            return string.IsNullOrWhiteSpace(groupName) ? "Общие настройки" : groupName;
        }

        /// <summary>
        /// Показывает текстовое состояние внутри списка настроек.
        /// </summary>
        /// <param name="text">Сообщение для оператора.</param>
        private void AddStatus(string text)
        {
            SetStatus(text);

            var emptyState = new Label(text);
            emptyState.AddToClassList("settings-empty-state");
            SettingsContainer.Add(emptyState);
        }

        /// <summary>
        /// Обновляет служебную строку состояния окна.
        /// </summary>
        /// <param name="text">Текст статуса.</param>
        private void SetStatus(string text)
        {
            if (statusLabel != null)
            {
                statusLabel.text = text;
            }
        }

        /// <summary>
        /// Сохраняет текущие значения сервисных настроек в репозиторий.
        /// </summary>
        private void SaveSettings()
        {
            settingsService?.Save();
            SetStatus("Настройки сохранены.");
            RequestClose();
        }

        /// <summary>
        /// Сбрасывает все сервисные настройки к значениям по умолчанию.
        /// </summary>
        private void ResetSettings()
        {
            settingsService?.ResetAllToDefaults();
            BuildSettingsList();
            if (IsOpen)
            {
                FocusDefault();
            }

            SetStatus("Настройки сброшены к значениям по умолчанию.");
        }

        /// <summary>
        /// Перечитывает настройки из внешнего файла и обновляет содержимое окна.
        /// </summary>
        private void ReloadSettings()
        {
            settingsService?.ReloadExternalChanges();
            BuildSettingsList();
            if (IsOpen)
            {
                FocusDefault();
            }

            SetStatus("Настройки перечитаны из внешнего файла.");
        }
    }
}
