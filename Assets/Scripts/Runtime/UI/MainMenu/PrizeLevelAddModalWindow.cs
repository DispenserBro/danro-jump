using System;
using DanroJump.Prizes;
using DanroJump.UI.Modals;
using UnityEngine;
using UnityEngine.UIElements;

namespace DanroJump.UI.MainMenu
{
    /// <summary>
    /// Модальное окно добавления призового порога по очкам.
    /// </summary>
    public sealed class PrizeLevelAddModalWindow : ModalWindowBase, IDisposable
    {
        private const int ScoreStep = 10;

        private readonly Label scoreLabel;
        private readonly Label kindLabel;
        private readonly Label statusLabel;
        private readonly Button scoreMinusButton;
        private readonly Button scorePlusButton;
        private readonly Button kindPreviousButton;
        private readonly Button kindNextButton;
        private readonly Button saveButton;
        private readonly Button cancelButton;
        private int score = 100;
        private int lastNavigationSubmitFrame = -1;
        private PrizeRewardKind rewardKind = PrizeRewardKind.QRCode;

        public PrizeLevelAddModalWindow(VisualElement root)
            : base(root)
        {
            if (root == null)
            {
                return;
            }

            scoreLabel = root.Q<Label>("PrizeLevelAddScoreValue");
            kindLabel = root.Q<Label>("PrizeLevelAddKindValue");
            statusLabel = root.Q<Label>("PrizeLevelAddStatus");
            scoreMinusButton = root.Q<Button>("PrizeLevelAddScoreMinusButton");
            scorePlusButton = root.Q<Button>("PrizeLevelAddScorePlusButton");
            kindPreviousButton = root.Q<Button>("PrizeLevelAddKindPreviousButton");
            kindNextButton = root.Q<Button>("PrizeLevelAddKindNextButton");
            saveButton = root.Q<Button>("PrizeLevelAddSaveButton");
            cancelButton = root.Q<Button>("PrizeLevelAddCancelButton");

            root.RegisterCallback<NavigationSubmitEvent>(HandleNavigationSubmit, TrickleDown.TrickleDown);

            if (scoreMinusButton != null)
            {
                scoreMinusButton.clicked += HandleScoreMinusClicked;
            }

            if (scorePlusButton != null)
            {
                scorePlusButton.clicked += HandleScorePlusClicked;
            }

            if (kindPreviousButton != null)
            {
                kindPreviousButton.clicked += HandleKindPreviousClicked;
            }

            if (kindNextButton != null)
            {
                kindNextButton.clicked += HandleKindNextClicked;
            }

            if (saveButton != null)
            {
                saveButton.clicked += HandleSaveClicked;
            }

            if (cancelButton != null)
            {
                cancelButton.clicked += HandleCancelClicked;
            }
        }

        public event Action<int, PrizeRewardKind> Saved;

        public void SetInitialScore(int value)
        {
            score = Mathf.Max(0, value);
            rewardKind = PrizeRewardKind.QRCode;
            Refresh();
        }

        public override void Open()
        {
            lastNavigationSubmitFrame = -1;
            Refresh();
            base.Open();
        }

        public void Dispose()
        {
            if (Root != null)
            {
                Root.UnregisterCallback<NavigationSubmitEvent>(HandleNavigationSubmit, TrickleDown.TrickleDown);
            }

            if (scoreMinusButton != null)
            {
                scoreMinusButton.clicked -= HandleScoreMinusClicked;
            }

            if (scorePlusButton != null)
            {
                scorePlusButton.clicked -= HandleScorePlusClicked;
            }

            if (kindPreviousButton != null)
            {
                kindPreviousButton.clicked -= HandleKindPreviousClicked;
            }

            if (kindNextButton != null)
            {
                kindNextButton.clicked -= HandleKindNextClicked;
            }

            if (saveButton != null)
            {
                saveButton.clicked -= HandleSaveClicked;
            }

            if (cancelButton != null)
            {
                cancelButton.clicked -= HandleCancelClicked;
            }
        }

        public override bool HandleNavigationMove(VisualElement current, NavigationMoveEvent.Direction direction)
        {
            switch (direction)
            {
                case NavigationMoveEvent.Direction.Left:
                    return AdjustFocusedValue(current, -1);
                case NavigationMoveEvent.Direction.Right:
                    return AdjustFocusedValue(current, 1);
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
                    return AdjustFocusedValue(current, -1);
                case KeyCode.RightArrow:
                case KeyCode.D:
                    return AdjustFocusedValue(current, 1);
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
            return scorePlusButton ?? kindNextButton ?? saveButton ?? cancelButton;
        }

        private void DecreaseScore()
        {
            score = Mathf.Max(0, score - ScoreStep);
            Refresh();
        }

        private void IncreaseScore()
        {
            score = Mathf.Max(0, score + ScoreStep);
            Refresh();
        }

        private void SelectPreviousKind()
        {
            rewardKind = PrizeLevelUiFormatter.PreviousKind(rewardKind);
            Refresh();
        }

        private void SelectNextKind()
        {
            rewardKind = PrizeLevelUiFormatter.NextKind(rewardKind);
            Refresh();
        }

        private void Save()
        {
            Saved?.Invoke(score, rewardKind);
            RequestClose();
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

        private void HandleScoreMinusClicked()
        {
            if (IsDuplicateNavigationSubmitClick())
            {
                return;
            }

            DecreaseScore();
        }

        private void HandleScorePlusClicked()
        {
            if (IsDuplicateNavigationSubmitClick())
            {
                return;
            }

            IncreaseScore();
        }

        private void HandleKindPreviousClicked()
        {
            if (IsDuplicateNavigationSubmitClick())
            {
                return;
            }

            SelectPreviousKind();
        }

        private void HandleKindNextClicked()
        {
            if (IsDuplicateNavigationSubmitClick())
            {
                return;
            }

            SelectNextKind();
        }

        private void HandleSaveClicked()
        {
            if (IsDuplicateNavigationSubmitClick())
            {
                return;
            }

            Save();
        }

        private void HandleCancelClicked()
        {
            if (IsDuplicateNavigationSubmitClick())
            {
                return;
            }

            RequestClose();
        }

        private bool AdjustFocusedValue(VisualElement current, int direction)
        {
            var focused = ResolveFocusedElement(current);
            if (IsScoreControl(focused))
            {
                if (direction < 0)
                {
                    DecreaseScore();
                }
                else
                {
                    IncreaseScore();
                }

                return true;
            }

            if (IsKindControl(focused))
            {
                if (direction < 0)
                {
                    SelectPreviousKind();
                }
                else
                {
                    SelectNextKind();
                }

                return true;
            }

            return false;
        }

        private bool ActivateFocusedElement(VisualElement current)
        {
            var focused = ResolveFocusedElement(current);
            if (focused == scoreMinusButton)
            {
                DecreaseScore();
                return true;
            }

            if (focused == scorePlusButton)
            {
                IncreaseScore();
                return true;
            }

            if (focused == kindPreviousButton)
            {
                SelectPreviousKind();
                return true;
            }

            if (focused == kindNextButton)
            {
                SelectNextKind();
                return true;
            }

            if (focused == saveButton)
            {
                Save();
                return true;
            }

            if (focused == cancelButton)
            {
                RequestClose();
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

        private bool IsScoreControl(VisualElement element)
        {
            return element == scoreMinusButton || element == scorePlusButton;
        }

        private bool IsKindControl(VisualElement element)
        {
            return element == kindPreviousButton || element == kindNextButton;
        }

        private bool IsDuplicateNavigationSubmitClick()
        {
            return Time.frameCount == lastNavigationSubmitFrame;
        }

        private void Refresh()
        {
            if (scoreLabel != null)
            {
                scoreLabel.text = score.ToString();
            }

            if (kindLabel != null)
            {
                kindLabel.text = PrizeLevelUiFormatter.FormatKind(rewardKind);
            }

            if (statusLabel != null)
            {
                statusLabel.text = "Если такой порог уже есть, его тип приза будет обновлен.";
            }
        }
    }
}
