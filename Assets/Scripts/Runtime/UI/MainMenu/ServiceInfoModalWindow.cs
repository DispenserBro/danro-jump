using DanroJump.Settings;
using DanroJump.UI.Modals;
using UnityEngine.UIElements;

namespace DanroJump.UI.MainMenu
{
    /// <summary>
    /// Универсальное служебное модальное окно для сервисных действий.
    /// Используется как первый слой для статистики, QR, призов и витрины.
    /// </summary>
    public sealed class ServiceInfoModalWindow : ModalWindowBase
    {
        private readonly Label titleLabel;
        private readonly Label contentLabel;
        private readonly Label focusLabel;
        private readonly Button closeButton;

        public ServiceInfoModalWindow(VisualElement root)
            : base(root)
        {
            titleLabel = root?.Q<Label>("ServiceInfoTitle");
            contentLabel = root?.Q<Label>("ServiceInfoContent");
            focusLabel = root?.Q<Label>("ServiceInfoFocus");
            closeButton = root?.Q<Button>("ServiceInfoCloseButton");

            if (closeButton != null)
            {
                closeButton.clicked += RequestClose;
            }
        }

        public void SetResult(ServiceSettingsActionResult result)
        {
            SetContent(result.Title, result.Status, result.FocusKey);
        }

        public void SetContent(string title, string content, string focus)
        {
            if (titleLabel != null)
            {
                titleLabel.text = string.IsNullOrWhiteSpace(title) ? "Служебное окно" : title;
            }

            if (contentLabel != null)
            {
                contentLabel.text = string.IsNullOrWhiteSpace(content) ? "Данные пока недоступны." : content;
            }

            if (focusLabel != null)
            {
                focusLabel.text = string.IsNullOrWhiteSpace(focus) ? string.Empty : $"Раздел: {focus}";
            }
        }

        public void Dispose()
        {
            if (closeButton != null)
            {
                closeButton.clicked -= RequestClose;
            }
        }

        protected override VisualElement GetDefaultFocusElement()
        {
            return closeButton;
        }
    }
}
