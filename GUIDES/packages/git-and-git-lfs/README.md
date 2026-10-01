# Git и Git LFS

## Зачем это нужно

`Git` нужен для контроля версий проекта, а `Git LFS` — для хранения больших бинарных файлов, которые плохо живут в обычном Git.

Для Unity-проекта это особенно важно по двум причинам:

- Unity генерирует много локальных служебных файлов, которые нельзя коммитить в репозиторий
- игровые проекты быстро накапливают тяжелые бинарные ассеты: исходники графики, звук, видео, 3D-модели и DCC-файлы

## Что считать актуальной практикой для Unity-проекта

На момент проверки разумная базовая стратегия такая:

- брать основу `.gitignore` из актуального `github/gitignore/Unity.gitignore`
- хранить в Git только то, что действительно нужно для восстановления проекта
- хранить в обычном Git текстовые Unity-ассеты, которые нормально диффятся
- отдавать в `Git LFS` большие бинарные файлы и исходники из внешних инструментов

Для Unity это означает:

- коммитить `Assets/`, `Packages/` и `ProjectSettings/`
- не коммитить `Library/`, `Temp/`, `Logs/`, `Obj/`, `Build/`, `Builds/`, `UserSettings/` и похожие локальные артефакты
- не отправлять в `Git LFS` подряд все файлы Unity
- не отправлять в `Git LFS` `.meta`, `.unity`, `.prefab`, `.mat`, `.anim`, `.controller`, `.asset`, если они сериализуются в текст и нужны для diff/merge

## Текущее состояние в проекте

На момент проверки в репозитории не найдено:

- корневого `.gitignore`
- корневого `.gitattributes`

Это значит, что гайд нужно воспринимать не как косметическое обновление, а как обязательную базовую настройку проекта.

## Как установить Git

Если Git уже стоит, переходи к следующему разделу. Если нет:

1. Установи `Git for Windows`.
2. Проверь в терминале:

```powershell
git --version
```

## Как инициализировать репозиторий

В корне проекта выполни:

```powershell
git init
```

После этого сразу настрой базовые данные пользователя, если они еще не заданы глобально:

```powershell
git config user.name "Your Name"
git config user.email "you@example.com"
```

## Какой `.gitignore` нужен Unity-проекту

За основу для Unity-проекта стоит брать актуальный шаблон `Unity.gitignore` из репозитория `github/gitignore`.

Минимальный обязательный набор исключений:

```gitignore
/[Ll]ibrary/
/[Tt]emp/
/[Oo]bj/
/[Bb]uild/
/[Bb]uilds/
/[Ll]ogs/
/[Uu]ser[Ss]ettings/
/[Mm]emoryCaptures/
/[Rr]ecordings/
*.log
.vs/
.gradle/
ExportedObj/
*.csproj
*.sln
*.tmp
*.user
*.userprefs
*.pidb
*.pdb
*.mdb
*.opendb
*.VC.db
sysinfo.txt
mono_crash.*
*.apk
*.aab
*.unitypackage
/ServerData
/[Aa]ssets/StreamingAssets/aa*
/[Aa]ssets/AddressableAssetsData/link.xml*
/[Aa]ssets/Addressables_Temp*
/[Aa]ssets/AddressableAssetsData/*/*.bin*
```

Практический смысл такой:

- служебные каталоги Unity и IDE не попадают в репозиторий
- артефакты локальных билдов не засоряют историю
- временные файлы Addressables и generated-файлы тоже не мешают

## Как установить Git LFS

По официальной документации Git LFS устанавливается отдельно от Git, а затем один раз активируется:

```powershell
git lfs install
```

Эту команду обычно достаточно выполнить один раз на учетную запись пользователя.

## Какие файлы Unity-проекта стоит хранить в Git LFS

Главное правило:

- в `Git LFS` уходят большие бинарные исходники и недиффуемые ассеты
- в обычный Git остаются текстовые Unity-ассеты и проектная конфигурация

### Хорошие кандидаты для LFS

Обычно имеет смысл начать с таких форматов:

```powershell
git lfs track "*.psd"
git lfs track "*.psb"
git lfs track "*.ase"
git lfs track "*.aseprite"
git lfs track "*.blend"
git lfs track "*.fbx"
git lfs track "*.obj"
git lfs track "*.wav"
git lfs track "*.mp3"
git lfs track "*.ogg"
git lfs track "*.flac"
git lfs track "*.mp4"
git lfs track "*.mov"
git lfs track "*.avi"
git lfs track "*.tga"
git lfs track "*.exr"
git lfs track "*.tif"
git lfs track "*.tiff"
```

### Что не стоит класть в LFS по умолчанию

Обычно не нужно отправлять в `Git LFS`:

- `*.meta`
- `*.unity`
- `*.prefab`
- `*.mat`
- `*.anim`
- `*.controller`
- `*.asset`
- `*.cs`
- `*.asmdef`
- `*.json`
- `Packages/manifest.json`
- `Packages/packages-lock.json`
- `ProjectSettings/*`

Причина простая:

- эти файлы должны нормально отслеживаться, сравниваться и ревьюиться как текст

## Как настроить Git LFS для этого проекта

После `git lfs install` выбери форматы, которые реально используются в проекте, и включи для них tracking.

После этого обязательно добавь `.gitattributes` в репозиторий:

```powershell
git add .gitattributes
```

Это важный момент по рекомендации GitHub:

- `.gitattributes` нужно коммитить в сам репозиторий
- не стоит полагаться на глобальный `.gitattributes` пользователя

## Что особенно важно для Unity-команды

### 1. Не коммитить `Library`

`Library/` занимает много места, зависит от локальной машины и пересобирается автоматически. Для Git это мусор.

### 2. Не отправлять в LFS весь `Assets/`

Это ухудшит diff, ревью и работу с merge. В LFS должны идти только крупные бинарные форматы.

### 3. Не трекать текстовые Unity-ассеты через LFS

Если проект использует нормальную текстовую сериализацию Unity, текстовые ассеты полезно хранить именно в обычном Git.

### 4. Настроить LFS до первого большого коммита

Если бинарники уже попали в обычный Git, потом их придется отдельно переносить в LFS.

### 5. Держать Unity в режиме `Visible Meta Files` и `Force Text`

Для нормальной работы Git в Unity обычно нужна и базовая настройка редактора:

- `Version Control Mode`: `Visible Meta Files`
- `Asset Serialization Mode`: `Force Text`

Это дает два эффекта:

- `.meta` файлы становятся предсказуемой частью проекта и не теряются между машинами
- сцены, префабы, материалы и другие текстовые Unity-ассеты лучше подходят для diff и merge в обычном Git

## Если большие файлы уже попали в обычный Git

Официальная рекомендация GitHub такая:

1. Сначала настроить `git lfs track`.
2. Затем убрать файл из обычного Git-индекса.
3. После этого добавить его заново уже как LFS-объект.

Если большие бинарники уже лежат глубоко в истории, может понадобиться отдельная миграция истории через `git lfs migrate`.

Это уже не базовая настройка, а отдельная операция по очистке репозитория.

## Минимальная практическая настройка для проекта

Для первого этапа достаточно:

- инициализировать Git
- создать корневой `.gitignore` на основе актуального `Unity.gitignore`
- выполнить `git lfs install`
- создать корневой `.gitattributes`
- включить LFS только для действительно тяжелых бинарных форматов
- сделать первый коммит базового состояния проекта

## Как проверить, что всё настроено

- `git status` работает в корне проекта
- в корне проекта есть `.gitignore`
- в корне проекта есть `.gitattributes`
- `git lfs install` выполнен без ошибок
- `git lfs track` создал или обновил `.gitattributes`
- файлы вроде `*.psd`, `*.fbx`, `*.wav` попадают в LFS, если для них включён tracking
- `Library/`, `Temp/`, `Logs/`, `Builds/` и `UserSettings/` не показываются в `git status`

## Официальные источники

- https://github.com/github/gitignore/blob/main/Unity.gitignore
- https://git-lfs.com/
- https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-git-large-file-storage
- https://docs.github.com/articles/configuring-git-large-file-storage?platform=windows
- https://docs.github.com/en/repositories/working-with-files/managing-large-files/moving-a-file-in-your-repository-to-git-large-file-storage
- https://learn.unity.com/tutorial/working-with-unity-and-github
- https://learn.unity.com/course/collaborate-with-github-desktop/tutorial/set-up-git-lfs-to-manage-large-files-in-unity?version=6.2
