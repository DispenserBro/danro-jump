# Полное руководство по UI Toolkit

UI Toolkit (`UnityEngine.UIElements`) — это современная замена uGUI в Unity, основанная на веб-технологиях (HTML/CSS-подобные UXML и USS). В Danro Jump она используется для всего пользовательского интерфейса (Главное меню, HUD, Модальные окна).

## 1. Поиск элементов и обработка событий

Поиск элементов (Querying) работает аналогично `document.querySelector` в JavaScript.

```csharp
using UnityEngine.UIElements;

public class MyMenuController : MonoBehaviour
{
    private UIDocument document;

    private void OnEnable()
    {
        document = GetComponent<UIDocument>();
        var root = document.rootVisualElement;

        // Поиск кнопки по имени
        Button startBtn = root.Q<Button>("StartButton");
        // Поиск элемента по классу
        VisualElement container = root.Q<VisualElement>(className: "main-container");

        // Подписка на клик
        startBtn.clicked += OnStartClicked;
        
        // Подписка на другие события (например, наведение мыши)
        startBtn.RegisterCallback<MouseEnterEvent>(evt => Debug.Log("Hover!"));
    }

    private void OnStartClicked()
    {
        Debug.Log("Game Started!");
    }
}
```

> [!WARNING]
> Обязательно отписывайтесь от событий (`-=`) или используйте `UnregisterCallback` в методе `OnDisable()`, чтобы избежать утечек памяти (Memory Leaks). Утечки событий в UI Toolkit — одна из самых частых причин падения производительности!

## 2. Анимации через USS (Transitions)

Вместо использования сложных `Animator` или C# корутин для анимации кнопок и меню, в UI Toolkit используются **Transitions** (Плавные переходы) прямо в стилях.

**USS Файл (`styles.uss`):**
```css
.my-button {
    background-color: rgb(100, 100, 100);
    width: 200px;
    /* Указываем, что любые изменения цвета и размера должны длиться 0.3 секунды */
    transition-duration: 0.3s;
    transition-property: background-color, scale;
}

.my-button:hover {
    background-color: rgb(200, 50, 50);
    scale: 1.1 1.1; /* Увеличиваем кнопку на 10% */
}
```

Никакого C# кода! Кнопка плавно увеличится и поменяет цвет при наведении, а когда мышь уберут — плавно вернется в исходное состояние.

## 3. Манипуляция стилями из C# (Classes vs Inline Styles)

### Подход 1: Переключение классов (Предпочтительный)
Лучший способ изменить внешний вид — добавить или удалить USS-класс. Это позволяет дизайнеру менять цвета в USS, не трогая код программиста.

```csharp
// В USS: .hidden { display: none; opacity: 0; }
myElement.AddToClassList("hidden"); // Спрятать элемент
myElement.RemoveFromClassList("hidden"); // Показать элемент
myElement.ToggleInClassList("hidden"); // Переключить (если есть - удалить, если нет - добавить)
```

### Подход 2: Inline Styles (Для динамических данных)
Используется, когда значение нельзя предсказать заранее (например, прогресс-бар от 0 до 100%).

```csharp
// Динамически задаем ширину
progressBar.style.width = new Length(50, LengthUnit.Percent);

// Задаем цвет из переменной Unity (Color)
healthBar.style.backgroundColor = new StyleColor(Color.green);
```

## 4. Кастомные Контролы (Custom Controls)

Часто один и тот же кусок интерфейса (например, карточка инвентаря) повторяется. Вместо того чтобы дублировать код, создайте свой собственный `VisualElement`.

```csharp
// 1. Создаем класс-наследник
public class InventorySlot : VisualElement
{
    // 2. Фабрика для того, чтобы этот элемент появился в UI Builder
    public new class UxmlFactory : UxmlFactory<InventorySlot, UxmlTraits> { }

    private Label titleLabel;
    private VisualElement iconElement;

    // 3. Инициализация (создание верстки из кода или загрузка UXML)
    public InventorySlot()
    {
        // Создаем элементы кодом
        titleLabel = new Label("Empty Slot");
        iconElement = new VisualElement();
        
        // Добавляем классы для стилизации
        this.AddToClassList("inventory-slot");
        iconElement.AddToClassList("inventory-icon");

        // Строим дерево
        hierarchy.Add(iconElement);
        hierarchy.Add(titleLabel);
    }

    // 4. API для программиста
    public void SetItem(string name, Sprite icon)
    {
        titleLabel.text = name;
        iconElement.style.backgroundImage = new StyleBackground(icon);
    }
}
```
После компиляции элемент `InventorySlot` появится в Library в окне UI Builder, и вы сможете перетаскивать его мышкой как обычную кнопку!
