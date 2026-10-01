# Управление сценами в Unity

## Зачем это нужно

Для `Danro Jump` управление сценами — это не просто переход между экранами.

Оно должно покрывать:

- старт приложения
- меню и attract-режим
- игровую сцену
- сервисный режим для персонала
- призовой экран
- сценарии диагностики аппаратуры
- будущую миграцию legacy-flow из `Assets/Temp/Legacy`

Если не зафиксировать стратегию управления сценами заранее, проект быстро превратится в набор экранов и singleton-переходов без понятного жизненного цикла.

## Какая цель у сцены

Сцена в Unity должна быть контейнером для:

- конкретного режима приложения
- конкретного визуального набора
- конкретного composition root

Сцена не должна быть:

- случайным складом систем
- единственным местом, где живёт вся логика приложения
- способом прятать глобальные зависимости

## Рекомендуемая модель сцен для Danro Jump

Практически для этого проекта удобно мыслить такими режимами:

- `Bootstrap`
- `MainMenu` или `Attract`
- `Gameplay`
- `Prize`
- `ServiceMode`
- `Diagnostics`

## Как учитывать legacy-код

В `Assets/Temp/Legacy` есть старые UI, managers и flow-классы, которые уже описывают похожие сценарии:

- `Assets/Temp/Legacy/Scripts/UI/MainMenu.cs`
- `Assets/Temp/Legacy/Scripts/UI/GameEndUIController.cs`
- `Assets/Temp/Legacy/Scripts/UI/Modal/PrizeStandModal.cs`
- `Assets/Temp/Legacy/Scripts/UI/Modal/QRCodeModal.cs`
- `Assets/Temp/Legacy/Scripts/UI/SettingsUI.cs`
- `Assets/Temp/Legacy/Scripts/Managers/PaymentManager.cs`
- `Assets/Temp/Legacy/Scripts/Managers/PrizeSystemManager.cs`
- `Assets/Temp/Legacy/Scripts/Managers/StartupLicenseManager.cs`

Их не стоит импортировать как готовые сцены или прямые зависимости. Полезнее извлечь из них:

- список нужных экранов
- состояния переходов
- события завершения игры и выдачи приза
- требования к сервисным модалкам
- startup/licensing сценарий

После этого flow нужно собрать заново через `ISceneFlowService`, `Extenject` installers и новые scene payload-модели.

### Bootstrap

Нужен для:

- ранней инициализации приложения
- проверки конфигурации
- старта глобальных сервисов
- перехода в основной режим
- опциональной проверки внешнего launcher/licensing flow

Если проект использует `Extenject`, здесь удобно поднимать глобальную инфраструктуру через `ProjectContext` и затем переключать пользователя в нужную сцену.

Legacy reference:

- `Assets/Temp/Legacy/Scripts/Managers/StartupLicenseManager.cs`
- `Assets/Temp/Legacy/StreamingAssets/README.txt`
- `Assets/Temp/Legacy/StreamingAssets/RRLauncher.exe.lic.json`

### MainMenu / Attract

Подходит для:

- стартового UI
- демонстрационного экрана
- ожидания запуска игры
- выбора пользовательского потока
- проверки возможности старта через кредиты или free play

### Gameplay

Подходит для:

- игрового цикла
- платформ
- игрока
- камеры
- счёта
- логики наградного результата

Legacy reference:

- `Assets/Temp/Legacy/Scripts/Behaviour/LevelFinish.cs`
- `Assets/Temp/Legacy/Scripts/UI/GameEndUIController.cs`
- `Assets/Temp/Legacy/Scripts/Managers/PaymentManager.cs`

### Prize

Лучше вынести отдельно, если:

- нужен отдельный UX выдачи
- есть сценарии взаимодействия с витриной
- нужен показ `QR`
- нужно отличать победный flow от основного gameplay flow

Legacy reference:

- `Assets/Temp/Legacy/Scripts/Managers/PrizeSystemManager.cs`
- `Assets/Temp/Legacy/Scripts/Prizes`
- `Assets/Temp/Legacy/Scripts/UI/Modal/PrizeStandModal.cs`
- `Assets/Temp/Legacy/Scripts/UI/Modal/QRCodeModal.cs`
- `Assets/Temp/Legacy/Scripts/UI/PrizeErrorDisplay.cs`

### ServiceMode

Для `Danro Jump` это отдельная продуктовая сцена, а не просто popup поверх игры.

Туда логично вынести:

- стоимость игры
- параметры сложности
- таблицы призов
- сервисные настройки
- тесты `COM`-устройств
- ручные сервисные операции

Legacy reference:

- `Assets/Temp/Legacy/Scripts/Settings`
- `Assets/Temp/Legacy/Scripts/UI/SettingsUI.cs`
- `Assets/Temp/Legacy/Scripts/UI/Modal/PrizeStandSettingsModal.cs`
- `Assets/Temp/Legacy/Scripts/UI/Modal/QRCodeSettingsModal.cs`
- `Assets/Temp/Legacy/Scripts/UI/AdminStatsWindowUIController.cs`

### Diagnostics

Диагностическая сцена нужна для проверки оборудования без запуска игры.

Туда логично вынести:

- список найденных `COM`-устройств
- роли устройств после handshake
- состояние призовой витрины
- ручное открытие ячеек
- проверку подсветки и служебных кнопок

Legacy reference:

- `Assets/Temp/Legacy/Scripts/Controls/Inputs/ComSystem`
- `Assets/Temp/Legacy/Scripts/Controls/Inputs/ButtonPinMapping.cs`
- `Assets/Temp/Legacy/Scripts/Controls/Inputs/ComSystem/LightController.cs`

## Single или Additive

В Unity есть два базовых сценария загрузки:

- `LoadSceneMode.Single`
- `LoadSceneMode.Additive`

### Когда использовать Single

Подходит, если:

- новый режим полностью заменяет предыдущий
- старые объекты больше не нужны
- хочется простого жизненного цикла

Для большинства переходов в `Danro Jump` это будет основной вариант:

- `MainMenu -> Gameplay`
- `Gameplay -> Prize`
- `Prize -> MainMenu`
- `MainMenu -> ServiceMode`

### Когда использовать Additive

Подходит, если:

- нужно держать несколько сцен одновременно
- есть отдельный persistent environment
- есть shared UI scene
- есть модульный подход к окружению

Например, additive loading может пригодиться, если проект позже разделит:

- общую `UI`-сцену
- игровую сцену
- отдельную сцену диагностического overlay

Но additive loading лучше включать только тогда, когда это действительно нужно, потому что он усложняет жизненный цикл.

## Практическая рекомендация для старта

Для первых этапов проекта лучше придерживаться простой схемы:

- основной режим сцены грузится через `Single`
- глобальные зависимости живут не в persistent scene, а в `ProjectContext`
- additive loading включается только после появления реальной потребности

## Что важно знать из Unity API

Полезные базовые операции:

- `SceneManager.LoadSceneAsync`
- `SceneManager.UnloadSceneAsync`
- `SceneManager.SetActiveScene`
- `LoadSceneMode.Additive`

Практические выводы из документации Unity:

- additive loading не выгружает текущую сцену автоматически
- `UnloadSceneAsync` удаляет объекты сцены, но не обязательно освобождает память ассетов полностью
- при одинаковых именах сцен лучше использовать полный путь, а не короткое имя

## Сервис загрузки сцен

Лучше не вызывать `SceneManager` напрямую из любого места проекта.

Вместо этого удобнее сделать собственную абстракцию:

```csharp
public interface ISceneFlowService
{
    UniTask OpenMainMenuAsync();
    UniTask OpenGameplayAsync();
    UniTask OpenPrizeAsync();
    UniTask OpenServiceModeAsync();
}
```

А внутри уже использовать Unity API:

```csharp
public sealed class SceneFlowService : ISceneFlowService
{
    public async UniTask OpenGameplayAsync()
    {
        await SceneManager.LoadSceneAsync("Gameplay", LoadSceneMode.Single);
    }
}
```

Это полезно потому что:

- сцены перестают быть разбросанной строковой зависимостью по всему проекту
- легче подменять реализацию
- проще тестировать flow-логику
- легче добавить preloading, guards и сервисные проверки

## Что лучше не делать

Плохо:

- вызывать `SceneManager.LoadScene` из `Button.onClick` напрямую
- разбрасывать строки имён сцен по десяткам скриптов
- хранить сценовые параметры в static-полях
- смешивать переходы сцен, бизнес-логику и UI-логику

Хорошо:

- иметь единый scene flow service
- вынести имена сцен в enum-like структуру или registry
- решать переходы через application layer

## Передача данных между сценами

Для `Danro Jump` между сценами могут передаваться:

- результат игровой сессии
- идентификатор приза
- флаг, нужно ли показать `QR`
- параметры сервисного запуска
- результат аппаратной проверки

Не стоит передавать это через:

- глобальные static-поля без контроля
- случайные `DontDestroyOnLoad` объекты

Лучше использовать:

- `ProjectContext` + отдельный state service
- scene transition payload service
- `ZenjectSceneLoader`, если переход tightly integrated с `Extenject`

## Связка с Extenject

Если проект строится вокруг `Extenject`, то у каждой важной сцены должен быть свой `SceneContext` и свой installer.

Пример:

- `MainMenuInstaller`
- `GameplayInstaller`
- `PrizeInstaller`
- `ServiceModeInstaller`

Тогда каждая сцена получает:

- собственный composition root
- локальные dependencies
- ясный жизненный цикл

## Рекомендуемая структура сцен для этого проекта

На текущем горизонте проектирования можно ориентироваться на такую структуру:

- `Bootstrap`
- `MainMenu`
- `Gameplay`
- `Prize`
- `ServiceMode`

Позже при необходимости:

- `SharedUI`
- `Diagnostics`
- `HardwareTest`

## Как отделять сцены от бизнес-логики

Хорошая практика:

- сцена отвечает за состав объектов и визуальное окружение
- service layer отвечает за решение, куда идти дальше
- domain layer отвечает за правила

Например:

- gameplay завершился
- `GameSessionService` решил, есть ли приз
- `PrizeDecisionService` определил тип выдачи
- `SceneFlowService` открыл `Prize`-сцену

В результате scene loading не принимает продуктовые решения самостоятельно.

## Переходы, которые стоит спроектировать заранее

Для `Danro Jump` полезно заранее описать хотя бы такие переходы:

- `Bootstrap -> MainMenu`
- `MainMenu -> Gameplay`
- `Gameplay -> Prize`
- `Gameplay -> MainMenu`
- `MainMenu -> ServiceMode`
- `ServiceMode -> MainMenu`
- `ServiceMode -> Diagnostics`
- `Diagnostics -> ServiceMode`

Если есть аварийные сценарии:

- `Any -> Diagnostics`
- `Diagnostics -> ServiceMode`

## Минимальный практический план внедрения

1. Зафиксировать список целевых сцен.
2. Сверить его с legacy-экранами из `Assets/Temp/Legacy/Scripts/UI`.
3. Завести единый scene flow service.
4. Убрать прямые вызовы `SceneManager` из UI.
5. Связать основные сцены с отдельными installers.
6. Описать payload для переходов `Gameplay -> Prize` и `MainMenu -> Gameplay`.
7. После этого решать, нужен ли additive loading.

## Официальные и полезные источники

- https://docs.unity3d.com/ru/current/ScriptReference/SceneManagement.SceneManager.LoadSceneAsync.html
- https://docs.unity3d.com/ru/current/ScriptReference/SceneManagement.SceneManager.UnloadSceneAsync.html
- https://docs.unity3d.com/ru/2021.1/ScriptReference/SceneManagement.LoadSceneMode.Additive.html
- https://github.com/Mathijs-Bakker/Extenject
