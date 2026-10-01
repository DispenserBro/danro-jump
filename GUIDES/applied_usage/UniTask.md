# Полное руководство по UniTask

**UniTask** (`Cysharp.Threading.Tasks`) — это библиотека, которая заменяет стандартные Unity `Coroutine` и `System.Threading.Tasks.Task`. 

Главное преимущество UniTask — **полное отсутствие аллокаций памяти (Zero Allocation)**. Структуры `UniTask` созданы специально для Unity, они интегрированы с `PlayerLoop` и работают быстрее стандартных тасок C#.

## 1. Базовые принципы

- Если метод возвращает `Task` или `IEnumerator`, перепишите его на `UniTask`.
- Вместо `yield return null` используйте `await UniTask.Yield()`.
- Вместо `yield return new WaitForSeconds(1)` используйте `await UniTask.Delay(1000)`.

```csharp
using Cysharp.Threading.Tasks;

public async UniTaskVoid StartGameSequenceAsync()
{
    Debug.Log("Start");
    // Ждем 1 секунду игрового времени (учитывает Time.timeScale)
    await UniTask.Delay(TimeSpan.FromSeconds(1));
    Debug.Log("End");
}
```

## 2. Возврат значений и Forget()

В отличие от корутин, UniTask может легко возвращать значения:

```csharp
public async UniTask<int> CalculateScoreAsync()
{
    await UniTask.Delay(500);
    return 100;
}
```

Если вы вызываете асинхронный метод из синхронного кода (например, из клика по кнопке) и вам не нужно дожидаться результата, метод следует пометить как `UniTaskVoid` или вызвать у него `.Forget()`. Это гарантирует, что в случае ошибки (Exception), она будет выведена в консоль Unity.

```csharp
public void OnButtonClick()
{
    // Правильный вызов (Fire and Forget)
    PlayAnimationAsync().Forget();
}
```

## 3. Отмена (Cancellation) — КРИТИЧЕСКИ ВАЖНО

В Unity объекты часто уничтожаются (например, при смене сцены). Если ваша таска в этот момент "спит" в `await UniTask.Delay`, она проснется и попытается обратиться к уничтоженному объекту, вызвав `MissingReferenceException`.

Всегда прокидывайте `CancellationToken` во все асинхронные методы!

```csharp
private async UniTaskVoid MoveObjectAsync(CancellationToken token)
{
    while (true)
    {
        // Если токен отменен, Delay выкинет OperationCanceledException и цикл прервется
        await UniTask.Delay(100, cancellationToken: token);
        transform.position += Vector3.forward;
    }
}

private void Start()
{
    // Получаем токен, который АВТОМАТИЧЕСКИ отменится при OnDestroy() этого MonoBehaviour
    MoveObjectAsync(this.GetCancellationTokenOnDestroy()).Forget();
}
```

## 4. Продвинутые конструкции (Когда нужна мощь)

### Параллельное выполнение (WhenAll)
Если нужно загрузить три ресурса одновременно и подождать завершения всех:

```csharp
public async UniTask LoadLevelAsync()
{
    var task1 = LoadTextureAsync();
    var task2 = LoadAudioAsync();
    var task3 = LoadDataAsync();

    // Ждем, пока завершатся все три параллельные таски
    await UniTask.WhenAll(task1, task2, task3);
    Debug.Log("Всё загружено!");
}
```

### Гонка тасок (WhenAny)
Если нужно дождаться ПЕРВОГО события (например, игрок нажал кнопку ИЛИ истек таймер):

```csharp
public async UniTask WaitForInteractionAsync(CancellationToken ct)
{
    var clickTask = WaitUserClickAsync(ct);
    var timeoutTask = UniTask.Delay(TimeSpan.FromSeconds(5), cancellationToken: ct);

    int finishedIndex = await UniTask.WhenAny(clickTask, timeoutTask);

    if (finishedIndex == 0) Debug.Log("Игрок нажал кнопку!");
    else Debug.Log("Игрок ничего не сделал, время вышло.");
}
```

### Таймауты
Любой метод можно ограничить по времени:

```csharp
try
{
    // Если метод не завершится за 2 секунды, будет брошено исключение TimeoutException
    await DownloadDataAsync().Timeout(TimeSpan.FromSeconds(2));
}
catch (TimeoutException)
{
    Debug.LogError("Слишком долго!");
}
```

## 5. Многопоточность (Переключение потоков)

Unity API (например, `transform.position`, `Instantiate`) можно использовать **только в Главном Потоке (Main Thread)**.
Но если вам нужно сделать тяжелые вычисления (генерация уровня, парсинг JSON 20MB), вы можете временно "прыгнуть" в фоновый поток (Thread Pool), а затем вернуться обратно!

```csharp
public async UniTask ProcessHeavyDataAsync()
{
    // Мы в главном потоке (Main Thread)
    var json = Resources.Load<TextAsset>("heavy_data").text;

    // ПРЫГАЕМ В ФОНОВЫЙ ПОТОК (Thread Pool)
    await UniTask.SwitchToThreadPool();

    // Тут нельзя трогать Unity API, но можно грузить процессор на 100%
    var parsedData = Newtonsoft.Json.JsonConvert.DeserializeObject<Data>(json);
    HeavyCalculations(parsedData);

    // ВОЗВРАЩАЕМСЯ В ГЛАВНЫЙ ПОТОК
    await UniTask.SwitchToMainThread();

    // Снова можно трогать Unity API!
    transform.position = parsedData.startPosition;
}
```

> [!IMPORTANT]
> Переключение потоков с помощью `UniTask` — это мощнейший инструмент оптимизации, который позволяет избавиться от фризов (лагов) при подгрузке тяжелых данных, оставляя код линейным и легко читаемым.
