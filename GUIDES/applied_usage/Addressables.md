# Полное руководство по Addressables

Система **Addressables** (`com.unity.addressables`) — это замена устаревшей папке `Resources` и сложным `AssetBundles`. Она позволяет загружать контент по текстовому адресу из любой точки (локально или с удаленного сервера), не меняя код игры.

## 1. Загрузка одиночных ассетов и Интеграция с UniTask

В проекте мы используем `Cysharp.Threading.Tasks` для превращения неудобных `AsyncOperationHandle` в удобные `UniTask`.

```csharp
using UnityEngine.AddressableAssets;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class AssetLoader
{
    public async UniTask<GameObject> LoadPlayerPrefabAsync()
    {
        // "PlayerPrefab" — это адрес, заданный в окне Addressables Groups
        GameObject prefab = await Addressables.LoadAssetAsync<GameObject>("PlayerPrefab").ToUniTask();
        return prefab;
    }
}
```

## 2. Инстанцирование (InstantiateAsync)

Если вам нужно загрузить префаб и сразу создать его на сцене, **НЕ ИСПОЛЬЗУЙТЕ** `Object.Instantiate(await LoadAssetAsync())`.
Правильный способ — использовать `Addressables.InstantiateAsync`.

```csharp
public async UniTask<GameObject> SpawnEnemyAsync(Vector3 position)
{
    // Загрузит префаб в память и сразу создаст его копию на сцене.
    // Addressables сам отслеживает этот объект!
    GameObject enemyInstance = await Addressables.InstantiateAsync("EnemyPrefab", position, Quaternion.identity).ToUniTask();
    
    return enemyInstance;
}
```

## 3. Reference Counting и Управление Памятью (КРИТИЧНО)

Главная проблема Addressables — утечки памяти (Memory Leaks), если не освобождать ресурсы. Система использует счетчик ссылок (Reference Counting). 
- Загрузили текстуру — счетчик = 1.
- Загрузили ее еще раз где-то еще — счетчик = 2.
- Вызвали `Release` — счетчик = 1.
- Вызвали `Release` еще раз — счетчик = 0, текстура выгружена из оперативной памяти (RAM/VRAM).

### Правила освобождения памяти:

**Сценарий 1: Вы использовали `LoadAssetAsync`**
Если вы загрузили ассет (например, `ScriptableObject` настроек, AudioClip или Texture), вы обязаны вызвать `Addressables.Release()`.

```csharp
private AudioClip backgroundMusic;

public async UniTask PlayMusicAsync()
{
    backgroundMusic = await Addressables.LoadAssetAsync<AudioClip>("MenuMusic").ToUniTask();
    audioSource.PlayOneShot(backgroundMusic);
}

public void OnDestroy()
{
    // Если объект уничтожается, освобождаем память от музыки!
    if (backgroundMusic != null)
    {
        Addressables.Release(backgroundMusic);
    }
}
```

**Сценарий 2: Вы использовали `InstantiateAsync`**
Если вы создали GameObject через Addressables, вы **не должны** удалять его через `Destroy(gameObject)`. Используйте `Addressables.ReleaseInstance()`.

```csharp
public void KillEnemy(GameObject enemyInstance)
{
    // Это автоматически уничтожит GameObject на сцене И уменьшит счетчик префаба в памяти.
    // Если это был последний враг такого типа, сам префаб выгрузится из RAM.
    Addressables.ReleaseInstance(enemyInstance);
}
```

## 4. Загрузка по Меткам (Labels)

Addressables позволяет загружать списки ассетов. Например, вы можете пометить все префабы уровней меткой `Level` и загрузить их одним вызовом.

```csharp
public async UniTask<IList<LevelData>> LoadAllLevelsAsync()
{
    // Загружает все ассеты, у которых стоит Label "Level"
    IList<LevelData> levels = await Addressables.LoadAssetsAsync<LevelData>("Level", null).ToUniTask();
    return levels;
}
```
