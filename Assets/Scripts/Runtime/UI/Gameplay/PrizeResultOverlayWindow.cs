using System;
using System.Collections.Generic;
using DanroJump.Hardware.Com;
using DanroJump.Prizes;
using DanroJump.Prizes.Qr;
using DanroJump.Audio;
using UnityEngine;
using UnityEngine.UIElements;

namespace DanroJump.UI.Gameplay
{
    /// <summary>
    /// Полноэкранный результат выдачи приза поверх gameplay HUD.
    /// </summary>
    public sealed class PrizeResultOverlayWindow : IDisposable
    {
        private const float OpenInputGuardSeconds = 0.45f;

        private readonly VisualElement root;
        private readonly Label titleLabel;
        private readonly Label subtitleLabel;
        private readonly Label detailsLabel;
        private readonly VisualElement qrContainer;
        private readonly Image qrImage;
        private readonly Label qrPhoneLabel;
        private readonly Label qrMessageLabel;
        private readonly VisualElement prizeStandContainer;
        private readonly Button returnButton;
        private readonly IGameAudioService audioService;
        private float inputEnabledAtTime = float.NegativeInfinity;

        public PrizeResultOverlayWindow(VisualElement root, IGameAudioService audioService = null)
        {
            this.root = root;
            this.audioService = audioService;

            titleLabel = root?.Q<Label>("PrizeResultTitle");
            subtitleLabel = root?.Q<Label>("PrizeResultSubtitle");
            detailsLabel = root?.Q<Label>("PrizeResultDetails");
            qrContainer = root?.Q<VisualElement>("PrizeQrContainer");
            qrImage = root?.Q<Image>("PrizeQrImage");
            qrPhoneLabel = root?.Q<Label>("PrizeQrPhone");
            qrMessageLabel = root?.Q<Label>("PrizeQrMessage");
            prizeStandContainer = root?.Q<VisualElement>("PrizeStandContainer");
            returnButton = root?.Q<Button>("PrizeResultReturnButton");

            if (qrContainer != null)
            {
                qrContainer.style.display = DisplayStyle.None;
            }
            if (prizeStandContainer != null)
            {
                prizeStandContainer.style.display = DisplayStyle.None;
            }

            if (returnButton != null)
            {
                returnButton.clicked += HandleReturnClicked;
                returnButton.RegisterCallback<FocusInEvent>(HandleButtonFocusIn);
                returnButton.RegisterCallback<FocusOutEvent>(HandleButtonFocusOut);
            }
        }

        public event Action ReturnRequested;

        public bool IsOpen { get; private set; }

        public void SetResult(
            PrizeFlowResult result,
            string bigPrizeName = null,
            string phoneNumber = null,
            PrizeQrTicket qrTicket = null,
            string qrError = null)
        {
            SetLabel(titleLabel, ResolveTitle(result, bigPrizeName));
            SetLabel(subtitleLabel, ResolveSubtitle(result, phoneNumber, qrTicket, qrError));
            SetLabel(detailsLabel, $"Очки: {result.Score}. Монеты: {result.Coins}. Высота: {result.Height:0} м.");
            RefreshQrBlock(result, qrTicket, qrError);

            if (prizeStandContainer != null)
            {
                prizeStandContainer.style.display = DisplayStyle.None;
                prizeStandContainer.Clear();
            }
        }

        public void SetSubtitle(string text)
        {
            SetLabel(subtitleLabel, text);
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

            if (returnButton != null)
            {
                returnButton.Focus();
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

        public void SetPrizeStandStatus(
            IReadOnlyDictionary<int, PrizeBoxStatus> statuses,
            int openingBox = -1,
            int openedBox = -1,
            int errorBox = -1)
        {
            if (qrContainer != null)
            {
                qrContainer.style.display = DisplayStyle.None;
            }
            if (prizeStandContainer == null)
            {
                return;
            }

            prizeStandContainer.style.display = DisplayStyle.Flex;
            prizeStandContainer.Clear();

            if (statuses == null || statuses.Count == 0)
            {
                var offlineLabel = new Label("Витрина оффлайн или нет доступных ячеек");
                offlineLabel.AddToClassList("overlay-details");
                offlineLabel.style.fontSize = 24;
                offlineLabel.style.color = new Color(0.9f, 0.3f, 0.3f, 1f);
                prizeStandContainer.Add(offlineLabel);
                return;
            }

            foreach (var pair in statuses)
            {
                int boxNumber = pair.Key;
                PrizeBoxStatus status = pair.Value;

                var cell = new VisualElement();
                cell.AddToClassList("prize-stand-cell");

                var label = new Label(boxNumber.ToString());
                label.AddToClassList("prize-stand-cell-label");
                cell.Add(label);

                if (boxNumber == openingBox)
                {
                    cell.AddToClassList("prize-stand-cell--opening");
                }
                else if (boxNumber == openedBox)
                {
                    cell.AddToClassList("prize-stand-cell--opened");
                }
                else if (boxNumber == errorBox)
                {
                    cell.AddToClassList("prize-stand-cell--error");
                }
                else
                {
                    switch (status)
                    {
                        case PrizeBoxStatus.Ok:
                        case PrizeBoxStatus.OkWithCard:
                            cell.AddToClassList("prize-stand-cell--ready");
                            break;
                        case PrizeBoxStatus.Empty:
                        case PrizeBoxStatus.Opened:
                            cell.AddToClassList("prize-stand-cell--empty");
                            break;
                        case PrizeBoxStatus.OpenError:
                        case PrizeBoxStatus.Error:
                            cell.AddToClassList("prize-stand-cell--error");
                            break;
                        default:
                            break;
                    }
                }

                prizeStandContainer.Add(cell);
            }
        }

        public void Dispose()
        {
            if (returnButton != null)
            {
                returnButton.clicked -= HandleReturnClicked;
                returnButton.UnregisterCallback<FocusInEvent>(HandleButtonFocusIn);
                returnButton.UnregisterCallback<FocusOutEvent>(HandleButtonFocusOut);
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

        private static string ResolveTitle(PrizeFlowResult result, string bigPrizeName)
        {
            return result.RewardKind switch
            {
                PrizeRewardKind.QRCode => "QR-приз!",
                PrizeRewardKind.Hopper => "Приз из хоппера!",
                PrizeRewardKind.PrizeStand => "Приз на витрине!",
                PrizeRewardKind.Big => string.IsNullOrWhiteSpace(bigPrizeName)
                    ? "Большой приз!"
                    : $"Большой приз: {bigPrizeName}",
                PrizeRewardKind.Small => "Малый приз!",
                _ => "Игра завершена",
            };
        }

        private static string ResolveSubtitle(
            PrizeFlowResult result,
            string phoneNumber,
            PrizeQrTicket qrTicket,
            string qrError)
        {
            if (!result.HasPrize)
            {
                return "Попробуйте ещё раз.";
            }

            if (result.RewardKind == PrizeRewardKind.QRCode)
            {
                if (qrTicket != null)
                {
                    return "Отсканируйте QR-код для связи в Telegram.";
                }

                return string.IsNullOrWhiteSpace(qrError)
                    ? "QR-приз готовится. Обратитесь к оператору."
                    : qrError;
            }

            return string.IsNullOrWhiteSpace(phoneNumber)
                ? "Обратитесь к оператору для выдачи приза."
                : $"Обратитесь к оператору: {phoneNumber}";
        }

        private void RefreshQrBlock(PrizeFlowResult result, PrizeQrTicket qrTicket, string qrError)
        {
            if (qrContainer == null)
            {
                return;
            }

            var shouldShow = result.RewardKind == PrizeRewardKind.QRCode;
            qrContainer.style.display = shouldShow ? DisplayStyle.Flex : DisplayStyle.None;
            if (!shouldShow)
            {
                if (qrImage != null)
                {
                    qrImage.image = null;
                }

                SetLabel(qrPhoneLabel, string.Empty);
                SetLabel(qrMessageLabel, string.Empty);
                return;
            }

            if (qrImage != null)
            {
                qrImage.image = qrTicket?.Texture;
                qrImage.style.display = qrTicket?.Texture != null ? DisplayStyle.Flex : DisplayStyle.None;
            }

            var phone = qrTicket != null
                ? FormatDisplayPhone(qrTicket)
                : "Телефон не задан";
            var message = qrTicket != null
                ? qrTicket.Message
                : qrError;

            SetLabel(qrPhoneLabel, phone);
            SetLabel(qrMessageLabel, message);
        }

        private static string FormatDisplayPhone(PrizeQrTicket qrTicket)
        {
            if (!string.IsNullOrWhiteSpace(qrTicket.OriginalPhone))
            {
                return qrTicket.OriginalPhone;
            }

            return string.IsNullOrWhiteSpace(qrTicket.PhoneDigits)
                ? "Телефон не задан"
                : $"+{qrTicket.PhoneDigits}";
        }

        private static void SetLabel(Label label, string value)
        {
            if (label != null)
            {
                label.text = value;
            }
        }

        private void HandleReturnClicked()
        {
            if (IsInputGuardActive())
            {
                return;
            }

            ReturnRequested?.Invoke();
        }

        private bool IsInputGuardActive()
        {
            return Time.unscaledTime < inputEnabledAtTime;
        }
    }
}
