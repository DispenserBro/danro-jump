using DanroJump.UI.Focus;
using UnityEngine;
using UnityEngine.UIElements;

namespace DanroJump.UI.Modals
{
    /// <summary>
    /// Базовый класс UI Toolkit-модалки с управлением видимостью и локальным кольцом фокуса.
    /// </summary>
    public abstract class ModalWindowBase
    {
        private const string ModalButtonClass = "modal-button";
        private const string FocusedModalButtonClass = "modal-button--focused";

        private readonly UiFocusRing focusRing = new();

        /// <summary>
        /// Создает модальное окно поверх корневого VisualElement.
        /// </summary>
        protected ModalWindowBase(VisualElement root)
        {
            Root = root;
            Root?.RegisterCallback<FocusInEvent>(HandleModalButtonFocusIn, TrickleDown.TrickleDown);
            Root?.RegisterCallback<FocusOutEvent>(HandleModalButtonFocusOut, TrickleDown.TrickleDown);
        }

        /// <summary>
        /// Корневой VisualElement модального окна.
        /// </summary>
        public VisualElement Root { get; }

        /// <summary>
        /// Последний элемент внутри модалки, на котором был фокус.
        /// </summary>
        public VisualElement LastFocusedElement { get; private set; }

        /// <summary>
        /// Разрешает закрытие окна по Escape/Back.
        /// </summary>
        public virtual bool CanCloseByBack => true;

        /// <summary>
        /// Показывает, открыто ли модальное окно сейчас.
        /// </summary>
        public bool IsOpen { get; private set; }

        public event System.Action<ModalWindowBase> Opened;
        public event System.Action<ModalWindowBase> Closed;
        public event System.Action<ModalWindowBase> CloseRequested;

        /// <summary>
        /// Показывает модальное окно и пересобирает доступные элементы фокуса.
        /// </summary>
        public virtual void Open()
        {
            if (Root == null)
            {
                return;
            }

            Root.style.display = DisplayStyle.Flex;
            Root.SetEnabled(true);
            IsOpen = true;
            RebuildFocusRing();
            Opened?.Invoke(this);
        }

        /// <summary>
        /// Скрывает модальное окно и сбрасывает запомненный фокус.
        /// </summary>
        public virtual void Close()
        {
            if (Root == null)
            {
                return;
            }

            Root.style.display = DisplayStyle.None;
            Root.SetEnabled(false);
            IsOpen = false;
            LastFocusedElement = null;
            ClearFocusedModalButtons();
            Closed?.Invoke(this);
        }

        /// <summary>
        /// Просит внешний стек закрыть это окно с учетом порядка модалок.
        /// </summary>
        public void RequestClose()
        {
            CloseRequested?.Invoke(this);
        }

        /// <summary>
        /// Обновляет список элементов, между которыми можно перемещать фокус.
        /// </summary>
        public void RebuildFocusRing()
        {
            focusRing.Rebuild(Root);
        }

        /// <summary>
        /// Проверяет, принадлежит ли элемент визуальному дереву модалки.
        /// </summary>
        public bool Contains(VisualElement element)
        {
            return ContainsElement(Root, element);
        }

        /// <summary>
        /// Проверяет, известен ли элемент текущему кольцу фокуса.
        /// </summary>
        public bool IsKnownFocusable(VisualElement element)
        {
            return focusRing.Contains(element);
        }

        /// <summary>
        /// Запоминает последний сфокусированный элемент внутри модалки.
        /// </summary>
        public void RememberFocusedElement(VisualElement element)
        {
            if (Contains(element))
            {
                LastFocusedElement = element;
            }
        }

        /// <summary>
        /// Возвращает фокус на последний активный элемент или на элемент по умолчанию.
        /// </summary>
        public void FocusDefault()
        {
            RebuildFocusRing();
            (LastFocusedElement != null && focusRing.Contains(LastFocusedElement)
                ? LastFocusedElement
                : GetDefaultFocusElement() ?? focusRing.GetFirst())?.Focus();
        }

        /// <summary>
        /// Сдвигает фокус внутри окна на один элемент вперед или назад.
        /// </summary>
        public bool FocusRelativeTo(VisualElement current, int direction)
        {
            RebuildFocusRing();
            return focusRing.FocusRelativeTo(current, direction);
        }

        /// <summary>
        /// Позволяет конкретной модалке перехватить UI Toolkit navigation-событие.
        /// </summary>
        /// <param name="current">Элемент, с которого пришла навигация.</param>
        /// <param name="direction">Направление навигации.</param>
        /// <returns>True, если модалка обработала событие сама.</returns>
        public virtual bool HandleNavigationMove(VisualElement current, NavigationMoveEvent.Direction direction)
        {
            return false;
        }

        /// <summary>
        /// Позволяет конкретной модалке перехватить клавиатурную навигацию.
        /// </summary>
        /// <param name="current">Элемент, с которого пришла клавиша.</param>
        /// <param name="keyCode">Код нажатой клавиши.</param>
        /// <returns>True, если модалка обработала клавишу сама.</returns>
        public virtual bool HandleNavigationKey(VisualElement current, KeyCode keyCode)
        {
            return false;
        }

        /// <summary>
        /// Позволяет конкретной модалке выбрать стартовый элемент фокуса.
        /// </summary>
        protected virtual VisualElement GetDefaultFocusElement()
        {
            return null;
        }

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

        private void HandleModalButtonFocusIn(FocusInEvent evt)
        {
            ResolveModalButton(evt.target as VisualElement)?.AddToClassList(FocusedModalButtonClass);
        }

        private void HandleModalButtonFocusOut(FocusOutEvent evt)
        {
            ResolveModalButton(evt.target as VisualElement)?.RemoveFromClassList(FocusedModalButtonClass);
        }

        private void ClearFocusedModalButtons()
        {
            Root?.Query<Button>(className: FocusedModalButtonClass).ForEach(
                button => button.RemoveFromClassList(FocusedModalButtonClass));
        }

        private static Button ResolveModalButton(VisualElement element)
        {
            for (var current = element; current != null; current = current.parent)
            {
                if (current is Button button && button.ClassListContains(ModalButtonClass))
                {
                    return button;
                }
            }

            return null;
        }
    }
}
