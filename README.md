


# Safe C: Cleaner 0.5.2

Windows-утилита для анализа системного диска, безопасной очистки временных данных и контролируемых системных/Developer Storage операций.
[**⬇ Скачать Safe C: Cleaner - последняя версия**](https://github.com/haknikpik-svg/Safe-C-Cleaner/releases/latest)

## Что нового в 0.5.2

- С главного экрана убрана лишняя подпись «Анализ диска C: и безопасная очистка».
- В Storage Analyzer убраны поиск по имени/пути и пользовательский переключатель Top 50/100.
- Фильтр категорий переименован в **«Тип файлов»** и собран в один dropdown: документы, видео, изображения, музыка, архивы, образы дисков, исполняемые файлы и прочее.
- Размер, возраст и сортировка Analyzer сохранены без изменений; внутренний лимит выдачи остаётся 50 результатов.
- Старый сохранённый Analyzer-кэш автоматически получает новые категории по расширению файла, поэтому обновление не требует обязательного повторного сканирования.
- Обновлён логотип: более крупный `C:`, компактный силуэт и розовая кисть; пересобраны PNG-ассеты и multi-resolution ICO для маленьких Windows-иконок.
- Cleanup engine, PathGuard, whitelist, Elevated Helper и существующие функции очистки не изменялись.

## 0.5.1 — Compact Quick Clean UI

- Быстрая очистка стала компактнее: низкие строки вместо высоких карточек.
- Подробности и предупреждения доступны через info/warning affordances, без постоянной второй строки текста.
- Внизу Quick Clean закреплена сводка выбранных категорий и объёма.
- Кнопки очистки пустых категорий автоматически отключены.
- Функциональность 0.5.0 — single-instance, сохранённый анализ, история до 10 записей и переработанная безопасность — сохранена.

## Previous major release: 0.5.0

## 0.4.1 — Storage Analyzer

- Новый read-only раздел **Storage Analyzer** для диска C:.
- Top 50 / Top 100 самых крупных файлов и папок.
- Поиск по имени и полному пути.
- Фильтры размера: 500 MB / 1 GB / 5 GB / 10 GB.
- Категории крупных файлов: образы дисков, архивы, установщики, видео, резервные копии и прочее.
- Фильтр старых файлов: более 180 дней / более 1 года.
- Сортировка по размеру, дате и имени.
- Отмена анализа, приблизительный прогресс и ETA.
- Reparse points и исключения Storage Scanner пропускаются.
- Файл можно выделить в Проводнике, папку — открыть или передать в существующий Storage Intelligence drill-down с breadcrumbs.
- Analyzer ничего не удаляет.

## 0.4.0.2 — UI Fix

- Исправлено отображение лишней вертикальной полосы прокрутки во внутренней панели NavigationView у нижних пунктов «Настройки» / «О программе».
- Прокрутка рабочих страниц и списков не отключается.


## 0.4.0 — Developer Storage + Windows Integration

- Added a dedicated **Developer Storage** section for Docker, WSL, npm, NuGet and Android SDK.
- Docker analysis uses the Docker CLI; safe cleanup actions are limited to build cache, dangling images and stopped containers. **Docker volumes are never pruned.**
- WSL is analysis-only in 0.4.0: registered distributions and their `ext4.vhdx` sizes are shown; distributions and Linux data are not removed.
- npm cache is measured using the configured npm cache path and cleaned only through `npm cache clean --force`.
- NuGet locations are discovered through `dotnet nuget locals all --list` and cleared through `dotnet nuget locals all --clear`.
- Android SDK is detected from `ANDROID_SDK_ROOT`, `ANDROID_HOME` or the default LocalAppData path; system-images/platforms/build-tools/emulator storage is analyzed and an explicitly selected installed package can be removed through the official `sdkmanager --uninstall`.
- Added **Start with Windows** through the current-user Run key. Autostart uses `--background` and opens directly into the system tray.
- Added a native Windows **system tray icon** with Open / Scan / Exit actions.
- Closing the window can hide it to the tray instead of terminating the process; this is configurable in Settings.
- Registry Cleaner and defragmentation remain intentionally excluded.
- Project author identity: **Cactus Dex**.

Safe C: Cleaner — Windows-утилита для анализа диска C:, безопасной очистки временных данных и контролируемого Deep Clean.


## Mini Patch 2 — 0.3.1.2

- минимальный размер окна теперь ограничивается на уровне Win32 и не вызывает смещение окна при попытке уменьшить его ниже 1240×850;
- `Сканировать` / `Пересканировать` блокируются на время сканирования и показывают состояние `Сканирование…`;
- после `Отмена` запуск снова доступен, когда текущая операция реально завершила отмену;
- внизу навигации добавлена страница `О программе`: версия, устройство, Windows/архитектура, CPU и автор.


## Что изменилось в 0.3.1 — Scanner Polish & Storage UX+

- ускорен Storage Scanner: верхнеуровневые каталоги анализируются с ограниченным параллелизмом;
- точный прогресс drill-down по количеству элементов верхнего уровня;
- поиск по имени и пути;
- фильтры по размеру: 100 MB / 1 GB / 5 GB;
- сортировка по размеру, имени, числу файлов и режим «сначала папки»;
- breadcrumbs и кнопка «Назад»;
- 15-минутный кэш с ручным «Обновить»;
- persistent-исключения Storage Scanner в `%LOCALAPPDATA%\SafeCCleaner\storage-exclusions.json`;
- безопасная обработка Access Denied и reparse points;
- отображение количества файлов и папок;
- открытие найденной папки в Проводнике;
- более компактный Storage Intelligence UI;
- улучшено сравнение snapshot: 1/7/30 дней выбираются по ближайшему снимку к целевой дате в допустимом окне.

## Что изменилось в 0.3.1-mini

- тёмный Windows title bar в цвет приложения;
- NavigationView: иконки всегда видны, подписи скрываются в compact-режиме;
- стартовый/минимальный размер окна 1240×850;
- общий прогресс сканирования + приблизительный ETA + отмена в нижней панели;
- сокращены вторичные пояснения в интерфейсе без удаления полезных данных.


## Что есть в 0.3.0

- Fluent/WinUI 3 интерфейс и Mica fallback.
- Dashboard: занято/свободно, SAFE reclaimable, причина роста каталогов.
- История снимков диска через SQLite.
- Отчёт последней очистки с фактическим свободным местом `до → после`.
- SAFE Quick Clean:
  - User Temp старше 7 дней;
  - Windows Temp старше 7 дней через UAC;
  - DirectX Shader Cache;
  - Thumbnail Cache;
  - Chrome / Edge / Firefox cache;
  - пользовательский и системный Windows Error Reporting.
- ADVANCED:
  - локальные crash dumps приложений;
  - системные `MEMORY.DMP` и `%SystemRoot%\Minidump` после BSOD;
  - корзина.
- Deep Clean:
  - `DISM /AnalyzeComponentStore` с понятной сводкой;
  - `DISM /StartComponentCleanup` без `/ResetBase`;
  - post-upgrade leftovers / Windows.old через штатный Disk Cleanup;
  - Delivery Optimization через штатный PowerShell-модуль;
  - управляемое отключение/включение гибернации через `powercfg`.


## Storage Intelligence 0.3.0

- Read-only анализ причин изменения занятого места на C:.
- Baseline для объяснения роста: 7 дней, а при короткой истории — предыдущий снимок.
- Список до 8 наиболее выросших отслеживаемых зон.
- Drill-down по каталогу: верхний уровень сортируется по фактическому размеру; можно уходить глубже вручную.
- Drill-down имеет отмену, игнорирует reparse points/junctions и кэширует результат на 15 минут.
- Экран анализа не содержит действий удаления: найденные каталоги и файлы только показываются.
- Существующие Cleanup Engine, PathGuard и elevated-helper whitelist не расширялись.


## Сборка

На Windows распакуйте проект в отдельную папку и запустите:

```text
BUILD-WINDOWS.cmd
```

Скрипт использует x64 .NET SDK из `C:\Program Files\dotnet` даже если x86 host стоит раньше в PATH, очищает старый XAML build output и проверяет наличие `SafeCCleaner.pri` перед READY.

Готовая portable-папка:

```text
artifacts\SafeCCleaner-win-x64-portable\
```

Запуск:

```text
SafeCCleaner.exe
```

## Принцип безопасности

Quick Clean не должен менять пользовательские документы или критическое состояние Windows. Deep/Advanced операции выполняются отдельно и объясняют последствия. Elevated helper использует whitelist ID операций и не принимает произвольный путь или произвольную shell-команду.

См. также `SECURITY.md`, `CHANGELOG.md`, `ROADMAP.md`.

## Storage UX 0.2.3

- Сводка занято / свободно / всего на странице диска.
- Тренды использования за 1 / 7 / 30 дней по SQLite-снимкам.
- Подробности каждой записи истории прямо в интерфейсе.
- Экспорт истории в TXT и JSON.
- Безопасное открытие только read-only пользовательских/developer-каталогов.

## 0.2.2 Cleanup UX

The app now stores cleanup runs in the same local SQLite database used for disk snapshots. Each run records free space before/after, reported bytes removed, skipped objects, mode, success state, and per-category results.

Quick Clean can be filtered by **All / SAFE / ADVANCED**. The active filter also scopes `Очистить выбранное`.

After a cleanup a result dialog shows the observed disk-space change and every category result. The **История очисток** page keeps recent runs and can export a local UTF-8 TXT report to `Documents\SafeCCleaner Reports`.

## 0.4.0.1 Stabilization Patch

Патч стабилизирует оболочку и Developer Storage: исправлена геометрия About/Settings при компактной навигации, нижний progress/ETA, поиск CLI-инструментов по стандартным Windows-путям и человекочитаемые состояния Docker/WSL/npm/NuGet.
