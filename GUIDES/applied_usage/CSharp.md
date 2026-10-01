# Продвинутые конструкции C# и архитектурные решения

Этот документ описывает принципы написания чистого и производительного C# кода, применяемые (или рекомендуемые к применению) в проекте `Danro Jump`.

## 1. Паттерн "Стек" для UI (Управление состояниями)

Для управления всплывающими окнами (MainMenu -> Settings -> QR Code Info) используется структура данных Стек (`Stack<T>`). Она работает по принципу LIFO (Last In, First Out).

```csharp
using System.Collections.Generic;

public class ModalStackController
{
    private readonly Stack<ModalWindowBase> modals = new Stack<ModalWindowBase>();

    public void Open(ModalWindowBase newModal)
    {
        // "Замораживаем" текущее верхнее окно (например, убираем у него фокус или делаем полупрозрачным)
        if (modals.TryPeek(out var currentTop))
        {
            currentTop.Suspend(); 
        }

        modals.Push(newModal);
        newModal.Activate();
    }

    public void CloseTop()
    {
        if (modals.Count == 0) return;

        // Закрываем и удаляем верхнее
        var top = modals.Pop();
        top.Deactivate();

        // "Размораживаем" то, которое оказалось под ним
        if (modals.TryPeek(out var newTop))
        {
            newTop.Resume();
        }
    }
}
```

## 2. Разделение через Интерфейсы (SOLID: Dependency Inversion)

Главное правило чистой архитектуры: **Высокоуровневые модули не должны зависеть от низкоуровневых. Оба должны зависеть от абстракций (интерфейсов).**

Плохо: `GameManager` знает о классе `ComPortInputBridge`.
Хорошо: `GameManager` знает об интерфейсе `IInputProvider`.

```csharp
// 1. Создаем абстракцию
public interface IInputProvider
{
    bool IsJumpPressed();
}

// 2. Реализуем для реального железа (COM)
public class ComInputProvider : IInputProvider { /* ... */ }

// 3. Реализуем для тестирования в редакторе (Keyboard)
public class KeyboardInputProvider : IInputProvider { /* ... */ }

// 4. Игроку всё равно, откуда пришел ввод!
public class PlayerController
{
    private readonly IInputProvider input;
    
    // Зависимость подставляется через Zenject
    public PlayerController(IInputProvider input) => this.input = input;

    public void Update()
    {
        if (input.IsJumpPressed()) Jump();
    }
}
```

## 3. Производительность (Избегание аллокаций)

Unity использует сборщик мусора (Garbage Collector). Каждая аллокация (создание объекта через `new`, выделение памяти под строки или массивы) рано или поздно приведет к скачку (GC Spike) и фризу (подвисанию игры).

### Избегайте LINQ в горячих путях (`Update`)
Методы вроде `.Where()`, `.ToList()`, `.Select()` создают объекты "под капотом". 

**ПЛОХО (в методе Update):**
```csharp
// Аллокация памяти каждый кадр!
var activeEnemies = allEnemies.Where(e => e.IsActive).ToList(); 
foreach(var e in activeEnemies) { /* ... */ }
```

**ХОРОШО:**
```csharp
// Ноль аллокаций
for (int i = 0; i < allEnemies.Count; i++)
{
    if (allEnemies[i].IsActive)
    {
        // ...
    }
}
```

### Структуры вместо Классов для данных (Structs vs Classes)
Если вам нужен просто контейнер для данных (например, координаты и цвет), используйте `struct` (Value Type) вместо `class` (Reference Type). Структуры выделяются в стеке (Stack) и не напрягают сборщик мусора.

```csharp
// Хороший кандидат на struct (весит 16 байт: float + float + float + int)
public struct SpawnPointData
{
    public float X;
    public float Y;
    public float Z;
    public int TypeId;
}
```

## 4. Модификатор `sealed`

Вы могли заметить, что многие классы в проекте объявлены как `sealed class`. 

```csharp
public sealed class MainMenuController : MonoBehaviour
```

Это делает класс недоступным для наследования. 
**Зачем:**
1. **Дизайн**: Явно говорит программисту "это финальная логика, не пытайся переопределять её методы, создай новый компонент".
2. **Производительность (Девиртуализация)**: В компиляторе IL2CPP (используемом Unity для билдов) компилятор C++ может заменить вызовы виртуальных методов (Virtual Dispatch) на прямые вызовы, если класс `sealed`. Это дает бесплатный (пусть и крошечный) прирост производительности.
