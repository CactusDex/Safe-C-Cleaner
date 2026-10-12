# Safe C: Cleaner 0.3.0 — test checklist

## Build / launch

1. Run `BUILD-WINDOWS.cmd`.
2. Confirm Release build completes and `SafeCCleaner.pri` exists.
3. Launch `SafeCCleaner.exe`.
4. Confirm version label shows `0.3.0 · Storage Intelligence`.

## Storage Intelligence

1. Open **Диск / Storage Intelligence**.
2. Run **Пересканировать**.
3. Confirm Used / Free / Total are populated.
4. Confirm 1/7/30-day cards show either a delta or an explicit insufficient-history message.
5. Confirm “Почему C: изменился” names the baseline being used.
6. If a tracked zone grew, confirm it appears in “Что выросло”.
7. Press **Разобрать** on AppData Local or another existing directory.
8. Confirm top-level children appear sorted by size.
9. Press **Глубже** on a subdirectory and confirm navigation works.
10. Re-run the same drill-down within 15 minutes and confirm the cache message appears.
11. Start a large drill-down and press **Отмена**; app must remain responsive.

## Safety regression

1. Storage Intelligence must expose no delete/clean action for discovered files/directories.
2. Quick Clean categories and behavior must match 0.2.3.
3. Deep Clean commands must match 0.2.3.
4. Elevated helper whitelist must match 0.2.3.
5. PathGuard must match 0.2.3.
6. No Docker/WSL/npm/NuGet/Android cleanup, Registry Cleaner, duplicate removal, defrag or RAM optimizer must exist.

## History

1. Run scans twice and confirm subsequent comparison has a previous baseline.
2. Existing cleanup history TXT and JSON export must still work.
