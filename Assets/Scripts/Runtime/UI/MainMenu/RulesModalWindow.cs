using DanroJump.UI.Modals;
using UnityEngine.UIElements;

namespace DanroJump.UI.MainMenu
{
    /// <summary>
    /// Модальное окно правил главного меню.
    /// </summary>
    public sealed class RulesModalWindow : ModalWindowBase
    {
        private readonly Button closeButton;

        /// <summary>
        /// Подключает кнопку закрытия к общей modal-stack логике.
        /// </summary>
        public RulesModalWindow(VisualElement root)
            : base(root)
        {
            closeButton = root?.Q<Button>("RulesModalCloseButton");

            if (closeButton != null)
            {
                closeButton.clicked += RequestClose;
            }
        }

        /// <summary>
        /// По умолчанию ставит фокус на кнопку закрытия правил.
        /// </summary>
        protected override VisualElement GetDefaultFocusElement()
        {
            return closeButton;
        }

        /// <summary>
        /// Отписывает обработчики, когда контроллер главного меню уничтожается.
        /// </summary>
        public void Dispose()
        {
            if (closeButton != null)
            {
                closeButton.clicked -= RequestClose;
            }
        }
    }
}
