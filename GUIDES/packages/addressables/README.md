# Addressables

## Зачем это нужно

`Addressables` нужен для управляемой загрузки ассетов по адресам, разделения контента на группы и подготовки ассетов к локальной или удаленной доставке.

## Как установить

1. Открой `Window > Package Manager`.
2. Переключи список на `Unity Registry`.
3. Найди `Addressables`.
4. Нажми `Install`.

Если будешь добавлять пакет вручную по имени, используй:

```text
com.unity.addressables
```

## Как настроить после установки

После установки package сам по себе еще не активирован для проекта. Нужно создать настройки:

1. Открой `Window > Asset Management > Addressables > Groups`.
2. Нажми `Create Addressables Settings`.
3. Проверь, что в проекте появилась папка:

```text
Assets/AddressableAssetsData
```

Эту папку нужно хранить в репозитории, потому что в ней лежат настройки Addressables.

## Минимальная практическая настройка для проекта

Для первого этапа достаточно следующего:

1. Создать `Addressables Settings`.
2. Создать хотя бы одну тестовую группу.
3. Пометить один тестовый prefab или asset как `Addressable`.
4. Выполнить тестовую сборку контента:
   - `Window > Asset Management > Addressables > Groups`
   - `Build > New Build > Default Build Script`

Пока не нужно:

- строить сложную remote-схему доставки
- разносить весь проект по addressable-группам
- внедрять Addressables во все системы сразу

## Как проверить, что всё настроено

- есть окно `Addressables Groups`
- создана папка `AddressableAssetsData`
- тестовый asset можно пометить как `Addressable`
- `Default Build Script` проходит без ошибок

## Официальные источники

- https://docs.unity.cn/Packages/com.unity.addressables%401.21/manual/installation-guide.html
- https://docs.unity.cn/Manual/com.unity.addressables.html
