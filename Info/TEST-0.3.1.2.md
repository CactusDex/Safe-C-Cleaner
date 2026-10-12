# TEST — 0.3.1.2 Mini Patch 2

## Window minimum size
1. Launch the app.
2. Drag any window edge/corner smaller than 1240×850.
3. Expected: resize stops at the minimum; the entire window must not jump or move across the desktop.
4. Verify enlarging and maximizing still work normally.

## Scan button state
1. Start a scan from Overview.
2. Expected: button text becomes `Сканирование…` and button is disabled.
3. The bottom progress bar, ETA and `Отмена` remain available.
4. Press `Отмена`.
5. Expected: after cancellation completes, the scan button becomes enabled again and returns to `Сканировать`.
6. On Disk page verify `Пересканировать` behaves the same way.

## About page
1. Open the navigation footer item `О программе`.
2. Verify version `0.3.1.2 (Mini Patch 2)`.
3. Verify device/Windows, machine name, architecture/CPU and author are visible.
4. Collapse the navigation pane and verify the About item remains accessible by icon.

## Safety regression
- Quick Clean and Deep Clean rules must behave exactly as in 0.3.1.
- No new cleanup categories are introduced.
