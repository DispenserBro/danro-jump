# Специфичные возможности Unity и железа

В разработке проекта `Danro Jump` используются продвинутые интеграции, не типичные для мобильных или ПК-игр, так как игра предназначена для специализированного оборудования (аркадного автомата).

## 1. Взаимодействие с внешним оборудованием (COM-порты)

Движок Unity не имеет встроенных средств для общения с внешними платами (кроме геймпадов). Поэтому используется стандартная библиотека .NET `System.IO.Ports`.

### Проблема Потоков (Thread Safety)
Главная проблема при работе с `SerialPort` — события приема данных происходят в **фоновом потоке Windows**. Если вы попытаетесь вызвать `Debug.Log`, изменить UI или заспавнить `GameObject` прямо из события `DataReceived`, Unity выдаст ошибку (или тихо упадет), потому что её API не потокобезопасен (not thread-safe).

### Решение: Очередь (ConcurrentQueue)
Все данные из фонового потока складываются в потокобезопасную очередь, а главный поток (в методе `Update`) читает эту очередь и применяет изменения к игре.

```csharp
using System.IO.Ports;
using System.Collections.Concurrent;
using UnityEngine;

public class ComPortManager : MonoBehaviour
{
    private SerialPort serialPort;
    
    // Потокобезопасная очередь для связи фонового и главного потоков
    private ConcurrentQueue<byte> incomingDataQueue = new ConcurrentQueue<byte>();

    public void StartConnection(string port)
    {
        serialPort = new SerialPort(port, 9600);
        serialPort.DataReceived += OnDataReceived;
        serialPort.Open();
    }

    // ВНИМАНИЕ: Вызывается в фоновом потоке!
    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        int bytesToRead = serialPort.BytesToRead;
        for (int i = 0; i < bytesToRead; i++)
        {
            // Просто складываем байты в очередь, Unity API не трогаем!
            incomingDataQueue.Enqueue((byte)serialPort.ReadByte());
        }
    }

    // ВНИМАНИЕ: Вызывается в главном потоке Unity
    private void Update()
    {
        // Читаем очередь
        while (incomingDataQueue.TryDequeue(out byte data))
        {
            ProcessData(data); // Здесь уже можно менять UI или двигать персонажа
        }
    }
}
```

## 2. Захват фокуса Windows (user32.dll)

Аркадный автомат работает без клавиатуры и мыши (для игрока). Если во время игры всплывет уведомление Windows или антивирус, игра потеряет фокус, и аппаратный ввод (или клавиатура) перестанет работать.

Для защиты от этого используется `DllImport` — прямой вызов нативных библиотек Windows из C#.

```csharp
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Cysharp.Threading.Tasks;

public class FocusEnforcer
{
    // Импорт системной функции WinAPI
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    public void EnforceFocus()
    {
        // Получаем дескриптор (Handle) окна нашей игры
        Process currentProcess = Process.GetCurrentProcess();
        IntPtr mainWindowHandle = currentProcess.MainWindowHandle;

        if (mainWindowHandle != IntPtr.Zero)
        {
            // Говорим Windows "Сделай это окно главным и активным!"
            SetForegroundWindow(mainWindowHandle);
        }
    }
}
```

## 3. Object Pooling (Пул объектов)

Для аркадной игры с постоянным движением (прыжки, платформы, враги) критически важно избегать вызовов `Instantiate` и `Destroy` во время геймплея. Это вызывает "сборку мусора" (Garbage Collection) и фризы на пару кадров.

Вместо уничтожения объектов, их нужно "выключать" и возвращать в пул, а когда нужен новый — доставать из пула и включать.

В Unity есть встроенный класс `UnityEngine.Pool.ObjectPool<T>` (начиная с Unity 2021).

```csharp
using UnityEngine;
using UnityEngine.Pool;

public class PlatformSpawner : MonoBehaviour
{
    public GameObject platformPrefab;
    private ObjectPool<GameObject> platformPool;

    private void Awake()
    {
        platformPool = new ObjectPool<GameObject>(
            createFunc: () => Instantiate(platformPrefab), // Как создать новый
            actionOnGet: (obj) => obj.SetActive(true),     // Как достать из пула
            actionOnRelease: (obj) => obj.SetActive(false),// Как вернуть в пул
            actionOnDestroy: (obj) => Destroy(obj),        // Как уничтожить, если пул переполнен
            defaultCapacity: 20,
            maxSize: 100
        );
    }

    public void SpawnPlatform(Vector3 position)
    {
        // Берем готовую платформу из пула, не вызывая Instantiate!
        GameObject platform = platformPool.Get();
        platform.transform.position = position;
    }

    public void ReturnPlatform(GameObject platform)
    {
        // Возвращаем в пул (она выключится)
        platformPool.Release(platform);
    }
}
```
