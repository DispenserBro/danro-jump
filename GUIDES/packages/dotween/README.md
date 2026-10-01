# DOTween

## Зачем это нужно

`DOTween` нужен для tween-анимаций объектов, UI и значений без написания собственных систем интерполяции с нуля.

## Сравнение официального источника и NuGet

На момент проверки расхождение между каналами очень большое:

- на `nuget.org` пакеты `DoTween`, `DoTween_Unity` и `DoTween_Unity_Editor` имеют версию `1.0.0` и дату обновления `2020-04-07`
- на официальном сайте DOTween в update notes уже фигурирует `v1.2.825`
- в официальном support-разделе отдельно указано, что special upgrade path нужен для free-версий старее `1.2.815`

Вывод для этого проекта однозначный:

- `NuGet for Unity` для `DOTween` использовать не нужно
- NuGet-пакеты сильно устарели относительно официального дистрибутива
- основной канал должен быть только официальный сайт DOTween или Asset Store для Pro-версии

## Как установить

Официальный способ:

1. Скачай актуальный `DOTween` из официального источника.
2. Распакуй содержимое в папку `Assets/` проекта.

По официальной инструкции пакет не нужно класть внутрь `Editor` или `Resources`.

## Как настроить после установки

После импорта пакет нужно обязательно прогнать через setup:

1. Открой `Tools > Demigiant > DOTween Utility Panel`.
2. Нажми `Setup DOTween...`.

Этот шаг нужен для переимпорта дополнительных библиотек и включения модулей под текущий проект.

## Частая ошибка: `DOTweenModuleEPOOutline.cs`

Если после импорта появляются ошибки вида:

```text
Assets/.../DOTween/Modules/DOTweenModuleEPOOutline.cs(5,7): error CS0246: The type or namespace name 'EPOOutline' could not be found
Assets/.../DOTween/Modules/DOTweenModuleEPOOutline.cs(...): error CS0246: The type or namespace name 'SerializedPass' could not be found
```

это означает, что в проект попал optional-модуль интеграции с `Easy Performant Outline`, но самого пакета `EPO Outline` в проекте нет.

Что важно для текущего проекта:

- в `Assets/Resources/DOTweenSettings.asset` модуль `epoOutlineEnabled` должен быть выключен
- если ошибка всё равно появляется, значит модульный файл не был корректно переписан после setup или у проекта остался активный define для `EPO`

### Как исправить

Если `EPO Outline` тебе не нужен:

1. Открой `Tools > Demigiant > DOTween Utility Panel`.
2. Убедись, что интеграция `EPO Outline` выключена.
3. Повтори `Setup DOTween...`.
4. Если ошибки не исчезли, закрой Unity и открой проект снова.
5. Если после повторного setup файл всё равно компилируется, временно удали или перемести из проекта файл:

```text
Assets/External/Packages/DOTween/Modules/DOTweenModuleEPOOutline.cs
```

Это безопасно, если пакет `Easy Performant Outline` в проекте не используется.

Если `EPO Outline` действительно нужен:

1. Сначала установи сам пакет `Easy Performant Outline`.
2. Только после этого включай соответствующий модуль в `DOTween Utility Panel`.
3. Повтори `Setup DOTween...`.

### Практическое правило

Для всех optional-модулей `DOTween` действует одно и то же правило:

- сначала ставится внешний пакет-зависимость
- потом включается интеграционный модуль в `DOTween`

Не наоборот.

## Что важно для этого проекта

С учетом наличия `NuGet for Unity` в проекте не нужно пытаться унифицировать `DOTween` через NuGet.

Причины:

- официальный Unity-сценарий у `DOTween` идет через импорт ассетов
- пакет содержит editor tooling и setup pipeline
- NuGet-канал для него устарел и не отражает актуальную версию

Если позже в проекте появится `UniTask`, для интеграции с ним может понадобиться define:

```text
UNITASK_DOTWEEN_SUPPORT
```

Но добавлять его нужно только после реальной установки `DOTween`.

## Минимальная практическая настройка для проекта

Для текущего проекта на первом этапе достаточно:

- установить актуальный `DOTween` из официального источника
- выполнить `Setup DOTween`
- проверить, что проект компилируется

Если в проекте уже используется `UGUI`, имеет смысл убедиться, что нужный модуль UI активирован.

## Как проверить, что всё настроено

- в Unity есть меню `Tools > Demigiant`
- `DOTween Utility Panel` открывается
- `Setup DOTween...` проходит без ошибок
- после добавления `using DG.Tweening;` в скрипт проект не ругается на отсутствие namespace

## Официальные источники

- https://dotween.demigiant.com/getstarted.php
- https://dotween.demigiant.com/support.php
- https://dotween.demigiant.com/download.php
- https://www.nuget.org/packages/DoTween
