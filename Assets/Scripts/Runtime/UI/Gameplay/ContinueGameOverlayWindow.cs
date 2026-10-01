using System;
using UnityEngine;
using UnityEngine.UIElements;
using DanroJump.Audio;

namespace DanroJump.UI.Gameplay
{
    /// <summary>
    /// Полноэкранное предложение продолжить забег за баланс.
    /// </summary>
    public sealed class ContinueGameOverlayWindow : IDisposable
    {
        private const float OpenInputGuardSeconds = 0.45f;

        private readonly VisualElement root;
        private readonly Label titleLabel;
        private readonly Label detailsLabel;
        private readonly Button continueButton;
        private readonly Button menuButton;
        private readonly IGameAudioService audioService;
        private float inputEnabledAtTime = float.NegativeInfinity;

        public ContinueGameOverlayWindow(VisualElement root, IGameAudioService audioService = null)
        {
            this.root = root;
            this.audioService = audioService;

            titleLabel = root?.Q<Label>("ContinueGameTitle");
            detailsLabel = root?.Q<Label>("ContinueGameDetails");
            continueButton = root?.Q<Button>("ContinueGameButton");
            menuButton = root?.Q<Button>("ContinueMenuButton");

            if (continueButton != null)
            {
                continueButton.clicked += HandleContinueClicked;
                continueButton.RegisterCallback<FocusInEvent>(HandleButtonFocusIn);
                continueButton.RegisterCallback<FocusOutEvent>(HandleButtonFocusOut);
            }

            if (menuButton != null)
            {
                menuButton.clicked += HandleMenuClicked;
                menuButton.RegisterCallback<FocusInEvent>(HandleButtonFocusIn);
                menuButton.RegisterCallback<FocusOutEvent>(HandleButtonFocusOut);
            }

            if (root != null)
            {
                root.RegisterCallback<KeyDownEvent>(HandleKeyDown, TrickleDown.TrickleDown);
                root.RegisterCallback<NavigationMoveEvent>(HandleNavigationMove, TrickleDown.TrickleDown);
            }
        }

        public event Action ContinueRequested;
        public event Action MenuRequested;

        public bool IsOpen { get; private set; }

        public void SetOffer(int cost, int lives)
        {
            SetLabel(titleLabel, "Продолжить игру?");
            SetLabel(detailsLabel, $"Стоимость: {Math.Max(0, cost)}. Дополнительные жизни: {Math.Max(1, lives)}.");
            if (continueButton != null)
            {
                continueButton.SetEnabled(true);
            }
        }

        public void SetUnavailable()
        {
            SetLabel(titleLabel, "Игра окончена");
            SetLabel(detailsLabel, "Недостаточно баланса или функция отключена в сервисных настройках.");
            if (continueButton != null)
            {
                continueButton.SetEnabled(false);
            }
        }

        public void Open()
        {
            if (root == null)
            {
                return;
            }

            root.style.display = DisplayStyle.Flex;
            root.SetEnabled(true);
            IsOpen = true;
            inputEnabledAtTime = Time.unscaledTime + OpenInputGuardSeconds;

            FocusDefaultButton();
        }

        private void FocusDefaultButton()
        {
            if (continueButton != null && continueButton.enabledSelf)
            {
                continueButton.Focus();
            }
            else if (menuButton != null)
            {
                menuButton.Focus();
            }
        }

        public void Close()
        {
            if (root == null)
            {
                return;
            }

            root.style.display = DisplayStyle.None;
            root.SetEnabled(false);
            IsOpen = false;
        }

        public void Dispose()
        {
            if (continueButton != null)
            {
                continueButton.clicked -= HandleContinueClicked;
                continueButton.UnregisterCallback<FocusInEvent>(HandleButtonFocusIn);
                continueButton.UnregisterCallback<FocusOutEvent>(HandleButtonFocusOut);
            }

            if (menuButton != null)
            {
                menuButton.clicked -= HandleMenuClicked;
                menuButton.UnregisterCallback<FocusInEvent>(HandleButtonFocusIn);
                menuButton.UnregisterCallback<FocusOutEvent>(HandleButtonFocusOut);
            }

            if (root != null)
            {
                root.UnregisterCallback<KeyDownEvent>(HandleKeyDown, TrickleDown.TrickleDown);
                root.UnregisterCallback<NavigationMoveEvent>(HandleNavigationMove, TrickleDown.TrickleDown);
            }
        }

        private static readonly Color FocusedButtonTint = new Color32(0xCE, 0xFF, 0x1A, 0xFF);
        private static readonly Color NormalButtonTint = Color.white;

        private void HandleButtonFocusIn(FocusInEvent evt)
        {
            if (evt.currentTarget is Button button)
            {
                audioService?.Play(GameAudioEvent.UiNavigate);
                button.style.unityBackgroundImageTintColor = new StyleColor(FocusedButtonTint);
            }
        }

        private void HandleButtonFocusOut(FocusOutEvent evt)
        {
            if (evt.currentTarget is Button button)
            {
                button.style.unityBackgroundImageTintColor = new StyleColor(NormalButtonTint);
            }
        }

        private static void SetLabel(Label label, string value)
        {
            if (label != null)
            {
                label.text = value;
            }
        }

        private void HandleContinueClicked()
        {
            if (IsInputGuardActive())
            {
                return;
            }

            ContinueRequested?.Invoke();
        }

        private void HandleMenuClicked()
        {
            if (IsInputGuardActive())
            {
                return;
            }

            MenuRequested?.Invoke();
        }

        private bool IsInputGuardActive()
        {
            return Time.unscaledTime < inputEnabledAtTime;
        }

        private void HandleKeyDown(KeyDownEvent evt)
        {
            if (IsInputGuardActive())
            {
                return;
            }

            var focusedElement = root?.panel?.focusController?.focusedElement as VisualElement;
            
            var direction = 0;
            switch (evt.keyCode)
            {
                case KeyCode.Tab:
                    direction = evt.shiftKey ? -1 : 1;
                    break;
                case KeyCode.LeftArrow:
                case KeyCode.UpArrow:
                case KeyCode.A:
                case KeyCode.W:
                    direction = -1;
                    break;
                case KeyCode.RightArrow:
                case KeyCode.DownArrow:
                case KeyCode.D:
                case KeyCode.S:
                    direction = 1;
                    break;
            }

            if (direction != 0)
            {
                NavigateFocus(focusedElement, direction);
                evt.StopPropagation();
            }
        }

        private void HandleNavigationMove(NavigationMoveEvent evt)
        {
            if (IsInputGuardActive())
            {
                return;
            }

            var focusedElement = root?.panel?.focusController?.focusedElement as VisualElement;
            var direction = 0;
            switch (evt.direction)
            {
                case NavigationMoveEvent.Direction.Left:
                case NavigationMoveEvent.Direction.Up:
                    direction = -1;
                    break;
                case NavigationMoveEvent.Direction.Right:
                case NavigationMoveEvent.Direction.Down:
                    direction = 1;
                    break;
            }

            if (direction != 0)
            {
                NavigateFocus(focusedElement, direction);
                evt.StopPropagation();
            }
        }

        private void NavigateFocus(VisualElement current, int direction)
        {
            if (continueButton == null || menuButton == null)
            {
                return;
            }

            if (!continueButton.enabledSelf)
            {
                menuButton.Focus();
                return;
            }

            if (current == continueButton)
            {
                if (direction > 0)
                {
                    menuButton.Focus();
                }
            }
            else if (current == menuButton)
            {
                if (direction < 0)
                {
                    continueButton.Focus();
                }
            }
            else
            {
                FocusDefaultButton();
            }
        }
    }
}
