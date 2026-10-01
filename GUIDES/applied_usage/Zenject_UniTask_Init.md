# Комбинированный пример: Zenject + UniTask + Addressables для старта игры

В современных проектах старт игры — это не просто `SceneManager.LoadScene()`. Это цепочка асинхронных операций: проверка обновлений, загрузка баз данных (баланс, цены), авторизация, инициализация железа (как в Danro Jump) и только затем показ меню.

Этот гайд показывает, как объединить 3 главные библиотеки проекта в одну элегантную систему загрузки.

## 1. Интерфейс Загрузочной Задачи (Task)

Для начала, сделаем систему расширяемой. Вместо жестко закодированных вызовов, создадим интерфейс `IStartupTask`.

```csharp
using Cysharp.Threading.Tasks;

public interface IStartupTask
{
    // Вес задачи для прогресс-бара (например, 10%)
    float ProgressWeight { get; }
    
    // Выполнение задачи с возможностью отмены
    UniTask ExecuteAsync(System.Threading.CancellationToken ct);
}
```

## 2. Реализация задач (Примеры)

### Задача 1: Загрузка конфигураций (Addressables)
```csharp
using UnityEngine.AddressableAssets;

public class LoadConfigTask : IStartupTask
{
    public float ProgressWeight => 30f;
    private readonly ConfigManager configManager;

    // Внедряем зависимость!
    public LoadConfigTask(ConfigManager configManager) => this.configManager = configManager;

    public async UniTask ExecuteAsync(System.Threading.CancellationToken ct)
    {
        // Загружаем глобальные настройки из Addressables
        var config = await Addressables.LoadAssetAsync<GameConfig>("GlobalConfig")
            .WithCancellation(ct); // Привязываем токен отмены
        
        configManager.Initialize(config);
    }
}
```

### Задача 2: Подключение оборудования (COM-порт / UniTask)
```csharp
public class HardwareInitTask : IStartupTask
{
    public float ProgressWeight => 40f;
    private readonly ComSystem comSystem;

    public HardwareInitTask(ComSystem comSystem) => this.comSystem = comSystem;

    public async UniTask ExecuteAsync(System.Threading.CancellationToken ct)
    {
        // Инициализация железа с таймаутом в 5 секунд!
        try 
        {
            await comSystem.InitializeHardwareAsync().Timeout(System.TimeSpan.FromSeconds(5));
        }
        catch (System.TimeoutException)
        {
            UnityEngine.Debug.LogError("Железо не ответило вовремя!");
            // Показать ошибку на экране
        }
    }
}
```

## 3. Регистрация задач в Zenject (Installer)

В `BootstrapInstaller` (привязанном к первой пустой сцене) мы регистрируем все наши задачи. 

```csharp
public class BootstrapInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        // Биндим сам менеджер загрузки
        Container.BindInterfacesAndSelfTo<AppBootstrapper>().AsSingle();

        // Биндим задачи как список. Zenject соберет их все в один List<IStartupTask>
        Container.Bind<IStartupTask>().To<LoadConfigTask>().AsCached();
        Container.Bind<IStartupTask>().To<HardwareInitTask>().AsCached();
    }
}
```

## 4. Исполнитель (AppBootstrapper)

Этот класс запустится автоматически благодаря интерфейсу `IInitializable`.

```csharp
using System.Collections.Generic;
using Zenject;
using Cysharp.Threading.Tasks;

public class AppBootstrapper : IInitializable, System.IDisposable
{
    private readonly List<IStartupTask> tasks;
    private readonly ISceneFlowService sceneFlow;
    private System.Threading.CancellationTokenSource cts;

    // Zenject автоматически передаст все классы, которые мы привязали к IStartupTask
    public AppBootstrapper(List<IStartupTask> tasks, ISceneFlowService sceneFlow)
    {
        this.tasks = tasks;
        this.sceneFlow = sceneFlow;
    }

    public void Initialize()
    {
        cts = new System.Threading.CancellationTokenSource();
        RunStartupFlowAsync(cts.Token).Forget();
    }

    private async UniTaskVoid RunStartupFlowAsync(System.Threading.CancellationToken ct)
    {
        float totalProgress = 0f;

        foreach (var task in tasks)
        {
            // Выполняем задачу последовательно
            await task.ExecuteAsync(ct);
            
            // Здесь можно вызывать сигнал для обновления UI прогресс-бара
            totalProgress += task.ProgressWeight;
            UnityEngine.Debug.Log($"Progress: {totalProgress}%");
        }

        // 4. Когда все задачи выполнены — переходим в меню!
        sceneFlow.OpenMainMenu();
    }

    public void Dispose()
    {
        cts?.Cancel();
        cts?.Dispose();
    }
}
```

**Итог**: Мы получили масштабируемую, потокобезопасную (в рамках UniTask) и легко тестируемую систему инициализации. Чтобы добавить новую логику (например, проверку лицензии), нужно просто создать новый класс `IStartupTask` и забиндить его в инсталлере в 1 строчку кода! Никаких гигантских God-объектов.
