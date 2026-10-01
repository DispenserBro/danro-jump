# Полное руководство по Zenject (Extenject)

Zenject — это мощный фреймворк для внедрения зависимостей (Dependency Injection / Inversion of Control) в Unity. В этом проекте мы используем форк **Extenject**. Zenject решает проблему жесткой связности классов (spaghetti code) и избавляет от необходимости использовать паттерн Singleton.

## 1. Основы привязок (Bindings)

Настройка зависимостей происходит в классах-инсталляторах (обычно `MonoInstaller`). Главный принцип: **мы говорим контейнеру, какую реализацию отдать, когда кто-то запрашивает интерфейс или класс.**

### Основные методы привязки

```csharp
public class GameplayInstaller : MonoInstaller
{
    [SerializeField] private PlayerSettings settings;

    public override void InstallBindings()
    {
        // 1. AsSingle() — создает один экземпляр (Singleton в рамках контейнера)
        Container.Bind<IPlayerMovement>().To<JumpPlayerController>().AsSingle();

        // 2. AsTransient() — создает НОВЫЙ экземпляр при каждом запросе [Inject]
        Container.Bind<EnemyAI>().AsTransient();

        // 3. AsCached() — создает экземпляр один раз для данного типа привязки
        Container.Bind<IWeapon>().To<Sword>().AsCached();

        // 4. BindInstance() — привязка уже существующего объекта (например, из Инспектора)
        Container.BindInstance(settings).AsSingle();

        // 5. BindInterfacesAndSelfTo — класс будет доступен и по своему типу, и по всем его интерфейсам
        Container.BindInterfacesAndSelfTo<GameAudioService>().AsSingle();
    }
}
```

## 2. Способы внедрения (Injection)

Существует три основных способа внедрения зависимостей.

### 2.1 Конструктор (Constructor Injection) — РЕКОМЕНДУЕТСЯ
Используется для всех C# классов, которые не являются `MonoBehaviour`.

```csharp
public class EnemySpawner
{
    private readonly IPlayerMovement player;
    private readonly EnemySettings settings;

    // Зависимости передаются явно. Невозможно создать класс без них.
    public EnemySpawner(IPlayerMovement player, EnemySettings settings)
    {
        this.player = player;
        this.settings = settings;
    }
}
```

### 2.2 Метод (Method Injection) — ДЛЯ MONOBEHAVIOUR
Unity создает `MonoBehaviour` самостоятельно, минуя конструктор. Поэтому для них используется метод, помеченный атрибутом `[Inject]`. Он вызывается сразу после `Awake()`.

```csharp
public class PlayerHealth : MonoBehaviour
{
    private GameAudioService audioService;

    [Inject]
    public void Construct(GameAudioService audioService)
    {
        this.audioService = audioService;
    }
}
```

### 2.3 Поля (Field Injection) — НЕ РЕКОМЕНДУЕТСЯ
Хоть Zenject и поддерживает инъекцию прямо в приватные поля через `[Inject]`, это нарушает инкапсуляцию и усложняет чтение кода. Избегайте этого.

## 3. Жизненный цикл (Interfaces)

Zenject предоставляет интерфейсы, заменяющие стандартные `Start`, `Update` и `OnDestroy` Unity, что позволяет писать чистые C# классы без наследования от `MonoBehaviour`.

```csharp
// Чтобы эти интерфейсы работали, класс нужно привязать через BindInterfacesAndSelfTo()
public class GameManager : IInitializable, ITickable, IDisposable
{
    // Аналог Start()
    public void Initialize()
    {
        Debug.Log("Game started!");
    }

    // Аналог Update()
    public void Tick()
    {
        // Выполняется каждый кадр
    }

    // Аналог OnDestroy()
    public void Dispose()
    {
        // Очистка ресурсов, отписка от событий
    }
}
```

## 4. Контексты (Contexts)

Зависимости живут в **контейнерах**, которые иерархичны.
1. **ProjectContext**: Создается один раз при старте игры. Здесь лежат глобальные сервисы (`AudioService`, `NetworkManager`).
2. **SceneContext**: Создается при загрузке сцены. Здесь лежит всё, что нужно только для текущего уровня (`Player`, `LevelManager`). Имеет доступ ко всему из `ProjectContext`.
3. **GameObjectContext**: Локальный контейнер для сложных префабов (например, машина со своими колесами и двигателем).

## 5. Фабрики (Factories)

Никогда не используйте `new` для создания классов, у которых есть зависимости. Используйте фабрики Zenject!

### Простая фабрика (PlaceholderFactory)

```csharp
// 1. Объявляем класс
public class Bullet { /* ... */ }

// 2. Объявляем фабрику (внутри класса или отдельно)
public class BulletFactory : PlaceholderFactory<Bullet> { }

// 3. Биндим в инсталлере
Container.BindFactory<Bullet, BulletFactory>();

// 4. Используем
public class Weapon
{
    private BulletFactory factory;
    
    public Weapon(BulletFactory factory) => this.factory = factory;

    public void Shoot()
    {
        Bullet bullet = factory.Create(); // Пуля создана, все инъекции внутри нее выполнены
    }
}
```

## 6. Zenject Signals (Паттерн Event Bus)

Сигналы (Signals) — это потрясающий способ избавиться от жестких связей. Компоненты ничего не знают друг о друге, они просто отправляют и слушают сообщения (например, `PlayerDiedSignal`).

### Настройка
В инсталлере необходимо добавить `SignalBusInstaller.Install(Container);` и объявить сигналы:
```csharp
SignalBusInstaller.Install(Container);
Container.DeclareSignal<PlayerDiedSignal>();
```

### Отправка сигнала
```csharp
public class PlayerHealth
{
    private SignalBus signalBus;
    public PlayerHealth(SignalBus signalBus) => this.signalBus = signalBus;

    public void Die()
    {
        signalBus.Fire<PlayerDiedSignal>(); // Сигнал улетел!
    }
}
```

### Подписка на сигнал
```csharp
public class UIManager : IInitializable, IDisposable
{
    private SignalBus signalBus;
    public UIManager(SignalBus signalBus) => this.signalBus = signalBus;

    public void Initialize()
    {
        signalBus.Subscribe<PlayerDiedSignal>(OnPlayerDied);
    }

    public void Dispose()
    {
        signalBus.Unsubscribe<PlayerDiedSignal>(OnPlayerDied);
    }

    private void OnPlayerDied()
    {
        // Показать экран GameOver
    }
}
```

> [!TIP]
> При использовании `[Inject]` методов в MonoBehaviour, вы можете инжектить `SignalBus` и подписываться на сигналы прямо там, а отписываться в `OnDestroy()`.
