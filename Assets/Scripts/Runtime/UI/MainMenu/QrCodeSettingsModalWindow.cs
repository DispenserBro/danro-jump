using System;
using DanroJump.Prizes.Qr;
using DanroJump.Settings;
using DanroJump.UI.Modals;
using UnityEngine;
using UnityEngine.UIElements;

namespace DanroJump.UI.MainMenu
{
    /// <summary>
    /// Сервисная модалка настройки короткого текста QR-приза по рангам.
    /// </summary>
    public sealed class QrCodeSettingsModalWindow : ModalWindowBase, IDisposable
    {
        private static readonly QrPrizeTier[] EditableTiers =
        {
            QrPrizeTier.Bronze,
            QrPrizeTier.Silver,
            QrPrizeTier.Gold
        };

        private readonly Label tierLabel;
        private readonly Label prizeTextLabel;
        private readonly Label statusLabel;
        private readonly Button previousTierButton;
        private readonly Button nextTierButton;
        private readonly Button editTextButton;
        private readonly Button closeButton;

        private IServiceSettingsService settingsService;
        private int tierIndex;

        public QrCodeSettingsModalWindow(VisualElement root, IServiceSettingsService settingsService)
            : base(root)
        {
            this.settingsService = settingsService;

            tierLabel = root?.Q<Label>("QrSettingsTierValue");
            prizeTextLabel = root?.Q<Label>("QrSettingsPrizeTextValue");
            statusLabel = root?.Q<Label>("QrSettingsStatus");
            previousTierButton = root?.Q<Button>("QrSettingsTierPreviousButton");
            nextTierButton = root?.Q<Button>("QrSettingsTierNextButton");
            editTextButton = root?.Q<Button>("QrSettingsEditTextButton");
            closeButton = root?.Q<Button>("QrSettingsCloseButton");

            if (previousTierButton != null)
            {
                previousTierButton.clicked += SelectPreviousTier;
            }

            if (nextTierButton != null)
            {
                nextTierButton.clicked += SelectNextTier;
            }

            if (editTextButton != null)
            {
                editTextButton.clicked += RequestTextEdit;
            }

            if (closeButton != null)
            {
                closeButton.clicked += RequestClose;
            }
        }

        public event Action<QrPrizeTier, string> TextEditRequested;

        private QrPrizeTier CurrentTier => EditableTiers[Mathf.Clamp(tierIndex, 0, EditableTiers.Length - 1)];

        public void SetSettingsService(IServiceSettingsService service)
        {
            settingsService = service;
            RefreshView();
        }

        public override void Open()
        {
            base.Open();
            RefreshView();
        }

        public void Dispose()
        {
            if (previousTierButton != null)
            {
                previousTierButton.clicked -= SelectPreviousTier;
            }

            if (nextTierButton != null)
            {
                nextTierButton.clicked -= SelectNextTier;
            }

            if (editTextButton != null)
            {
                editTextButton.clicked -= RequestTextEdit;
            }

            if (closeButton != null)
            {
                closeButton.clicked -= RequestClose;
            }
        }

        public override bool HandleNavigationMove(VisualElement current, NavigationMoveEvent.Direction direction)
        {
            switch (direction)
            {
                case NavigationMoveEvent.Direction.Left:
                    SelectPreviousTier();
                    return true;
                case NavigationMoveEvent.Direction.Right:
                    SelectNextTier();
                    return true;
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
                    SelectPreviousTier();
                    return true;
                case KeyCode.RightArrow:
                case KeyCode.D:
                    SelectNextTier();
                    return true;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.JoystickButton0:
                    if (current == editTextButton)
                    {
                        RequestTextEdit();
                        return true;
                    }

                    if (current == closeButton)
                    {
                        RequestClose();
                        return true;
                    }

                    return false;
                default:
                    return false;
            }
        }

        protected override VisualElement GetDefaultFocusElement()
        {
            return editTextButton ?? nextTierButton ?? closeButton;
        }

        private void SelectPreviousTier()
        {
            tierIndex = (tierIndex - 1 + EditableTiers.Length) % EditableTiers.Length;
            RefreshView();
        }

        private void SelectNextTier()
        {
            tierIndex = (tierIndex + 1) % EditableTiers.Length;
            RefreshView();
        }

        private void RequestTextEdit()
        {
            TextEditRequested?.Invoke(CurrentTier, QrPrizeTextSettings.GetSettingKey(CurrentTier));
        }

        private void RefreshView()
        {
            var tier = CurrentTier;
            var text = QrPrizeTextSettings.GetPrizeText(settingsService, tier);

            if (tierLabel != null)
            {
                tierLabel.text = QrPrizeTextSettings.GetDisplayName(tier);
            }

            if (prizeTextLabel != null)
            {
                prizeTextLabel.text = string.IsNullOrWhiteSpace(text) ? "<пусто>" : text;
            }

            if (statusLabel != null)
            {
                statusLabel.text = "В QR вводится только текст приза. Телефон и ссылка Telegram собираются автоматически.";
            }
        }
    }
}
