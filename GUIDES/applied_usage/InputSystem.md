# Полное руководство по новому Input System

В Unity старая система `Input.GetKey()` заменена на современную, модульную `Input System` (`UnityEngine.InputSystem`). Она решает проблемы поддержки множества геймпадов, кроссплатформенности и переназначения клавиш.

## 1. Быстрый способ (Прямое чтение, как в старой системе)

Хотя это не самый гибкий способ, он удобен для быстрого прототипирования. Мы обращаемся к текущему активному устройству (`Keyboard.current`, `Gamepad.current`).

```csharp
using UnityEngine.InputSystem;

public void Update()
{
    // Важно проверять на null, т.к. клавиатуры может не быть (на мобилках, консолях)
    if (Keyboard.current != null)
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Jump();
        }
    }

    if (Gamepad.current != null)
    {
        // Чтение стика (возвращает Vector2 от -1 до 1)
        Vector2 moveInput = Gamepad.current.leftStick.ReadValue();
    }
}
```

## 2. Правильный способ (Input Actions Asset)

Рекомендуемый подход — создать файл `.inputactions` через редактор (Create -> Input Actions). В нем вы задаете "Действия" (Actions), например "Move" или "Jump", и привязываете к ним кнопки с разных устройств (Пробел на ПК, Кнопка А на Xbox, Экранный стик на телефоне).

### Вариант 2А: Генерация C# класса (Рекомендуется для Zenject)

1. В инспекторе файла `.inputactions` поставьте галочку `Generate C# Class`.
2. Создайте класс (например, `PlayerControls`).

```csharp
public class PlayerInputHandler : MonoBehaviour, IDisposable
{
    private PlayerControls controls;

    private void Awake()
    {
        // Инициализируем сгенерированный класс
        controls = new PlayerControls();

        // ПОДПИСЫВАЕМСЯ НА СОБЫТИЯ
        // performed - кнопка нажата (и прошла порог чувствительности)
        controls.Gameplay.Jump.performed += OnJumpPerformed;
        // canceled - кнопка отпущена
        controls.Gameplay.Jump.canceled += OnJumpCanceled;
    }

    private void OnEnable()
    {
        // Важно: экшены по умолчанию выключены!
        controls.Gameplay.Enable(); 
    }

    private void OnDisable()
    {
        controls.Gameplay.Disable();
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        Debug.Log("Прыжок!");
    }

    private void Update()
    {
        // Чтение значений (например, вектора движения) в Update
        Vector2 movement = controls.Gameplay.Move.ReadValue<Vector2>();
        MoveCharacter(movement);
    }

    public void Dispose()
    {
        controls?.Dispose();
    }
}
```

## 3. Обработка виртуального / аппаратного ввода (COM-порты)

В проекте "Danro Jump" есть `ComControllerInputBridge`, который принимает сигналы от реального "железного" автомата через COM-порт.

Как обмануть Unity и заставить её думать, что нажата физическая кнопка геймпада? 
В Input System можно программно создавать виртуальные устройства и посылать в них события.

```csharp
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class VirtualGamepadEmulator
{
    private Gamepad virtualGamepad;

    public void Initialize()
    {
        // Создаем виртуальный геймпад в системе
        virtualGamepad = InputSystem.AddDevice<Gamepad>("Virtual Gamepad");
    }

    // Этот метод вызывается, когда по COM-порту приходит сигнал
    public void TriggerVirtualJump()
    {
        if (virtualGamepad == null) return;

        // Генерируем событие "Кнопка А (южная кнопка) нажата"
        InputSystem.QueueStateEvent(virtualGamepad, new GamepadState
        {
            buttons = (uint)GamepadButton.South
        });
    }

    public void Cleanup()
    {
        if (virtualGamepad != null)
        {
            InputSystem.RemoveDevice(virtualGamepad);
        }
    }
}
```

> [!IMPORTANT]
> Если вы генерируете события ввода из фонового потока (от COM-порта), функция `InputSystem.QueueStateEvent` является потокобезопасной, но вызовы любых других Unity API (например, перемещение объекта) должны быть переданы в главный поток!
