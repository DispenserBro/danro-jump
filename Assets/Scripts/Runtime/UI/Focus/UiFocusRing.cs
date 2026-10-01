using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DanroJump.UI.Focus
{
    /// <summary>
    /// Собирает линейное кольцо фокусируемых UI Toolkit-элементов для навигации с клавиатуры или автомата.
    /// </summary>
    public sealed class UiFocusRing
    {
        private readonly List<VisualElement> elements = new();

        /// <summary>
        /// Текущий список фокусируемых элементов.
        /// </summary>
        public IReadOnlyList<VisualElement> Elements => elements;

        /// <summary>
        /// Количество элементов в кольце фокуса.
        /// </summary>
        public int Count => elements.Count;

        /// <summary>
        /// Пересобирает список видимых и активных элементов, доступных для фокуса.
        /// </summary>
        public void Rebuild(VisualElement root)
        {
            elements.Clear();

            if (root == null)
            {
                return;
            }

            CollectFocusableElements(root);
        }

        /// <summary>
        /// Проверяет, входит ли элемент в текущее кольцо фокуса.
        /// </summary>
        public bool Contains(VisualElement element)
        {
            return elements.Contains(element);
        }

        /// <summary>
        /// Возвращает первый элемент кольца фокуса.
        /// </summary>
        public VisualElement GetFirst()
        {
            return elements.Count > 0 ? elements[0] : null;
        }

        /// <summary>
        /// Возвращает следующий элемент с зацикливанием по краям списка.
        /// </summary>
        public VisualElement GetNext(VisualElement current, int direction)
        {
            if (elements.Count == 0)
            {
                return null;
            }

            var currentIndex = current != null ? elements.IndexOf(current) : -1;
            if (direction == 0)
            {
                return currentIndex >= 0 ? elements[currentIndex] : elements[0];
            }

            if (currentIndex < 0)
            {
                return direction > 0 ? elements[0] : elements[^1];
            }

            var targetIndex = (currentIndex + direction + elements.Count) % elements.Count;
            return elements[targetIndex];
        }

        /// <summary>
        /// Ставит фокус на первый доступный элемент.
        /// </summary>
        public void FocusFirst()
        {
            TryFocus(GetFirst());
        }

        /// <summary>
        /// Перемещает фокус относительно текущего элемента.
        /// </summary>
        public bool FocusRelativeTo(VisualElement current, int direction)
        {
            var next = GetNext(current, direction);
            if (next == null)
            {
                return false;
            }

            return TryFocus(next);
        }

        /// <summary>
        /// Ставит фокус на элемент, если он все еще доступен в текущем кольце.
        /// </summary>
        public bool TryFocus(VisualElement element)
        {
            if (element == null || !elements.Contains(element) || !IsFocusable(element))
            {
                return false;
            }

            element.Focus();
            return true;
        }

        /// <summary>
        /// Обходит визуальное дерево в порядке отображения и собирает доступные элементы.
        /// </summary>
        private void CollectFocusableElements(VisualElement root)
        {
            if (!CanTraverse(root))
            {
                return;
            }

            if (IsFocusable(root))
            {
                elements.Add(root);
            }

            for (var index = 0; index < root.childCount; index++)
            {
                CollectFocusableElements(root[index]);
            }
        }

        /// <summary>
        /// Проверяет базовые условия доступности элемента для фокуса.
        /// </summary>
        private static bool IsFocusable(VisualElement element)
        {
            return element.focusable &&
                CanTraverse(element);
        }

        /// <summary>
        /// Проверяет, нужно ли учитывать элемент и его дочернее дерево при сборке фокуса.
        /// </summary>
        private static bool CanTraverse(VisualElement element)
        {
            return element != null &&
                element.enabledInHierarchy &&
                element.resolvedStyle.display != DisplayStyle.None &&
                element.resolvedStyle.visibility != Visibility.Hidden;
        }
    }
}
