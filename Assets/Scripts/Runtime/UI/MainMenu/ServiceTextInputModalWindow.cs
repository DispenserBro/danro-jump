using System;
using System.Collections.Generic;
using DanroJump.Settings;
using DanroJump.UI.ArcadeInput;
using DanroJump.UI.Modals;
using UnityEngine;
using UnityEngine.UIElements;

namespace DanroJump.UI.MainMenu
{
    /// <summary>
    /// Модальное окно посимвольного ввода для автомата с джойстиком и одной кнопкой.
    /// </summary>
    public sealed class ServiceTextInputModalWindow : ModalWindowBase, IDisposable
    {
        private const string ActiveSlotClass = "service-text-input-slot--active";
        private const string ActiveActionClass = "service-text-input-action--active";
        private const float NavigationMoveDebounceSeconds = 0.36f;
        private const float NavigationKeyDebounceSeconds = 0.16f;
        private const float NavigationSubmitDebounceSeconds = 0.22f;

        private readonly Label titleLabel;
        private readonly Label settingLabel;
        private readonly Label previewLabel;
        private readonly Label symbolLabel;
        private readonly Label statusLabel;
        private readonly VisualElement slotsContainer;
        private readonly Button confirmButton;
        private readonly Button deleteButton;
        private readonly Button cancelButton;
        private readonly List<Label> slotLabels = new();

        private ArcadeStringInputState state;
        private Action<string> confirmed;
        private NavigationMoveEvent.Direction lastDirectionalStep;
        private int lastDirectionalStepFrame = -1;
        private float lastDirectionalStepBlockedUntil = float.NegativeInfinity;
        private int lastNavigationSubmitFrame = -1;
        private float lastNavigationSubmitTime = float.NegativeInfinity;

        public ServiceTextInputModalWindow(VisualElement root)
            : base(root)
        {
            if (root == null)
            {
                return;
            }

            titleLabel = root.Q<Label>("ServiceTextInputTitle");
            settingLabel = root.Q<Label>("ServiceTextInputSetting");
            previewLabel = root.Q<Label>("ServiceTextInputPreview");
            symbolLabel = root.Q<Label>("ServiceTextInputSymbol");
            statusLabel = root.Q<Label>("ServiceTextInputStatus");
            slotsContainer = root.Q<VisualElement>("ServiceTextInputSlots");
            confirmButton = root.Q<Button>("ServiceTextInputConfirmButton");
            deleteButton = root.Q<Button>("ServiceTextInputDeleteButton");
            cancelButton = root.Q<Button>("ServiceTextInputCancelButton");

            root.RegisterCallback<NavigationSubmitEvent>(HandleNavigationSubmit, TrickleDown.TrickleDown);

            if (confirmButton != null)
            {
                confirmButton.clicked += HandleConfirmButtonClicked;
            }

            if (deleteButton != null)
            {
                deleteButton.clicked += HandleDeleteButtonClicked;
            }

            if (cancelButton != null)
            {
                cancelButton.clicked += HandleCancelButtonClicked;
            }
        }

        public void SetContext(
            string title,
            string settingName,
            string currentValue,
            ArcadeStringInputProfile profile,
            Action<string> confirmedCallback)
        {
            state = new ArcadeStringInputState(profile, currentValue);
            confirmed = confirmedCallback;
            lastDirectionalStepFrame = -1;
            lastDirectionalStepBlockedUntil = float.NegativeInfinity;
            lastNavigationSubmitFrame = -1;
            lastNavigationSubmitTime = float.NegativeInfinity;

            if (titleLabel != null)
            {
                titleLabel.text = title;
            }

            if (settingLabel != null)
            {
                settingLabel.text = settingName;
            }

            BuildSlotLabels();
            RefreshView();
        }

        public void SetContext(
            ServiceSettingDefinition definition,
            string currentValue,
            ArcadeStringInputProfile profile,
            Action<string> confirmedCallback)
        {
            var settingName = definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName)
                ? definition.DisplayName
                : definition?.Key ?? string.Empty;

            SetContext("Ввод значения", settingName, currentValue, profile, confirmedCallback);
        }

        public override void Open()
        {
            base.Open();
            RefreshView();
        }

        public void Dispose()
        {
            if (Root != null)
            {
                Root.UnregisterCallback<NavigationSubmitEvent>(HandleNavigationSubmit, TrickleDown.TrickleDown);
            }

            if (confirmButton != null)
            {
                confirmButton.clicked -= HandleConfirmButtonClicked;
            }

            if (deleteButton != null)
            {
                deleteButton.clicked -= HandleDeleteButtonClicked;
            }

            if (cancelButton != null)
            {
                cancelButton.clicked -= HandleCancelButtonClicked;
            }
        }

        public override void Close()
        {
            confirmed = null;
            base.Close();
        }

        public override bool HandleNavigationMove(VisualElement current, NavigationMoveEvent.Direction direction)
        {
            if (state == null)
            {
                return false;
            }

            if (!TryAcceptDirectionalStep(direction, NavigationMoveDebounceSeconds))
            {
                return true;
            }

            switch (direction)
            {
                case NavigationMoveEvent.Direction.Up:
                    state.MoveUp();
                    break;
                case NavigationMoveEvent.Direction.Down:
                    state.MoveDown();
                    break;
                case NavigationMoveEvent.Direction.Left:
                    state.MoveLeft();
                    break;
                case NavigationMoveEvent.Direction.Right:
                    state.MoveRight();
                    break;
                default:
                    return false;
            }

            RefreshView();
            return true;
        }

        public override bool HandleNavigationKey(VisualElement current, KeyCode keyCode)
        {
            if (state == null)
            {
                return false;
            }

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                case KeyCode.W:
                    if (!TryAcceptDirectionalStep(NavigationMoveEvent.Direction.Up, NavigationKeyDebounceSeconds))
                    {
                        return true;
                    }

                    state.MoveUp();
                    break;
                case KeyCode.DownArrow:
                case KeyCode.S:
                    if (!TryAcceptDirectionalStep(NavigationMoveEvent.Direction.Down, NavigationKeyDebounceSeconds))
                    {
                        return true;
                    }

                    state.MoveDown();
                    break;
                case KeyCode.LeftArrow:
                case KeyCode.A:
                    if (!TryAcceptDirectionalStep(NavigationMoveEvent.Direction.Left, NavigationKeyDebounceSeconds))
                    {
                        return true;
                    }

                    state.MoveLeft();
                    break;
                case KeyCode.RightArrow:
                case KeyCode.D:
                    if (!TryAcceptDirectionalStep(NavigationMoveEvent.Direction.Right, NavigationKeyDebounceSeconds))
                    {
                        return true;
                    }

                    state.MoveRight();
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Space:
                    Submit();
                    return true;
                default:
                    return false;
            }

            RefreshView();
            return true;
        }

        protected override VisualElement GetDefaultFocusElement()
        {
            return confirmButton ?? Root;
        }

        public bool Confirm()
        {
            if (state == null || confirmed == null)
            {
                return false;
            }

            var confirmedCallback = confirmed;
            confirmed = null;
            RequestClose();
            confirmedCallback.Invoke(state.Value);
            return true;
        }

        public void DeleteCurrentCharacter()
        {
            state?.DeleteCurrentCharacter();
            RefreshView();
        }

        private void HandleNavigationSubmit(NavigationSubmitEvent evt)
        {
            evt.StopImmediatePropagation();

            var now = Time.unscaledTime;
            if (Time.frameCount == lastNavigationSubmitFrame ||
                now - lastNavigationSubmitTime < NavigationSubmitDebounceSeconds)
            {
                return;
            }

            lastNavigationSubmitFrame = Time.frameCount;
            lastNavigationSubmitTime = now;
            Submit();
        }

        private void HandleConfirmButtonClicked()
        {
            if (Time.frameCount == lastNavigationSubmitFrame)
            {
                return;
            }

            Confirm();
        }

        private void HandleDeleteButtonClicked()
        {
            if (Time.frameCount == lastNavigationSubmitFrame)
            {
                return;
            }

            DeleteCurrentCharacter();
        }

        private void HandleCancelButtonClicked()
        {
            if (Time.frameCount == lastNavigationSubmitFrame)
            {
                return;
            }

            RequestClose();
        }

        private bool TryAcceptDirectionalStep(NavigationMoveEvent.Direction direction, float debounceSeconds)
        {
            var now = Time.unscaledTime;
            if (direction == lastDirectionalStep &&
                (Time.frameCount == lastDirectionalStepFrame ||
                    now < lastDirectionalStepBlockedUntil))
            {
                return false;
            }

            lastDirectionalStep = direction;
            lastDirectionalStepFrame = Time.frameCount;
            lastDirectionalStepBlockedUntil = now + Mathf.Max(0f, debounceSeconds);
            return true;
        }

        private bool Submit()
        {
            if (state == null)
            {
                return false;
            }

            var result = state.Submit();
            switch (result)
            {
                case ArcadeStringInputSubmitResult.Confirm:
                    return Confirm();
                case ArcadeStringInputSubmitResult.Cancel:
                    RequestClose();
                    return true;
                default:
                    RefreshView();
                    return true;
            }
        }

        private void BuildSlotLabels()
        {
            slotLabels.Clear();
            slotsContainer?.Clear();
            if (slotsContainer == null || state == null)
            {
                return;
            }

            for (var index = 0; index < state.MaxLength; index++)
            {
                var label = new Label();
                label.AddToClassList("service-text-input-slot");
                label.focusable = false;
                slotsContainer.Add(label);
                slotLabels.Add(label);
            }
        }

        private void RefreshView()
        {
            if (state == null)
            {
                return;
            }

            if (previewLabel != null)
            {
                previewLabel.text = string.IsNullOrEmpty(state.Value) ? " " : state.Value;
            }

            for (var index = 0; index < slotLabels.Count; index++)
            {
                var label = slotLabels[index];
                label.text = FormatCharacter(state.GetSlot(index));

                if (state.IsSlotSelected(index))
                {
                    label.AddToClassList(ActiveSlotClass);
                }
                else
                {
                    label.RemoveFromClassList(ActiveSlotClass);
                }
            }

            if (symbolLabel != null)
            {
                symbolLabel.text = state.SelectionKind == ArcadeStringInputSelectionKind.Character
                    ? FormatCharacter(state.GetSlot(state.SelectedSlot))
                    : FormatAction(state.SelectedAction);
            }

            bool isAnyActionSelected = false;

            RefreshActionButton(confirmButton, ArcadeStringInputAction.Confirm, ref isAnyActionSelected);
            RefreshActionButton(deleteButton, ArcadeStringInputAction.Delete, ref isAnyActionSelected);
            RefreshActionButton(cancelButton, ArcadeStringInputAction.Cancel, ref isAnyActionSelected);

            if (!isAnyActionSelected && Root != null)
            {
                Root.Focus();
            }
        }

        private void RefreshActionButton(Button button, ArcadeStringInputAction action, ref bool isAnyActionSelected)
        {
            if (button == null)
            {
                return;
            }

            if (state.IsActionSelected(action))
            {
                button.AddToClassList(ActiveActionClass);
                button.Focus();
                isAnyActionSelected = true;
                return;
            }

            button.RemoveFromClassList(ActiveActionClass);
        }

        private void SetStatus(string status)
        {
            if (statusLabel != null)
            {
                statusLabel.text = status;
            }
        }

        private static string FormatCharacter(char character)
        {
            return character == ArcadeStringInputState.BlankCharacter ? "_" : character.ToString();
        }

        private static string FormatAction(ArcadeStringInputAction action)
        {
            return action switch
            {
                ArcadeStringInputAction.Confirm => "OK",
                ArcadeStringInputAction.Delete => "Удалить",
                ArcadeStringInputAction.Cancel => "Отмена",
                _ => string.Empty
            };
        }
    }
}
