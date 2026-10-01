using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DanroJump.UI.Modals
{
    /// <summary>
    /// Управляет стеком UI Toolkit-модалок и удерживает фокус внутри последнего открытого окна.
    /// </summary>
    public sealed class ModalStackController
    {
        private readonly VisualElement layer;
        private readonly VisualElement backdrop;
        private readonly List<ModalWindowBase> stack = new();
        private VisualElement focusBeforeFirstModal;
        private VisualElement eventRoot;
        private bool isRegistered;

        /// <summary>
        /// Создает контроллер для слоя модальных окон и фоновой затемняющей подложки.
        /// </summary>
        public ModalStackController(VisualElement layer, VisualElement backdrop)
        {
            this.layer = layer;
            this.backdrop = backdrop;

            StretchModalSurface();
            HideLayer();
        }

        /// <summary>
        /// Показывает, есть ли открытые модальные окна.
        /// </summary>
        public bool HasOpenModals => stack.Count > 0;

        /// <summary>
        /// Последняя открытая модалка, которая сейчас удерживает фокус.
        /// </summary>
        public ModalWindowBase TopModal => stack.Count > 0 ? stack[^1] : null;

        /// <summary>
        /// Открывает модалку поверх текущего стека и переводит в нее фокус.
        /// </summary>
        public void Open(ModalWindowBase modal)
        {
            if (modal == null || modal.Root == null)
            {
                return;
            }

            if (stack.Contains(modal))
            {
                Close(modal);
            }

            if (stack.Count == 0)
            {
                focusBeforeFirstModal = GetFocusedVisualElement();
                ShowLayer();
                RegisterFocusGuards();
            }

            stack.Add(modal);
            modal.CloseRequested += Close;
            ApplyFullscreen(modal.Root);
            ApplyResolvedSize(modal.Root, layer);
            modal.Open();
            modal.Root.BringToFront();
            modal.FocusDefault();
        }

        /// <summary>
        /// Закрывает указанную модалку и все окна, которые были открыты поверх нее.
        /// </summary>
        public void Close(ModalWindowBase modal)
        {
            if (modal == null)
            {
                return;
            }

            var index = stack.LastIndexOf(modal);
            if (index < 0)
            {
                return;
            }

            for (var i = stack.Count - 1; i >= index; i--)
            {
                CloseAt(i);
            }

            if (stack.Count > 0)
            {
                TopModal.FocusDefault();
                return;
            }

            UnregisterFocusGuards();
            HideLayer();
            focusBeforeFirstModal?.Focus();
            focusBeforeFirstModal = null;
        }

        /// <summary>
        /// Закрывает верхнюю модалку в стеке.
        /// </summary>
        public void CloseTop()
        {
            Close(TopModal);
        }

        /// <summary>
        /// Закрывает все модалки и снимает глобальные обработчики фокуса.
        /// </summary>
        public void Dispose()
        {
            while (stack.Count > 0)
            {
                CloseAt(stack.Count - 1);
            }

            UnregisterFocusGuards();
            HideLayer();
            focusBeforeFirstModal = null;
        }

        /// <summary>
        /// Закрывает модалку по индексу стека без восстановления фокуса наружу.
        /// </summary>
        private void CloseAt(int index)
        {
            var modal = stack[index];
            modal.CloseRequested -= Close;
            modal.Close();
            stack.RemoveAt(index);
        }

        /// <summary>
        /// Показывает общий слой модалок и backdrop.
        /// </summary>
        private void ShowLayer()
        {
            StretchModalSurface();

            if (layer != null)
            {
                layer.style.display = DisplayStyle.Flex;
                layer.SetEnabled(true);
                layer.BringToFront();
            }

            if (backdrop != null)
            {
                backdrop.style.display = DisplayStyle.Flex;
                backdrop.SetEnabled(true);
            }
        }

        /// <summary>
        /// Растягивает слой и host-контейнер UXML-шаблона на весь UIDocument.
        /// </summary>
        private void StretchModalSurface()
        {
            var host = layer?.parent;
            ApplyFullscreen(host);
            if (host != null)
            {
                host.pickingMode = PickingMode.Ignore;
            }

            ApplyFullscreen(layer);
            ApplyFullscreen(backdrop);
            ApplyResolvedSize(layer, host);
            ApplyResolvedSize(backdrop, host);
        }

        private static void ApplyFullscreen(VisualElement element)
        {
            if (element == null)
            {
                return;
            }

            element.style.position = Position.Absolute;
            element.style.left = 0;
            element.style.right = 0;
            element.style.top = 0;
            element.style.bottom = 0;
            element.style.width = Length.Percent(100);
            element.style.height = Length.Percent(100);
            element.style.minHeight = Length.Percent(100);
        }

        private static void ApplyResolvedSize(VisualElement element, VisualElement reference)
        {
            if (element == null || reference == null)
            {
                return;
            }

            var width = reference.resolvedStyle.width;
            var height = reference.resolvedStyle.height;
            if (width <= 0f || height <= 0f)
            {
                return;
            }

            element.style.width = width;
            element.style.height = height;
            element.style.minHeight = height;
        }

        /// <summary>
        /// Скрывает общий слой модалок и backdrop.
        /// </summary>
        private void HideLayer()
        {
            if (backdrop != null)
            {
                backdrop.style.display = DisplayStyle.None;
                backdrop.SetEnabled(false);
            }

            if (layer != null)
            {
                layer.style.display = DisplayStyle.None;
                layer.SetEnabled(false);
            }
        }

        /// <summary>
        /// Подключает обработчики к корню панели, чтобы фокус нельзя было увести за верхнюю модалку.
        /// </summary>
        private void RegisterFocusGuards()
        {
            if (layer == null || isRegistered)
            {
                return;
            }

            eventRoot = layer.panel?.visualTree ?? layer;
            eventRoot.RegisterCallback<FocusInEvent>(HandleFocusIn, TrickleDown.TrickleDown);
            eventRoot.RegisterCallback<FocusOutEvent>(HandleFocusOut, TrickleDown.TrickleDown);
            eventRoot.RegisterCallback<NavigationMoveEvent>(HandleNavigationMove, TrickleDown.TrickleDown);
            eventRoot.RegisterCallback<KeyDownEvent>(HandleKeyDown, TrickleDown.TrickleDown);
            isRegistered = true;
        }

        /// <summary>
        /// Снимает обработчики, удерживающие фокус внутри модального стека.
        /// </summary>
        private void UnregisterFocusGuards()
        {
            if (eventRoot == null || !isRegistered)
            {
                return;
            }

            eventRoot.UnregisterCallback<FocusInEvent>(HandleFocusIn, TrickleDown.TrickleDown);
            eventRoot.UnregisterCallback<FocusOutEvent>(HandleFocusOut, TrickleDown.TrickleDown);
            eventRoot.UnregisterCallback<NavigationMoveEvent>(HandleNavigationMove, TrickleDown.TrickleDown);
            eventRoot.UnregisterCallback<KeyDownEvent>(HandleKeyDown, TrickleDown.TrickleDown);
            eventRoot = null;
            isRegistered = false;
        }

        /// <summary>
        /// Возвращает фокус назад в верхнюю модалку, если он попал во внешний UI.
        /// </summary>
        private void HandleFocusIn(FocusInEvent evt)
        {
            var top = TopModal;
            if (top == null || evt.target is not VisualElement element)
            {
                return;
            }

            if (!top.Contains(element))
            {
                top.FocusDefault();
                IgnoreFocusControllerEvent(evt);
                evt.StopImmediatePropagation();
                return;
            }

            top.RememberFocusedElement(element);
        }

        /// <summary>
        /// Планирует проверку фокуса после того, как UI Toolkit завершит FocusOut.
        /// </summary>
        private void HandleFocusOut(FocusOutEvent evt)
        {
            if (TopModal == null)
            {
                return;
            }

            layer?.schedule.Execute(KeepFocusInsideTopModal);
        }

        /// <summary>
        /// Преобразует UI Toolkit navigation-событие в линейное движение по элементам модалки.
        /// </summary>
        private void HandleNavigationMove(NavigationMoveEvent evt)
        {
            var top = TopModal;
            if (top == null || evt.target is not VisualElement current)
            {
                return;
            }

            if (IsInsideTextField(current))
            {
                return;
            }

            if (top.HandleNavigationMove(current, evt.direction))
            {
                IgnoreFocusControllerEvent(evt);
                evt.StopImmediatePropagation();
                return;
            }

            var direction = GetNavigationOffset(evt.direction);
            if (direction == 0)
            {
                return;
            }

            top.FocusRelativeTo(current, direction);
            IgnoreFocusControllerEvent(evt);
            evt.StopImmediatePropagation();
        }

        /// <summary>
        /// Обрабатывает клавиатурную навигацию и закрытие верхнего окна по back-кнопкам.
        /// </summary>
        private void HandleKeyDown(KeyDownEvent evt)
        {
            var top = TopModal;
            if (top == null)
            {
                return;
            }

            var current = evt.target as VisualElement;
            if (IsInsideTextField(current) && IsTextFieldKey(evt.keyCode))
            {
                return;
            }

            if (IsBackKey(evt.keyCode))
            {
                if (top.CanCloseByBack)
                {
                    CloseTop();
                }

                IgnoreFocusControllerEvent(evt);
                evt.StopImmediatePropagation();
                return;
            }

            if (top.HandleNavigationKey(current, evt.keyCode))
            {
                IgnoreFocusControllerEvent(evt);
                evt.StopImmediatePropagation();
                return;
            }

            var direction = GetKeyNavigationOffset(evt);
            if (direction == 0)
            {
                return;
            }

            top.FocusRelativeTo(current, direction);
            IgnoreFocusControllerEvent(evt);
            evt.StopImmediatePropagation();
        }

        /// <summary>
        /// Отложенно восстанавливает фокус после FocusOut, когда UI Toolkit уже обновил focusController.
        /// </summary>
        private void KeepFocusInsideTopModal()
        {
            var top = TopModal;
            if (top == null)
            {
                return;
            }

            var focused = GetFocusedVisualElement();
            if (focused == null || !top.Contains(focused))
            {
                top.FocusDefault();
            }
        }

        /// <summary>
        /// Возвращает текущий сфокусированный элемент панели.
        /// </summary>
        private VisualElement GetFocusedVisualElement()
        {
            return layer?.panel?.focusController?.focusedElement as VisualElement;
        }

        /// <summary>
        /// Переводит направление NavigationMoveEvent в сдвиг по линейному списку фокуса.
        /// </summary>
        private static int GetNavigationOffset(NavigationMoveEvent.Direction direction)
        {
            return direction switch
            {
                NavigationMoveEvent.Direction.Up => -1,
                NavigationMoveEvent.Direction.Down => 1,
                _ => 0,
            };
        }

        /// <summary>
        /// Переводит клавиши навигации и WASD в сдвиг по линейному списку фокуса.
        /// </summary>
        private static int GetKeyNavigationOffset(KeyDownEvent evt)
        {
            return evt.keyCode switch
            {
                KeyCode.Tab => evt.shiftKey ? -1 : 1,
                KeyCode.UpArrow => -1,
                KeyCode.W => -1,
                KeyCode.DownArrow => 1,
                KeyCode.S => 1,
                _ => 0,
            };
        }

        /// <summary>
        /// Проверяет клавиши, которые должны закрывать верхнюю модалку.
        /// </summary>
        private static bool IsBackKey(KeyCode keyCode)
        {
            return keyCode == KeyCode.Escape ||
                keyCode == KeyCode.Backspace ||
                keyCode == KeyCode.JoystickButton1;
        }

        /// <summary>
        /// Проверяет, находится ли событие внутри TextField или его внутреннего текстового элемента.
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
        /// Клавиши, которые TextField должен получать без модальной навигации и закрытия.
        /// </summary>
        private static bool IsTextFieldKey(KeyCode keyCode)
        {
            return keyCode == KeyCode.Backspace ||
                keyCode == KeyCode.Space ||
                keyCode == KeyCode.Return ||
                keyCode == KeyCode.KeypadEnter ||
                keyCode == KeyCode.UpArrow ||
                keyCode == KeyCode.DownArrow ||
                keyCode == KeyCode.LeftArrow ||
                keyCode == KeyCode.RightArrow ||
                keyCode == KeyCode.W ||
                keyCode == KeyCode.A ||
                keyCode == KeyCode.S ||
                keyCode == KeyCode.D;
        }

        /// <summary>
        /// Запрещает встроенной UI Toolkit-навигации повторно двигать фокус после кастомной обработки.
        /// </summary>
        private static void IgnoreFocusControllerEvent(EventBase evt)
        {
            (evt?.target as VisualElement)?.panel?.focusController?.IgnoreEvent(evt);
        }
    }
}
