using System;
using System.Collections.Generic;
using DanroJump.Prizes;
using DanroJump.Settings;
using DanroJump.UI.Modals;
using UnityEngine;
using UnityEngine.UIElements;

namespace DanroJump.UI.MainMenu
{
    /// <summary>
    /// Сервисная модалка списка призовых уровней по очкам.
    /// </summary>
    public sealed class PrizeLevelsModalWindow : ModalWindowBase, IDisposable
    {
        private readonly ScrollView levelsList;
        private readonly Label statusLabel;
        private readonly Button addButton;
        private readonly Button closeButton;
        private readonly Dictionary<Button, Action> dynamicButtonActions = new();
        private IServiceSettingsService settingsService;
        private int lastNavigationSubmitFrame = -1;

        public PrizeLevelsModalWindow(VisualElement root, IServiceSettingsService settingsService)
            : base(root)
        {
            if (root == null)
            {
                return;
            }

            this.settingsService = settingsService;
            levelsList = root.Q<ScrollView>("PrizeLevelsList");
            statusLabel = root.Q<Label>("PrizeLevelsStatus");
            addButton = root.Q<Button>("PrizeLevelsAddButton");
            closeButton = root.Q<Button>("PrizeLevelsCloseButton");

            root.RegisterCallback<NavigationSubmitEvent>(HandleNavigationSubmit, TrickleDown.TrickleDown);

            if (addButton != null)
            {
                addButton.clicked += HandleAddClicked;
            }

            if (closeButton != null)
            {
                closeButton.clicked += HandleCloseClicked;
            }
        }

        public event Action AddRequested;

        private VisualElement LevelsContainer => levelsList?.contentContainer ?? levelsList;

        public void SetSettingsService(IServiceSettingsService service)
        {
            settingsService = service;
        }

        public override void Open()
        {
            lastNavigationSubmitFrame = -1;
            BuildList();
            base.Open();
        }

        public void AddOrUpdateLevel(int score, PrizeRewardKind rewardKind)
        {
            if (settingsService == null)
            {
                SetStatus("Сервис настроек недоступен. Уровень не сохранен.");
                return;
            }

            var table = LoadTable();
            table.Upsert(score, rewardKind);
            if (!SaveTable(table))
            {
                return;
            }

            BuildList();
        }

        public int GetNextSuggestedScore()
        {
            var table = LoadTable();
            if (table.Count == 0)
            {
                return 100;
            }

            return table.Levels[^1].Score + 100;
        }

        public void Dispose()
        {
            if (addButton != null)
            {
                addButton.clicked -= HandleAddClicked;
            }

            if (closeButton != null)
            {
                closeButton.clicked -= HandleCloseClicked;
            }

            if (Root != null)
            {
                Root.UnregisterCallback<NavigationSubmitEvent>(HandleNavigationSubmit, TrickleDown.TrickleDown);
            }
        }

        public override bool HandleNavigationMove(VisualElement current, NavigationMoveEvent.Direction direction)
        {
            switch (direction)
            {
                case NavigationMoveEvent.Direction.Left:
                case NavigationMoveEvent.Direction.Up:
                    return FocusRelativeTo(ResolveFocusedElement(current), -1);
                case NavigationMoveEvent.Direction.Right:
                case NavigationMoveEvent.Direction.Down:
                    return FocusRelativeTo(ResolveFocusedElement(current), 1);
                default:
                    return false;
            }
        }

        public override bool HandleNavigationKey(VisualElement current, KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.LeftArrow:
                case KeyCode.A:
                    return FocusRelativeTo(ResolveFocusedElement(current), -1);
                case KeyCode.RightArrow:
                case KeyCode.D:
                    return FocusRelativeTo(ResolveFocusedElement(current), 1);
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Space:
                case KeyCode.JoystickButton0:
                    return ActivateFocusedElement(current);
                default:
                    return false;
            }
        }

        protected override VisualElement GetDefaultFocusElement()
        {
            return addButton ?? closeButton;
        }

        private void RequestAdd()
        {
            AddRequested?.Invoke();
        }

        private void HandleNavigationSubmit(NavigationSubmitEvent evt)
        {
            evt.StopImmediatePropagation();

            if (Time.frameCount == lastNavigationSubmitFrame)
            {
                return;
            }

            lastNavigationSubmitFrame = Time.frameCount;
            ActivateFocusedElement(evt.target as VisualElement);
        }

        private void HandleAddClicked()
        {
            if (IsDuplicateNavigationSubmitClick())
            {
                return;
            }

            RequestAdd();
        }

        private void HandleCloseClicked()
        {
            if (IsDuplicateNavigationSubmitClick())
            {
                return;
            }

            RequestClose();
        }

        private void HandleDeleteClicked(int index)
        {
            if (IsDuplicateNavigationSubmitClick())
            {
                return;
            }

            DeleteLevel(index);
        }

        private void BuildList()
        {
            if (LevelsContainer == null)
            {
                return;
            }

            dynamicButtonActions.Clear();
            LevelsContainer.Clear();

            if (settingsService == null)
            {
                SetStatus("Сервис настроек пока недоступен.");
                return;
            }

            var table = LoadTable();
            if (table.Count == 0)
            {
                AddEmptyState();
                SetStatus("Таблица уровней пустая. Пока используется legacy-логика призовых флагов.");
                RebuildFocusRing();
                return;
            }

            for (var index = 0; index < table.Count; index++)
            {
                AddRow(table.Levels[index], index);
            }

            SetStatus($"Настроено уровней: {table.Count}. Для счета берется последний подходящий порог.");
            RebuildFocusRing();
        }

        private void AddRow(PrizeLevelEntry entry, int index)
        {
            var row = new VisualElement();
            row.AddToClassList("prize-level-row");

            var score = new Label($"от {entry.Score} очков");
            score.AddToClassList("prize-level-score");
            row.Add(score);

            var kind = new Label(PrizeLevelUiFormatter.FormatKind(entry.RewardKind));
            kind.AddToClassList("prize-level-kind");
            row.Add(kind);

            var deleteButton = new Button(() => HandleDeleteClicked(index))
            {
                text = "УДАЛИТЬ"
            };
            deleteButton.AddToClassList("modal-button");
            deleteButton.AddToClassList("prize-level-delete-button");
            dynamicButtonActions[deleteButton] = () => DeleteLevel(index);
            row.Add(deleteButton);

            LevelsContainer.Add(row);
        }

        private void AddEmptyState()
        {
            var empty = new Label("Добавьте первый порог очков для малого, большого или пустого приза.");
            empty.AddToClassList("prize-level-empty");
            LevelsContainer.Add(empty);
        }

        private void DeleteLevel(int index)
        {
            var table = LoadTable();
            if (!table.RemoveAt(index))
            {
                SetStatus("Не удалось удалить уровень: список изменился.");
                return;
            }

            if (SaveTable(table))
            {
                BuildList();
            }
        }

        private bool ActivateFocusedElement(VisualElement current)
        {
            var focused = ResolveFocusedElement(current);
            if (focused == addButton)
            {
                RequestAdd();
                return true;
            }

            if (focused == closeButton)
            {
                RequestClose();
                return true;
            }

            if (focused is Button button && dynamicButtonActions.TryGetValue(button, out var action))
            {
                action.Invoke();
                return true;
            }

            return false;
        }

        private VisualElement ResolveFocusedElement(VisualElement current)
        {
            return ResolveButton(current) ??
                ResolveButton(Root?.panel?.focusController?.focusedElement as VisualElement) ??
                LastFocusedElement ??
                GetDefaultFocusElement();
        }

        private static Button ResolveButton(VisualElement element)
        {
            for (var current = element; current != null; current = current.parent)
            {
                if (current is Button button)
                {
                    return button;
                }
            }

            return null;
        }

        private bool IsDuplicateNavigationSubmitClick()
        {
            return Time.frameCount == lastNavigationSubmitFrame;
        }

        private PrizeLevelTable LoadTable()
        {
            return PrizeLevelTable.FromJson(settingsService?.GetString(ServiceSettingsKeys.PrizeLevels));
        }

        private bool SaveTable(PrizeLevelTable table)
        {
            if (settingsService == null)
            {
                SetStatus("Сервис настроек недоступен. Изменение не сохранено.");
                return false;
            }

            if (!settingsService.SetString(ServiceSettingsKeys.PrizeLevels, table.ToJson()))
            {
                SetStatus("Не удалось сохранить уровни: ключ prizes.levels не найден в базе настроек.");
                return false;
            }

            settingsService.Save();
            SetStatus("Уровни призов сохранены.");
            return true;
        }

        private void SetStatus(string value)
        {
            if (statusLabel != null)
            {
                statusLabel.text = value;
            }
        }
    }
}
