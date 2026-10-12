# TEST — Safe C: Cleaner 0.3.2 Usability & Settings

## Settings
1. Open **Настройки** from the bottom of navigation.
2. Disable **Сканировать при запуске**, restart app, verify no automatic scan starts.
3. Set Scanner cache to 5/15/30/60 minutes, restart, verify selection persists.
4. Add an exclusion path, open Storage Scanner, verify excluded path is skipped.
5. Remove an exclusion and verify it can be scanned again.
6. Reset settings and verify defaults return while exclusions remain intact.

## Window/UI state
1. Resize/move the window and close the app.
2. Restart and verify size/position are restored.
3. Move the app to a secondary monitor, close, disconnect that monitor, then restart. Window must appear on an available monitor.
4. Collapse/expand navigation, close and restart; pane state should persist.
5. Open another section, close and restart; last section should persist.

## Navigation
1. On Overview press **Открыть анализ**; Storage Intelligence should open.
2. Press **Открыть Deep Clean**; Deep Clean should open.

## Scan result UX
1. Run a scan.
2. Verify Overview shows last scan date/time, duration, zone count and safe reclaimable amount.
3. Verify Access Denied in drill-down does not crash the app and is reflected in status.

## About/diagnostics
1. Verify author is **Cactus Dex** only.
2. Verify version is `0.3.2 (Usability & Settings)`.
3. Verify .NET and Windows App SDK version rows are visible.
4. **Открыть папку логов** opens `%LOCALAPPDATA%\SafeCCleaner`.
5. **Скопировать сведения** copies diagnostic information to clipboard.

## Safety regression
Run existing 0.3.1 tests. Cleanup rules, PathGuard and elevated operation allowlists must remain unchanged.
