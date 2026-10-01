# Dependency Injection через Extenject

## Зачем это нужно

`Dependency Injection` через `Extenject` нужен, чтобы:

- разделять игровую логику и Unity-обвязку
- уменьшать количество жёстких ссылок между компонентами
- легче тестировать код
- проще собирать разные режимы проекта: игра, сервисный режим, призовой контур, аппаратный слой

Для `Danro Jump` это особенно полезно, потому что проект со временем будет сочетать:

- аркадный геймплей
- работу с `COM`-портами
- сервисные экраны для персонала
- призовую витрину
- сценарии выдачи через `QR`-коды

Без DI такие подсистемы быстро начинают зависеть друг от друга напрямую и усложняют сопровождение.

## Когда Extenject действительно полезен

`Extenject` особенно хорошо подходит, если в проекте есть:

- несколько отдельных подсистем
- сервисы с долгим жизненным циклом
- необходимость подменять реализации
- разные режимы запуска
- желание держать доменную логику вне `MonoBehaviour`

Для `Danro Jump` типичные кандидаты на DI:

- `IGameSessionService`
- `IScoreService`
- `IPrizeService`
- `IPrizeShowcaseGateway`
- `IQrRewardService`
- `IComPortGateway`
- `IServiceModeAccessPolicy`
- `IGameBalanceProvider`

## Как использовать legacy-код из Assets/Temp/Legacy

В `Assets/Temp/Legacy` сохранён старый код, который может пригодиться для будущей реализации. Его не нужно подключать к runtime целиком: он должен быть источником идей, API-контрактов, протоколов и моделей.

Самые полезные legacy-кандидаты для DI:

- `Assets/Temp/Legacy/Scripts/Controls/Inputs/ComSystem` — основа для аппаратного слоя, `COM`, handshake, ролей устройств и призовой витрины
- `Assets/Temp/Legacy/Scripts/Managers/PaymentManager.cs` — reference для кредитов, оплаты и free play режима
- `Assets/Temp/Legacy/Scripts/Managers/PrizeSystemManager.cs` — reference для orchestration призов
- `Assets/Temp/Legacy/Scripts/Prizes` — модели и исполнители призов
- `Assets/Temp/Legacy/Scripts/Settings` — база для сервисных настроек персонала
- `Assets/Temp/Legacy/Scripts/Utilities/QRCodeUtils.cs` — генерация `QR` и модели `QRCodeInfo`

Правило переноса:

- сначала выделить интерфейс новой подсистемы
- затем перенести минимальную доменную модель
- после этого завернуть legacy-алгоритм в новую реализацию сервиса
- только в конце подключить реализацию через installer

Пример: вместо прямого переноса `ComSystemManager` лучше выделить контракт:

```csharp
public interface IComDeviceService
{
    IReadOnlyList<string> ConnectedDeviceIds { get; }
    UniTask InitializeAsync();
    UniTask SendAsync(string deviceId, string command);
}
```

А старую связку `ComHub`, `ComDeviceClient`, `ComNativeWrapper` использовать как reference для реализации `ComDeviceService`.

## Legacy как Anti-Corruption Layer

Если старый код нужно подключить временно, его лучше обернуть anti-corruption layer:

```csharp
public sealed class LegacyPrizeShowcaseGateway : IPrizeShowcaseGateway, IDisposable
{
    private readonly LegacyPrizeStandAdapter _adapter;

    public LegacyPrizeShowcaseGateway(LegacyPrizeStandAdapter adapter)
    {
        _adapter = adapter;
    }

    public UniTask OpenAsync(int prizeSlotId)
    {
        return _adapter.TryOpenSlotAsync(prizeSlotId);
    }

    public void Dispose()
    {
        _adapter.Dispose();
    }
}
```

Так новый код зависит от `IPrizeShowcaseGateway`, а не от старых `RR.Common.*` классов.

## Базовая модель Extenject

В `Extenject` важно понимать три уровня:

- `ProjectContext` — глобальные зависимости на весь runtime
- `SceneContext` — зависимости внутри конкретной сцены
- `GameObjectContext` — локальный контейнер для prefab или отдельного объекта

Практическое правило:

- в `ProjectContext` складывай только по-настоящему глобальные вещи
- в `SceneContext` складывай зависимости конкретного режима или сцены
- `GameObjectContext` используй только там, где действительно нужен локальный composition root

## Рекомендуемая стратегия для Danro Jump

### ProjectContext

В `ProjectContext` обычно стоит держать:

- конфигурацию приложения
- логирование
- фабрики системного уровня
- глобальные настройки локализации
- сервисы, которые переживают смену сцен

Примеры:

```csharp
Container.BindInterfacesAndSelfTo<AppLifetime>().AsSingle();
Container.BindInterfacesAndSelfTo<GameSettingsProvider>().AsSingle();
Container.Bind<IComPortGateway>().To<ComPortGateway>().AsSingle();
Container.Bind<IQrRewardService>().To<QrRewardService>().AsSingle();
Container.Bind<IPrizeService>().To<PrizeService>().AsSingle();
Container.Bind<IPaymentService>().To<PaymentService>().AsSingle();
```

### SceneContext

В `SceneContext` конкретной сцены лучше держать:

- локальный gameplay flow
- сценовые контроллеры
- scene-specific presenters
- экранные сервисы

Пример для игровой сцены:

```csharp
public sealed class GameplayInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        Container.BindInterfacesAndSelfTo<GameSessionService>().AsSingle();
        Container.BindInterfacesAndSelfTo<PlatformSpawner>().AsSingle();
        Container.BindInterfacesAndSelfTo<PlayerDeathHandler>().AsSingle();
        Container.BindInterfacesAndSelfTo<GameHudPresenter>().AsSingle();
    }
}
```

### Отдельный сервисный режим

Сервисный режим для персонала лучше оформлять отдельной сценой или отдельным сценовым контекстом, а не примешивать к gameplay-сцене напрямую.

Тогда там можно поднимать:

- настройки стоимости игры
- настройки сложности
- настройки призов
- диагностику `COM`-оборудования
- ручные тесты витрины и выдачи

## Composition Root

Главное правило `Extenject`:

- контейнер знает, как собрать объект
- сам объект не должен знать, где искать свои зависимости

Это значит:

- не использовать `FindObjectOfType`
- не использовать singleton-поиск как основной способ сборки
- не прокидывать контейнер в доменную логику как сервис-локатор

Хорошо:

```csharp
public sealed class PrizeService : IPrizeService
{
    private readonly IPrizeRepository _repository;
    private readonly IPrizeShowcaseGateway _showcaseGateway;

    public PrizeService(
        IPrizeRepository repository,
        IPrizeShowcaseGateway showcaseGateway)
    {
        _repository = repository;
        _showcaseGateway = showcaseGateway;
    }
}
```

Плохо:

```csharp
public sealed class PrizeService
{
    public PrizeService()
    {
        var gateway = ProjectContext.Instance.Container.Resolve<IPrizeShowcaseGateway>();
    }
}
```

## Что именно инжектить

Обычно через `Extenject` лучше инжектить:

- интерфейсы
- чистые C# сервисы
- конфигурационные объекты
- фабрики
- адаптеры к внешним системам

С осторожностью:

- `MonoBehaviour`
- `ScriptableObject`
- runtime-created prefab-объекты

Если зависимость является Unity-компонентом сцены, лучше явно понимать её жизненный цикл.

## Типичный набор ролей в проекте

Для `Danro Jump` удобно делить классы так:

### Domain / Core

- правила игры
- подсчёт наград
- проверка eligibility для выдачи призов
- политика сложности

### Application / Services

- orchestration игровых сценариев
- координация сервисного режима
- работа с `QR`
- orchestration аппаратных операций

### Infrastructure

- `COM`-порт
- Unity scene loading
- persistence
- конкретная работа с UI и устройствами

### Presentation

- HUD presenters
- menu presenters
- service panel presenters

`Extenject` особенно хорошо работает, когда связывает эти уровни без прямой жёсткой сцепки.

## Рекомендуемые binding-паттерны

### BindInterfacesAndSelfTo

Удобен для сервисов с несколькими ролями:

```csharp
Container.BindInterfacesAndSelfTo<GameSessionService>().AsSingle();
```

### FromInstance

Хорошо подходит для конфигурации:

```csharp
Container.Bind<GameBalanceConfig>().FromInstance(_balanceConfig).AsSingle();
```

### FromComponentInHierarchy

Подходит только для сценовых объектов, которые действительно уже существуют:

```csharp
Container.Bind<GameplayHudView>().FromComponentInHierarchy().AsSingle();
```

### Factory

Подходит для runtime-создания:

- врагов
- платформ
- popup-элементов
- наградных визуальных объектов

## Жизненные циклы

Для сервисов в `Extenject` чаще всего пригодятся:

- `IInitializable`
- `ITickable`
- `IDisposable`

Практика:

- `IInitializable` используй для явного старта
- `ITickable` только там, где реально нужен цикл обновления
- `IDisposable` обязательно для внешних ресурсов, особенно `COM`-соединений и подписок

Пример:

```csharp
public sealed class ComPortGateway : IComPortGateway, IInitializable, IDisposable
{
    public void Initialize()
    {
        // Открытие и подготовка порта
    }

    public void Dispose()
    {
        // Корректное закрытие порта
    }
}
```

## Связка с управлением сценами

Для сцен в `Extenject` важны два сценария:

- передача зависимостей через `ProjectContext`
- передача параметров при переходах

Если нужно передавать параметры между сценами, удобнее не использовать статические поля, а оформлять отдельный переходный сервис или `ZenjectSceneLoader`.

Это особенно важно для переходов вида:

- из attract/menu режима в игровую сессию
- из игры в экран награды
- из игры в сервисный режим

## Чего лучше избегать

- делать `ProjectContext` свалкой всех сервисов
- инжектить всё подряд в `MonoBehaviour`
- дублировать bindings в разных installers без правил
- смешивать доменную логику и прямую работу с Unity API
- решать все проблемы через один огромный installer

## Практическая схема для этого проекта

На старте проекта удобно держать такой набор installers:

- `ProjectInstaller`
- `GameplayInstaller`
- `MenuInstaller`
- `ServiceModeInstaller`
- `PrizeFlowInstaller`
- `HardwareInstaller`

Пример разбиения ответственности:

- `ProjectInstaller` — глобальные настройки и shared-сервисы
- `GameplayInstaller` — игровой цикл и gameplay-сервисы
- `ServiceModeInstaller` — настройки персонала
- `HardwareInstaller` — `COM`, диагностика, витрина

## Рекомендуемая карта legacy -> новые сервисы

| Legacy source | Новый слой |
| --- | --- |
| `ComSystemManager`, `ComHub`, `ComDeviceClient` | `IComDeviceService`, `IComPortGateway`, `IHardwareDiagnosticsService` |
| `PrizeStandController` | `IPrizeShowcaseGateway` |
| `PrizeSystemManager`, `PrizeBase`, `PrizeDatabase` | `IPrizeService`, `IPrizeRepository`, `IPrizeExecutor` |
| `PaymentManager`, credit-события из `ComSystemManager` | `IPaymentService`, `ICreditWallet` |
| `SettingsManager`, `SettingsDatabase` | `IServiceSettingsService`, `ISettingsRepository` |
| `QRCodeUtils`, `QRCodeInfo`, `QRCodePrize` | `IQrRewardService`, `IQrTextureFactory` |

Эта карта не означает прямое копирование классов. Она показывает, какие обязанности старого кода стоит сохранить в новой архитектуре.

## Как внедрять DI поэтапно

Не нужно переводить весь проект на `Extenject` за один раз.

Лучше идти по шагам:

1. Выбрать один legacy-блок из `Assets/Temp/Legacy`, например `COM` или `Prize`.
2. Описать новый интерфейс без зависимости от `RR.Common.*`.
3. Перенести минимальные модели данных.
4. Написать новую реализацию или временный adapter.
5. Подключить её через installer.
6. Отделить UI от прикладной логики.
7. Только потом строить более широкую сетку installers и фабрик.

## Для каких задач это даст максимальную пользу в Danro Jump

- для сервисного режима
- для аппаратной интеграции
- для призовой логики
- для разделения игровых режимов
- для тестируемой бизнес-логики без прямой привязки к `MonoBehaviour`

## Официальные и полезные источники

- https://github.com/Mathijs-Bakker/Extenject
- https://github.com/Mathijs-Bakker/Extenject/releases
- `GUIDES/packages/zenject/README.md`
