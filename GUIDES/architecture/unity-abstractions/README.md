# Собственные надстройки и абстракции над Unity-классами

## Зачем это нужно

Unity даёт много удобных API, но если использовать их напрямую везде, проект быстро получает:

- жёсткую привязку к `MonoBehaviour`
- сложный unit testing
- размытые границы между логикой и инфраструктурой
- трудности с заменой реализации

Поэтому для прикладного проекта полезно строить собственные надстройки над Unity API:

- обёртки
- фасады
- адаптеры
- базовые интерфейсы
- прикладные сервисы

Для `Danro Jump` это особенно важно из-за сочетания:

- gameplay-систем
- UI
- сцен
- `COM`-оборудования
- призовой логики
- сервисного режима

## Legacy как источник абстракций

`Assets/Temp/Legacy` содержит много старых `MonoBehaviour`, managers, UI-контроллеров и editor extensions. Их стоит рассматривать как материал для выделения новых абстракций, а не как готовый код для прямого переноса.

Особенно полезные источники:

- `Assets/Temp/Legacy/Scripts/Controls/Inputs/ComSystem` — аппаратный протокол, handshake, роли устройств, витрина
- `Assets/Temp/Legacy/Scripts/Managers/PaymentManager.cs` — кредиты, free play, старт игры
- `Assets/Temp/Legacy/Scripts/Managers/PrizeSystemManager.cs` — prize orchestration
- `Assets/Temp/Legacy/Scripts/Settings` — сервисные настройки
- `Assets/Temp/Legacy/Scripts/UI/Modal` — модальные окна призов и настроек
- `Assets/Temp/Legacy/Scripts/Editor` — editor tooling для настроек и иерархии
- `Assets/Temp/Legacy/Scripts/Utilities` — логирование, `QR`, телефонные форматы

Перед переносом старого класса нужно ответить на вопрос: какую стабильную обязанность он выполняет в новом проекте?

## Что считать надстройкой

Под надстройкой над Unity-классами в этом гайде понимаются не только editor extensions, но и прикладные архитектурные слои над Unity API.

Чаще всего это:

- интерфейс над Unity API
- сервис над статическим классом
- базовый runtime-компонент
- конфигурационный слой на `ScriptableObject`
- editor tooling для удобства настройки

## Когда надстройка действительно нужна

Надстройка оправдана, если:

- Unity API мешает тестировать код
- доступ к системе идёт из слишком многих мест
- нужно заменить поведение в будущем
- есть разница между доменной логикой и конкретной Unity-реализацией

Надстройка не нужна, если:

- это одноразовый локальный вызов
- абстракция ничего не упрощает
- она только дублирует Unity API без пользы

## Хорошие кандидаты на абстракции в Unity

### SceneManager

Вместо прямого доступа:

```csharp
SceneManager.LoadSceneAsync("Gameplay");
```

лучше:

```csharp
public interface ISceneFlowService
{
    UniTask OpenGameplayAsync();
}
```

### Time

Вместо прямого `Time.deltaTime` везде можно иметь:

```csharp
public interface ITimeProvider
{
    float DeltaTime { get; }
    float UnscaledDeltaTime { get; }
}
```

Это упрощает тестирование и управление скоростью.

### Random

Вместо прямого `UnityEngine.Random`:

```csharp
public interface IRandomService
{
    int Range(int minInclusive, int maxExclusive);
    float Value();
}
```

### PlayerPrefs

Для project-scale логики лучше не разбрасывать вызовы `PlayerPrefs` напрямую.

Лучше:

```csharp
public interface ILocalSettingsStorage
{
    void SetInt(string key, int value);
    int GetInt(string key, int defaultValue = 0);
}
```

### COM-порт и внешние устройства

Это один из самых сильных кандидатов на абстракцию в `Danro Jump`.

Не стоит смешивать:

- порт
- протокол
- игровую логику
- сервисную диагностику

Лучше разделять:

- `IComPortGateway`
- `IDeviceProtocol`
- `IPrizeShowcaseGateway`
- `IHardwareDiagnosticsService`

Legacy reference:

- `ComNativeWrapper` показывает границу с native `ComNative`
- `ComHub` и `ComDeviceClient` показывают модель нескольких устройств
- `PrizeStandController` показывает прикладной протокол витрины
- `ComInputToInputSystemAdapter` показывает, как аппаратный ввод можно отдавать в Unity Input System

## Базовые Unity-типы, над которыми чаще всего строят свои слои

### MonoBehaviour

`MonoBehaviour` хорош как Unity lifecycle entry point, но плох как контейнер всей бизнес-логики.

Практика:

- `MonoBehaviour` лучше использовать как view, adapter или composition point
- доменную и прикладную логику лучше выносить в обычные C# классы

Хорошая схема:

```csharp
public sealed class PrizeScreenView : MonoBehaviour
{
    [SerializeField] private Button _claimButton;

    public event Action ClaimClicked;

    private void Awake()
    {
        _claimButton.onClick.AddListener(() => ClaimClicked?.Invoke());
    }
}
```

А логика живёт отдельно:

```csharp
public sealed class PrizeScreenPresenter
{
    private readonly PrizeScreenView _view;
    private readonly IPrizeService _prizeService;
}
```

### ScriptableObject

`ScriptableObject` хорошо подходит для:

- конфигурации
- таблиц баланса
- статических наборов данных
- editor-friendly настроек

Для `Danro Jump` через `ScriptableObject` удобно хранить:

- стоимость игры
- настройки сложности
- параметры призов
- таблицы выдачи
- конфигурацию аппаратных режимов

Практика:

- `ScriptableObject` хранит данные
- поведение и orchestration живут в сервисах

Legacy reference:

- `Assets/Temp/Legacy/Scripts/Settings/MainSettingsDatabase.asset`
- `Assets/Temp/Legacy/Scripts/Settings/Core/SettingsDatabase.cs`
- `Assets/Temp/Legacy/Scripts/ScriptableObjects/MainSettings.asset`

При переносе этих материалов важно не тащить старую базу настроек как единственный источник истины без ревизии. Нужно выделить новые группы настроек: оплата, сложность, призы, `QR`, витрина, диагностика.

### EditorWindow / CustomEditor / PropertyDrawer

Если под "надстройками" понимать editor-расширения, то это тоже важный слой.

Они полезны, когда нужно:

- удобнее настраивать сервисные параметры
- валидировать конфигурацию
- упростить работу дизайнеру или оператору
- сделать собственные окна диагностики

Типичные инструменты:

- `CustomEditor`
- `PropertyDrawer`
- `EditorWindow`
- `MenuItem`

Legacy reference:

- `Assets/Temp/Legacy/Scripts/Editor/SettingsDatabaseEditor.cs`
- `Assets/Temp/Legacy/Scripts/Editor/SettingEntryDrawer.cs`
- `Assets/Temp/Legacy/Scripts/Editor/HierarchyBulkRenamerWindow.cs`
- `Assets/Temp/Legacy/Scripts/Editor/MouseWorldPositionOverlay.cs`

Эти инструменты можно использовать как идеи для нового tooling, но лучше переподключать их к новой модели настроек и новым namespace.

## Полезные паттерны над Unity API

### Adapter

Используется, когда нужно скрыть конкретный Unity API за интерфейсом.

Пример:

- `SceneManager` -> `ISceneFlowService`
- `PlayerPrefs` -> `ILocalSettingsStorage`
- `AudioSource` -> `IAudioPlaybackService`

### Facade

Используется, когда несколько Unity-подсистем должны выглядеть как одна прикладная операция.

Пример:

- показать экран награды
- проиграть анимацию
- активировать витрину
- ожидать подтверждение выдачи

Вместо разброса логики по сцене можно иметь:

```csharp
public interface IRewardPresentationFacade
{
    UniTask PresentRewardAsync(RewardResult rewardResult);
}
```

### Wrapper around static API

Подходит для:

- `Time`
- `Random`
- `PlayerPrefs`
- `SceneManager`
- `Application`

### Presenter / Controller layer

Подходит для UI:

- view знает только про кнопки и поля
- presenter знает про бизнес-реакции

Legacy UI-классы из `Assets/Temp/Legacy/Scripts/UI` чаще всего нужно переносить именно через этот паттерн:

- старый `MonoBehaviour` становится `View`
- бизнес-решения уходят в `Presenter` или application service
- прямые обращения к singleton-manager'ам заменяются интерфейсами
- переходы сцен уходят в `ISceneFlowService`

Например, `QRCodeModal` и `PrizeStandModal` стоит рассматривать как reference для будущих `QrRewardView` и `PrizeShowcaseView`, а не как финальные controllers.

## Что лучше не делать

Плохо:

- наследоваться от `MonoBehaviour` для всего подряд
- хранить бизнес-логику прямо в `Update`
- вызывать `SceneManager`, `PlayerPrefs`, `Random`, `Time` напрямую в доменных классах
- делать толстые `Manager`-классы, которые знают про всё

Хорошо:

- вводить точечные интерфейсы
- отделять runtime view от логики
- держать конфигурацию отдельно от поведения
- строить прикладные сервисы поверх Unity API

## Практические абстракции, которые особенно полезны для Danro Jump

### 1. Аппаратный слой

```csharp
public interface IPrizeShowcaseGateway
{
    UniTask OpenAsync(int prizeSlotId);
}
```

Legacy mapping:

- `PrizeStandController` -> `IPrizeShowcaseGateway`
- `ComSystemManager` -> `IHardwareSessionService`
- `ComNativeWrapper` -> `IComNativePortApi`

### 2. QR-выдача

```csharp
public interface IQrRewardService
{
    UniTask<QrRewardResult> CreateRewardCodeAsync(RewardRequest request);
}
```

Legacy mapping:

- `QRCodeUtils` -> `IQrTextureFactory`
- `QRCodeInfo` -> новая `QrRewardConfig`
- `QRCodePrize` -> `QrPrizeExecutor`

### 3. Баланс игры

```csharp
public interface IGameBalanceProvider
{
    int GetSessionPrice();
    float GetDifficultyMultiplier();
}
```

### 4. Сервисные настройки

```csharp
public interface IServiceSettingsService
{
    void SetSessionPrice(int value);
    void SetDifficulty(float value);
}
```

Legacy mapping:

- `SettingsManager` -> `IServiceSettingsService`
- `SettingsDatabase` -> `ISettingsDefinitionRepository`
- `SettingsUIGenerator` -> новый service mode presenter/view generator

### 5. Время и рандом

```csharp
public interface ITimeProvider
{
    float DeltaTime { get; }
}

public interface IRandomService
{
    int Range(int minInclusive, int maxExclusive);
}
```

## Когда делать editor-надстройки

Editor-расширения стоит делать, когда:

- ручная настройка через стандартный inspector становится слишком тяжёлой
- нужно валидировать таблицы призов
- нужно дать персоналу или дизайнеру безопасный интерфейс настройки
- нужно сделать диагностику подключённого оборудования

Хорошие кандидаты для editor tooling в этом проекте:

- окно сервисной конфигурации
- custom inspector для таблиц призов
- property drawer для настроек весов и диапазонов
- diagnostic window для `COM`-связи

## Когда остановиться и не переусложнять

Надстройки полезны, пока они:

- уменьшают связанность
- улучшают тестируемость
- делают архитектуру понятнее

Если абстракция:

- не скрывает ничего важного
- не даёт тестовой выгоды
- не помогает сопровождению

то она, скорее всего, лишняя.

## Рекомендуемый путь внедрения

1. Сначала выбрать legacy-подсистему из `Assets/Temp/Legacy`.
2. Выписать её реальные обязанности.
3. Вынести эти обязанности за новые интерфейсы.
4. Перенести только нужные модели данных.
5. Подключить новую реализацию через `Extenject`.
6. После этого при необходимости делать editor tooling.

Для `Danro Jump` разумный старт:

1. `SceneManager`
2. `PlayerPrefs`
3. `Time` и `Random`
4. аппаратные шлюзы
5. editor tooling для сервисных настроек

Для миграции legacy-кода разумный старт:

1. `COM` и витрина
2. призовой контур
3. сервисные настройки
4. `QR`-выдача
5. UI-модалки и admin screens

## Официальные и полезные источники

- https://docs.unity3d.com/6000.1/Documentation/Manual/class-ScriptableObject.html
- https://docs.unity3d.com/ja/2023.2/ScriptReference/ScriptableObject.html
- https://docs.unity3d.com/es/current/ScriptReference/CustomEditor.html
- https://docs.unity3d.com/ja/current/ScriptReference/PropertyDrawer.html
- https://docs.unity3d.com/es/current/ScriptReference/EditorWindow.html
- https://docs.unity3d.com/cn/2022.3/ScriptReference/MenuItem.html
