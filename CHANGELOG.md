## v0.6.0 Candidate Fix 4 (not released)

- Added fallback browser launch if Windows HTTPS association fails; first try default URL handler, then validated installed browser executable.
- Preserve update-check status on browser errors, show launch diagnostic separately.
- Link always targets official GitHub releases/latest. No registry changes.

## 0.6.0 - Stability, Performance & GitHub Integration (candidate, Windows QA pending)

- Fixed dynamic progress text layout constraints.
- Reduced UI progress refresh frequency during scanning.
- Added GitHub Release version checks to About and settings switches.
- Added guarded local Git push and release scripts.
- Fixed stale version in portable START HERE.txt and installer wait counter expansion.
- Updated download URL.

# 0.5.2 — Analyzer Filter & Icon Polish

- Убрана подпись «Анализ диска C: и безопасная очистка» с главного экрана.
- В Storage Analyzer убраны поле поиска по имени/пути и UI-переключатель Top 50/100.
- Фильтр категорий переименован в «Тип файлов» и теперь содержит: Все типы, Документы, Видео, Изображения, Музыка, Архивы, Образы дисков, Исполняемые файлы, Прочее.
- Фильтры размера и возраста, а также сортировка по размеру/дате/имени сохранены.
- Внутренний лимит Analyzer зафиксирован на 50 отображаемых результатов; движок сканирования и read-only поведение не менялись.
- Категоризация файлов расширена для документов, изображений и музыки; старый сохранённый Analyzer-кэш нормализуется по расширениям при загрузке.
- Обновлён логотип Safe C: Cleaner: крупный `C:`, компактный силуэт, розовая кисть и полностью прозрачный фон; пересобраны PNG 16–512 px и multi-resolution `SafeCCleaner.ico`.
- Версия приложения обновлена до 0.5.2. AppId установщика сохранён, поэтому Setup обновляет существующую установку поверх предыдущей версии.
- Cleanup rules, PathGuard, whitelist, Elevated Helper, Developer Storage, tray, single-instance и история очисток не изменялись.

# 0.5.1 — Compact Quick Clean UI

- Quick Clean переведён с высоких карточек на компактные строки высотой около 52 px.
- Постоянные предупреждения убраны из второй строки: они доступны через значок предупреждения и в подробном info-flyout.
- Размер, количество файлов и действие «Очистить» собраны в одну компактную строку.
- Пустые категории нельзя запускать отдельной кнопкой очистки.
- Добавлена закреплённая нижняя summary-панель: число выбранных категорий, суммарный найденный объём и «Очистить выбранное».
- Summary автоматически обновляется при выборе категорий, смене фильтра и повторном сканировании.
- Cleanup engine, PathGuard, whitelist и elevated-helper не изменялись.

# 0.5.0 — UX, Persistence & Single Instance

- Added a compact information-button pattern (`ⓘ`) beside major section titles and Quick Clean categories; explanatory copy is moved into hover tooltips / click flyouts to reduce persistent text on the main UI.
- Quick Clean category descriptions are now available from the per-category info button; warnings remain visible.
- Added a true single-instance guard: launching Safe C: Cleaner again signals the already-running instance to show/activate instead of creating another window or tray icon. Background autostart launches do not force an existing window to the foreground.
- Storage Intelligence restores the latest persisted disk snapshot on startup before any optional rescan.
- Storage Analyzer persists the last successful large-file/folder result under LocalAppData, restores it on the next launch, shows the capture time, marks data older than 24 hours as potentially stale, and changes the action to `Пересканировать C:` when cached data exists.
- Cleanup history is physically capped at the latest 10 operations in SQLite; older run rows and their item rows are removed automatically. TXT/JSON exports therefore cover the same bounded history.
- Reworked the Safety page around actual controls: PathGuard, elevated-helper allowlists, protected roots/subtrees, UAC-only operations, and intentionally excluded features.
- Application version updated to 0.5.0. Cleanup-rule behavior, PathGuard rules and elevated-helper allowlists were not broadened.

# 0.4.1.3 — Branding Patch

- Added the final Safe C: Cleaner app icon based on the C: drive + cleaning brush mark.
- Rebuilt `SafeCCleaner.ico` as a multi-resolution Windows icon (16–256 px).
- The icon is used by the EXE, taskbar, Alt+Tab, window chrome and system tray.
- Added transparent PNG logo assets for the application UI and future packaging.
- The About page now shows the product logo instead of the generic glyph.
- No cleanup, analyzer, safety or elevated-helper logic changed.

## 0.4.1.2 — Storage Analyzer compile hotfix 2

- Убраны char-литералы обратного слэша из StorageAnalyzerService.
- TrimEnd теперь использует Path.DirectorySeparatorChar и Path.AltDirectorySeparatorChar.
- Это исключает повторение ошибки парсинга CS1012/CS1010 на строках 97/156/213/223.
- Остальная логика 0.4.1 не изменена.

# Safe C: Cleaner — CHANGELOG

## 0.4.1.1 — Storage Analyzer compile hotfix

- Исправлены четыре некорректных C# `char`-литерала с обратным слэшем в `StorageAnalyzerService.cs`.
- `TrimEnd('\\', '/')` теперь корректно компилируется на .NET 10 / C# для Windows.
- Исправление не меняет алгоритм Storage Analyzer, cleanup rules, PathGuard, Elevated Helper или Developer Storage.
- Версия приложения обновлена до 0.4.1.1.

## 0.4.1 — Storage Analyzer + 0.4.0.2 UI Fix

- Добавлен отдельный read-only раздел Storage Analyzer для диска C:.
- Добавлены Top 50/100 крупных файлов и папок, поиск, фильтры размера и возраста, категории файлов и сортировка.
- Добавлены категории: disk images, archives, installers, video, backups и other.
- Добавлены отмена анализа, приблизительный progress/ETA и учёт ограниченных по доступу участков.
- Analyzer использует общие исключения Storage Scanner и пропускает reparse points/junctions.
- Файлы можно выделить в Explorer; папки можно открыть либо передать в существующий Storage Intelligence drill-down с breadcrumbs.
- В Analyzer нет действий удаления.
- UI Fix 0.4.0.2 скрывает scrollbar внутренней панели NavigationView у Settings/About, не отключая рабочие ScrollViewer/ListView.
- Версия приложения обновлена до 0.4.1. Автор: Cactus Dex.

## 0.4.0.1 — Stabilization Patch + Mini Patch 3

- Исправлена геометрия страниц «О программе», «Настройки» и Developer Storage при свёрнутой NavigationView: горизонтальная прокрутка отключена, содержимое центрируется в текущей рабочей области.
- Скрыт лишний scrollbar во внутренней панели навигации у нижних пунктов «Настройки»/«О программе».
- Нижняя панель сканирования переразмечена: progress bar стал адаптивным, под ETA выделено 132 px, поэтому строки вида `~1 мин 40 сек` больше не обрезаются слева.
- ETA начинает вычисляться после 5% прогресса и 3 секунд работы, чтобы уменьшить ранние скачки оценки.
- Developer Storage теперь ищет Docker, WSL, npm и dotnet не только через PATH, но и в стандартных Windows-каталогах.
- Docker: сырая ошибка `npipe/dockerDesktopLinuxEngine` заменена на понятный статус о не запущенном Docker Desktop/движке.
- WSL: пользовательские дистрибутивы отделены от служебных `docker-desktop*`; показывается размер доступных VHDX и количество дистрибутивов без найденного VHDX.
- npm/NuGet: если CLI отсутствует, приложение всё равно пытается обнаружить стандартные cache-каталоги read-only и показать их размер.
- Версия приложения обновлена до 0.4.0.1. Автор: Cactus Dex.

# CHANGELOG

## 0.4.0 — Developer Storage + Windows Integration

- Added Developer Storage section for Docker, WSL, npm, NuGet and Android SDK.
- Added Docker analysis through `docker system df`. Cleanup is limited to build cache, dangling images and stopped containers; volumes are explicitly excluded.
- Added read-only WSL distribution/VHDX analysis through the WSL registry and `wsl.exe`.
- Added npm cache measurement and official CLI cleanup.
- Added NuGet local-cache measurement and official .NET CLI cleanup.
- Added Android SDK component storage analysis plus explicit selected-package removal through the official `sdkmanager`; no automatic SDK deletion.
- Added per-user Windows autostart with background launch.
- Added native system tray icon with Open / Scan / Exit.
- Added configurable close-to-tray behavior.
- Registry Cleaner and defragmentation remain excluded.
- Author identity remains `Cactus Dex`.


## 0.3.3 — System Cleanup Expansion

- Added `windows-update-cleanup` to the elevated-helper whitelist.
- Added Windows Update Cleanup via the Windows Disk Cleanup `Update Cleanup` category.
- Uses an isolated `StateFlags9133` profile and restores every pre-existing flag after execution, preventing unrelated cleanmgr categories from running.
- Added SAFE user-scoped NVIDIA/AMD GPU shader-cache cleanup; system-wide driver stores are intentionally excluded.
- Kept DirectX shader cache, thumbnail cache, WER, app crash dumps, BSOD dumps and Delivery Optimization as separate auditable categories.
- Kept Component Store cleanup on DISM `/StartComponentCleanup`; `/ResetBase` remains prohibited.
- Corrected project/author identity to `Cactus Dex`.
- No Docker/WSL/npm/NuGet/Android cleanup, Registry Cleaner, duplicate removal, defragmentation, RAM optimization or Storage Scanner auto-delete.

## 0.3.2 — Usability & Settings

- Added persistent application settings (`settings.json`).
- Added startup scan toggle and configurable Storage Scanner cache TTL.
- Added Storage Scanner exclusion management UI.
- Persisted window size/position, navigation pane state and last open section.
- Added off-screen window-position protection when monitors change.
- Added quick navigation from Overview to Storage Intelligence / Deep Clean.
- Added last scan time, duration and result summary to Overview.
- Expanded About with .NET / Windows App SDK details and diagnostic actions.
- Changed author identity to `Cactus Dex`; real name is not stored in UI/project metadata.
- No new cleanup modules; cleanup safety rules and elevated allowlists unchanged.

# Changelog

## 0.3.1.2 — Mini Patch 2

- Replaced post-resize minimum-size correction with native `WM_GETMINMAXINFO` handling so the window simply stops at 1240×850 instead of shifting when dragged smaller.
- Scan and rescan buttons are disabled while a scan is active and show `Сканирование…`; they become available again after completion or cancellation.
- Bottom scan bar keeps the existing cancel action and progress/ETA behavior.
- Added `О программе` to the NavigationView footer with app version, Windows/device information, architecture/CPU summary and author.
- Cleanup rules, PathGuard, SafeDeletion and elevated helper are unchanged.

## 0.3.1 — Scanner Polish & Storage UX+

- Faster bounded-parallel storage drill-down scanning.
- Precise drill-down progress and access-limited status.
- Search, size filters and result sorting.
- Breadcrumb navigation and Back.
- 15-minute cache with forced refresh.
- Persistent scanner exclusions.
- File/folder counts and Explorer open action.
- Compact Storage Intelligence presentation.
- Snapshot comparison now selects a snapshot nearest to the 1/7/30 day target within a tolerance window.
- Cleanup rules, PathGuard, SafeDeletion and elevated helper unchanged.


## 0.3.1-mini — UI Polish Shell
- Dark system title bar now matches the application's graphite shell.
- NavigationView now has persistent icons: expanded pane shows icon + full label, compact pane shows icons only.
- Startup/minimum window size set to approximately the tested 1920×1080 screenshot footprint: 1240×850.
- Bottom scan status bar now shows determinate progress, approximate ETA and cancel action during a scan.
- Scan ETA automatically switches between seconds and minutes.
- Reduced nonessential explanatory copy across Overview, Quick Clean, Deep Clean, Storage, History and Safety screens.
- Kept cleanup rules, PathGuard, elevated helper and all deletion behavior unchanged.

## 0.3.0 — Storage Intelligence
- Added read-only Storage Intelligence summary explaining disk usage changes against a historical baseline.
- Added positive-growth ranking for tracked storage zones.
- Baseline prefers a snapshot at least 7 days old; with a short history it falls back to the previous snapshot and says so explicitly.
- Added on-demand folder drill-down sorted by measured size, with manual deeper navigation.
- Added cancellation for storage/drill-down scans.
- Drill-down skips reparse points/junctions and caches results for 15 minutes.
- Storage Intelligence does not delete discovered files or directories.
- Cleanup rules, PathGuard, elevated-helper whitelist and Deep Clean operations remain unchanged from 0.2.3.
- Explicitly deferred Docker/WSL/npm/NuGet/Android cleanup, Registry Cleaner, duplicate search, defragmentation, RAM optimization and automatic deletion from Storage Scanner.

## 0.2.3 — Storage UX
- Reworked “Диск и рост” into a storage dashboard with used/free/total cards and a rescan action.
- Added disk-growth trends for 1, 7 and 30 days using persisted SQLite snapshots.
- Each trend identifies the largest growing tracked category when historical data is available.
- Added guarded “Открыть” actions only for read-only user/developer locations; system roots remain disabled.
- Added per-run cleanup details dialog with category-level results.
- Added JSON cleanup-history export alongside TXT.
- Cleanup rules, PathGuard, elevated-helper whitelist and Deep Clean operations are unchanged from 0.2.2.

## 0.2.2 — Cleanup UX
- Added persistent cleanup history in SQLite with per-category results.
- Added signed free-space before/after reporting and a detailed completion dialog.
- Added Quick Clean filters: All / SAFE / ADVANCED.
- `Очистить выбранное` now respects the active filter so hidden categories cannot be executed accidentally.
- Added cleanup-history page with the latest 100 runs.
- Added local TXT export to Documents/SafeCCleaner Reports.
- Deep/Advanced system operations are also stored in history.
- Existing 0.2.1 databases migrate in place with CREATE TABLE IF NOT EXISTS.

## 0.2.1

- Отчёт последней очистки: фактическое свободное место до → после.
- DISM AnalyzeComponentStore теперь показывает структурированную сводку вместо сырого вывода.
- Добавлены системные BSOD-дампы (`MEMORY.DMP` и `%SystemRoot%\Minidump`) как ADVANCED-категория с UAC и подтверждением.
- SAFE Temp ужесточён: User Temp и Windows Temp учитывают только файлы старше 7 дней.
- Расширены предупреждения для диагностических данных.
- Сохранён whitelist elevated helper и запрет произвольных путей/команд.

## 0.2.0

- Новый NavigationView/Fluent интерфейс.
- Dashboard с SAFE reclaimable space и причиной роста C:.
- Добавлены Thumbnail Cache и Windows Error Reporting.
- Добавлен Deep Clean foundation.
- DISM AnalyzeComponentStore / StartComponentCleanup без ResetBase.
- cleanmgr /AUTOCLEAN для post-upgrade leftovers / Windows.old.
- Delivery Optimization cleanup через штатный PowerShell cmdlet.
- Управляемое отключение/включение гибернации через powercfg.
- Elevated helper расширен whitelist системных операций.
- Сохранён workaround для `.pri` в unpackaged WinUI publish.

## 0.1.3

- Workaround отсутствующего `SafeCCleaner.pri` в portable publish.

## 0.1.2

- Исправления XAML parser и startup diagnostics.

## 0.6.0 Candidate - Embedded Update Manager

- Removed browser-dependent GitHub release action and obsolete BrowserLauncher with CS1626 compile error.
- Added in-app installer download from validated latest stable GitHub Release, cancellation/progress, exact-size and SHA-256 integrity check, and safe Downloads output.
- Download is only enabled for releases newer than the installed app. No automatic installer execution pending trusted code-signature verification.
