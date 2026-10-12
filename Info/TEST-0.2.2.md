# Safe C: Cleaner 0.2.2 — Windows test checklist

## 1. Build and launch
- Run `BUILD-WINDOWS.cmd`.
- Wait for `Verified app resource PRI` and `READY`.
- Launch `artifacts\SafeCCleaner-win-x64-portable\SafeCCleaner.exe`.
- Confirm Overview opens without startup errors.

## 2. Quick Clean filters
- Open **Быстрая очистка**.
- Switch **Все / SAFE / ADVANCED**.
- SAFE must hide Recycle Bin and dump categories.
- ADVANCED must show only categories requiring explicit confirmation.
- `Очистить выбранное` must act only on categories visible in the active filter.

## 3. Detailed result dialog
- Run a small SAFE cleanup.
- Confirm the result dialog shows:
  - free disk space before and after;
  - observed free-space change;
  - total deleted according to cleanup rules;
  - a row for each category;
  - deleted/skipped file counts.

## 4. Cleanup history
- Open **История очисток**.
- Confirm the latest operation appears at the top.
- Restart the app and confirm history is still present.
- Existing 0.2.1 `history.db` must migrate automatically without deleting disk snapshots.

## 5. Export
- In **История очисток**, press **Экспортировать TXT**.
- Confirm Explorer opens with a `SafeCCleaner-report-YYYYMMDD-HHMMSS.txt` file selected.
- The report must contain operation metrics only, not user file contents.

## 6. Deep Clean history
Test one available operation (Delivery Optimization is preferable for a low-risk test).
- Complete the operation.
- Confirm the result dialog appears.
- Confirm it is added to **История очисток** with `DEEP` mode.

## 7. Safety regression
- SAFE Temp must still use the 7-day age rule.
- UAC helper must still accept only whitelisted operation IDs.
- No direct manual deletion of WinSxS or SoftwareDistribution.
- `/ResetBase` must not appear anywhere in executable commands.
