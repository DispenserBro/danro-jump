# Temp Legacy Audit

Этот гайд фиксирует результаты аудита содержимого `Assets/Temp/Legacy` и описывает, что именно сохранено для последующего реиспользования в `Danro Jump`.

## Что сделано

- проанализирована структура `Assets/Temp/Legacy`
- выделены подсистемы, совпадающие с целевой продуктовой рамкой проекта
- все не-`meta` файлы сохранены в tracked-временную папку `Assets/Temp/Legacy`
- `Assets/Temp/Legacy` оставлен внутри `Assets` по проектному решению, но защищён отдельным `.asmdef` с define constraint, чтобы Unity не компилировала legacy-код раньше времени

## Что сохранено

В `Assets/Temp/Legacy` перенесены все не-`meta` файлы legacy-архива, включая:

- `191` C#-скрипт
- `21` `.asset`
- `2` `.prefab`
- `1` `.uxml`
- `3` `.json`
- `3` `.txt`
- `1` `.exe`
- `1` `.md`

Это сделано намеренно: на этапе аудита безопаснее сохранить весь исходный материал, который потенциально может пригодиться, чем потерять рабочие куски старой системы.

## Главные кандидаты на реиспользование

### 1. COM и аппаратная интеграция

Наиболее ценный блок для `Danro Jump`, потому что проект изначально предполагает работу с устройствами через `COM`.

Ключевые файлы и папки:

- `Assets/Temp/Legacy/Scripts/Controls`
- `Assets/Temp/Legacy/Scripts/Controls/Inputs/ComSystem/ComSystemManager.cs`
- `Assets/Temp/Legacy/Scripts/Controls/Inputs/ComSystem/ComHub.cs`
- `Assets/Temp/Legacy/Scripts/Controls/Inputs/ComSystem/ComDeviceClient.cs`
- `Assets/Temp/Legacy/Scripts/Controls/Inputs/ComSystem/PrizeStandController.cs`
- `Assets/Temp/Legacy/Scripts/Controls/ComInputToInputSystemAdapter.cs`
- `Assets/Temp/Legacy/Scripts/Managers/PaymentManager.cs`

Почему это важно:

- логика кредитов и оплаты
- handshake и авторизация устройств
- маршрутизация нескольких устройств по ролям
- интеграция призовой витрины
- адаптация аппаратного ввода к Unity Input System

### 2. Система призов и призовая витрина

Этот блок почти напрямую соответствует будущим требованиям проекта.

Ключевые файлы и папки:

- `Assets/Temp/Legacy/Scripts/Prizes`
- `Assets/Temp/Legacy/Scripts/Managers/PrizeSystemManager.cs`
- `Assets/Temp/Legacy/Scripts/UI/QRCodeUIController.cs`
- `Assets/Temp/Legacy/Scripts/UI/PrizeStandButtonView.cs`
- `Assets/Temp/Legacy/Scripts/UI/PrizeErrorDisplay.cs`
- `Assets/Temp/Legacy/Scripts/UI/Modal/PrizeStandModal.cs`
- `Assets/Temp/Legacy/Scripts/UI/Modal/PrizeStandSettingsModal.cs`
- `Assets/Temp/Legacy/Scripts/UI/Modal/QRCodeModal.cs`
- `Assets/Temp/Legacy/Scripts/UI/Modal/QRCodeSettingsModal.cs`

Почему это важно:

- выдача призов по типам
- связка уровня с призом
- конфигурация витрины
- сценарии показа и выдачи `QR`
- UI для управления призами

### 3. Настройки для обслуживающего персонала

Это второй по ценности блок после `COM`, потому что проекту нужны сервисные настройки стоимости игры, сложности, призов и режима работы.

Ключевые файлы и папки:

- `Assets/Temp/Legacy/Scripts/Settings`
- `Assets/Temp/Legacy/Scripts/Settings/Core/SettingsManager.cs`
- `Assets/Temp/Legacy/Scripts/Settings/Core/SettingsDatabase.cs`
- `Assets/Temp/Legacy/Scripts/Settings/UI/SettingsUIGenerator.cs`
- `Assets/Temp/Legacy/Scripts/Settings/UI/TextInputSettingsBinder.cs`
- `Assets/Temp/Legacy/Scripts/UI/SettingsUI.cs`
- `Assets/Temp/Legacy/Scripts/UI/AdminStatsWindowUIController.cs`
- `Assets/Temp/Legacy/Scripts/UI/StatsWindowUIController.cs`

Почему это важно:

- централизованная база настроек
- сериализация в `json`
- флаг `exposed` для внешнего редактирования
- генерация UI для настроек
- административные и статистические окна

### 4. QR-утилиты и сопутствующие вспомогательные системы

Ключевые файлы:

- `Assets/Temp/Legacy/Scripts/Utilities/QRCodeUtils.cs`
- `Assets/Temp/Legacy/Scripts/Utilities/PhoneNumberUtils.cs`
- `Assets/Temp/Legacy/Scripts/Utilities/CompositeLogger.cs`
- `Assets/Temp/Legacy/Scripts/Utilities/FileLogger.cs`
- `Assets/Temp/Legacy/Scripts/Utilities/LoggerInitializer.cs`

Почему это важно:

- генерация `QR`-текстур и ссылок
- форматирование номера телефона
- файловое логирование для аркадной/терминальной эксплуатации
- база для сервисной диагностики

### 5. Вторичный, но потенциально полезный gameplay-reference

Этот код не является первоочередным для переноса, но его стоит сохранить как референс для будущей вертикальной платформенной части проекта.

Примеры:

- `Assets/Temp/Legacy/Scripts/Behaviour`
- `Assets/Temp/Legacy/Scripts/Entities`
- `Assets/Temp/Legacy/Scripts/Animations`
- `Assets/Temp/Legacy/Scripts/FX`
- `Assets/Temp/Legacy/Scripts/Audio`

Почему это не стоит тащить в runtime сразу:

- код тесно связан со старой игрой и её структурой сцен
- много зависимостей на старые `GameManager`-паттерны
- часть логики лучше перепроектировать под новую архитектуру и новый gameplay loop

## Внешние ресурсы

В `StreamingAssets` есть материалы, связанные с внешним запуском и лицензированием:

- `Assets/Temp/Legacy/StreamingAssets/RRLauncher.exe`
- `Assets/Temp/Legacy/StreamingAssets/RRLauncher.exe.lic.json`
- `Assets/Temp/Legacy/StreamingAssets/request.req.json`
- `Assets/Temp/Legacy/StreamingAssets/response.res.json`
- `Assets/Temp/Legacy/StreamingAssets/README.txt`

Это важно как reference для:

- внешнего launcher flow
- лицензирования
- обмена `json`-данными с внешним ПО
- понимания старого operational pipeline

## Почему архив лежит в Assets/Temp/Legacy

Legacy-материалы сохранены в `Assets/Temp/Legacy`, потому что это временная рабочая зона проекта внутри Unity-иерархии. Чтобы текущая Unity-сборка не получила старые `.cs` как активные runtime-скрипты, в корне архива лежит `DanroJump.LegacyReference.asmdef` с define constraint `DANRO_JUMP_COMPILE_LEGACY_REFERENCE`.

Пока этот define не добавлен в проект, legacy-assembly не должна компилироваться. Это позволяет держать архив внутри `Assets`, но не смешивать его с текущим runtime-кодом.

Прямой импорт сейчас рискован:

- есть жёсткие связи с `RR.Game2D.*` и старой структурой менеджеров
- есть зависимости на `TextMeshPro`, `InputSystem`, `Cinemachine`, `QRCoder`, `System.Drawing`
- есть `DllImport`-зависимость на нативную библиотеку `ComNative`
- внешний `RRLauncher.exe` связан с отдельным licensing flow
- часть `.asset` и `.prefab` рассчитана на старую сценовую иерархию

Итог: материалы сохранены как источник миграции, а не как готовый runtime-модуль.

## Рекомендованный порядок повторного использования

1. Сначала вынести концепции и API из `COM`-системы в новые `Extenject`-сервисы.
2. Затем спроектировать новый `Prize`-контур, опираясь на legacy-модели и UI flow.
3. После этого собрать современную сервисную систему настроек для персонала.
4. Только потом переносить отдельные UI-элементы и gameplay-reference, если они всё ещё нужны.

## Вывод

`Assets/Temp/Legacy` содержал не мусор, а большой объём потенциально ценных наработок под аппаратную интеграцию, призовую систему, `QR`-выдачу и сервисный режим.

Чтобы ничего не потерять, полезный legacy-контент сохранён в `Assets/Temp/Legacy` как временный reference-архив для поэтапной миграции в новый проект. Подключать его к сборке нужно только осознанно и частями.
