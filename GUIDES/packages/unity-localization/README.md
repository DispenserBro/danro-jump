# Unity Localization

## Зачем это нужно

`Unity Localization` нужен для локализации строк, ассетов и выбора языков проекта.

## Как установить

1. Открой `Window > Package Manager`.
2. Переключи список на `Unity Registry`.
3. Найди `Localization`.
4. Нажми `Install`.

Если будешь добавлять пакет вручную по имени, используй:

```text
com.unity.localization
```

## Что важно перед настройкой

`Localization` тесно завязан на `Addressables`, поэтому сначала лучше установить и инициализировать `Addressables`.

## Как настроить после установки

### 1. Создать Localization Settings

1. Открой `Edit > Project Settings > Localization`.
2. Нажми `Create`.

### 2. Создать локали

1. В том же окне нажми `Locale Generator`.
2. Добавь хотя бы базовый набор локалей для проекта.

Для старта проекта обычно достаточно:

- `Russian (ru)`
- `English (en)`

### 3. Назначить locale по умолчанию

После создания локалей укажи, какая локаль должна использоваться по умолчанию.

### 4. Создать таблицы локализации

1. Открой `Window > Asset Management > Localization Tables`.
2. Создай хотя бы одну `String Table Collection`.
3. Добавь в нее стартовые UI-строки.

## Минимальная практическая настройка для проекта

Для первого этапа достаточно:

- создать `Localization Settings`
- добавить `ru` и `en`
- завести одну строковую таблицу
- локализовать один тестовый UI-текст

Если локализуешь UI-компонент, Unity позволяет повесить на него локализацию через `Localize`.

## Как проверить, что всё настроено

- в `Project Settings` создан `Localization Settings` asset
- есть минимум две локали
- создана хотя бы одна `String Table Collection`
- тестовый UI-текст меняется при смене локали

## Официальные источники

- https://docs.unity.cn/Packages/com.unity.localization%401.4/manual/QuickStartGuide.html
- https://docs.unity.cn/Manual/com.unity.localization.html
