# Security model

## SAFE

SAFE предназначен только для восстанавливаемых кэшей и временных данных.

User Temp и Windows Temp в 0.2.2 обрабатывают только файлы, не изменявшиеся более 7 дней. Занятые/недоступные файлы пропускаются. Reparse points и junction не обходятся.

## ADVANCED

ADVANCED всегда требует явного подтверждения, если данные могут быть полезны для восстановления или диагностики.

Сюда относятся:

- Recycle Bin;
- crash dumps приложений;
- системные `MEMORY.DMP` и `%SystemRoot%\Minidump` после BSOD;
- изменение состояния гибернации.

## DEEP

Системное обслуживание выполняется штатными механизмами Windows:

- DISM AnalyzeComponentStore;
- DISM StartComponentCleanup;
- Disk Cleanup / AUTOCLEAN;
- Delivery Optimization PowerShell module;
- powercfg.

`DISM /ResetBase` не используется.

## Elevated helper

`SafeCCleaner.Elevated.exe` принимает только заранее разрешённые ID операций. UI не может передать helper произвольный путь, имя файла или произвольную командную строку.

Разрешённые cleanup rule ID в 0.2.2:

- `windows-temp`
- `system-wer`
- `system-crash-dumps`

Системные операции:

- `analyze-component-store`
- `component-cleanup`
- `windows-update-cleanup`
- `windows-upgrade-leftovers`
- `delivery-optimization`
- `hibernate-off`
- `hibernate-on`

## Жёсткие запреты

Cleaner не должен вручную удалять:

- `System32`
- `WinSxS`
- `Windows\Installer`
- `Program Files`
- `pagefile.sys`
- корни профиля пользователя
- Documents / Desktop / Downloads
- точки восстановления / shadow copies без отдельного будущего Expert Mode
- `SoftwareDistribution` как обычную директорию


## История и экспорт

SQLite-история хранит только метаданные операций очистки: названия категорий, время, размеры, счётчики удалённых/пропущенных объектов и технические сообщения. Содержимое пользовательских файлов в историю не записывается. В 0.5.0 база физически ограничена 10 последними операциями; более старые строки и их category-items удаляются автоматически. TXT/JSON-экспорт создаётся только по явному нажатию пользователя и содержит тот же ограниченный набор записей.

## Developer Storage 0.4.0

Developer cleanup does not bypass existing cleanup safety controls and does not add arbitrary elevated paths.

- Docker cleanup is delegated to Docker CLI and is limited to build cache, dangling images and stopped containers. `docker volume prune` is intentionally absent.
- WSL is read-only in 0.4.0; no `wsl --unregister` or virtual-disk deletion is exposed.
- npm and NuGet caches are cleared through their official CLIs, not by deleting project directories.
- Android SDK is analyzed first; only an explicitly selected package can be removed through the official `sdkmanager`. No automatic SDK deletion.
- Registry Cleaner and defragmentation are outside the Safe C: Cleaner cleanup model.
- Windows autostart uses only the current-user `HKCU\...\Run` key and requires no elevation.

## Storage Analyzer 0.4.1

Storage Analyzer is read-only. It does not expose a delete action, skips reparse points/junctions, respects Storage Scanner exclusions and only offers Explorer navigation or handoff to the existing read-only drill-down. Cleanup allowlists are not expanded by Analyzer.

## Persistence 0.5.0

- Последний Storage Intelligence snapshot хранится в локальной SQLite-базе и используется только для read-only сравнения/восстановления UI.
- Последний успешный Storage Analyzer result хранится в `%LOCALAPPDATA%\SafeCCleaner\storage-analyzer-last.json`; это имена/пути/размеры найденных объектов и агрегированная статистика, без содержимого файлов.
- Восстановленный Analyzer result не даёт новых прав на удаление: Analyzer остаётся read-only.
- Single-instance guard предотвращает создание второго окна/второй tray icon; повторный обычный запуск только сигнализирует существующему окну показаться.
