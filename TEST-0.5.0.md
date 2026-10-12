# Safe C: Cleaner 0.5.0 — test checklist

## Single instance

1. Запустить `SafeCCleaner.exe`.
2. Закрыть окно крестиком при включённом `Закрывать в системный трей`.
3. Снова запустить тот же `SafeCCleaner.exe` или ярлык.
4. Ожидается: существующее окно появляется и получает фокус; в Task Manager остаётся один `SafeCCleaner.exe`, в трее одна иконка.
5. Проверить запуск с `--background`, когда приложение уже работает: второй процесс должен завершиться без вывода окна на передний план.

## Persistence

1. Выполнить Storage Intelligence scan, закрыть приложение полностью через `Выйти` из трея, запустить снова с отключённым автосканированием.
2. Ожидается: последний снимок диска, дата, tracked zones и сравнение доступны без нового scan.
3. Выполнить Storage Analyzer, дождаться `Готово`, выйти и запустить снова.
4. Ожидается: Top files/folders восстановлены, показано время последнего анализа; после 24 часов данные помечаются как потенциально устаревшие.
5. Кнопка Analyzer после восстановления называется `Пересканировать C:`.

## Cleanup history

1. Создать более 10 тестовых записей очистки/системных операций.
2. Ожидается: UI, TXT и JSON содержат только 10 последних операций.
3. Перезапустить приложение — лимит должен сохраниться физически в SQLite.

## UI info buttons

- Проверить hover tooltip и click flyout возле Quick Clean, Deep Clean, Storage Intelligence, Storage Analyzer, Developer Storage, History, Safety и Settings.
- В Quick Clean проверить `ⓘ` у каждой категории: описание совпадает с cleanup rule.
- Warning конкретной категории, если он есть, остаётся видимым в карточке.

## Safety regression

- SAFE/ADVANCED cleanup работает как до 0.5.0.
- UAC helper запускается только для разрешённых rule/operation IDs.
- Storage Analyzer не содержит delete action.
- Docker volumes и WSL distributions не удаляются автоматически.
- `DISM /ResetBase` отсутствует.

## Windows compatibility

- Основной target runtime: win-x64.
- `SupportedOSPlatformVersion`: 10.0.19041.0.
- Перед внешним релизом проверить чистую Windows 10 22H2 x64 и Windows 11 x64: install, launch, tray, UAC helper, cleanup, analyzer, uninstall/update-in-place.
