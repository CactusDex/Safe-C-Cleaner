## 0.6.0 - Stability, Performance & GitHub Integration (candidate)

- [x] Scan status width and line clamp.
- [x] Throttled UI progress updates.
- [x] GitHub Releases latest-version check and About card.
- [x] Update preferences and 24h cache.
- [x] GITHUB-PUSH.cmd and GITHUB-RELEASE.cmd.
- [ ] Windows build, installer, performance benchmarks and regression test pending.

# Roadmap

## 0.5.2 — Analyzer Filter & Icon Polish ✅

- [x] Remove the overview subtitle «Анализ диска C: и безопасная очистка».
- [x] Remove Analyzer name/path search from the UI.
- [x] Remove Analyzer Top 50/100 selector; keep a fixed internal 50-result display limit.
- [x] Replace the old category selector with one «Тип файлов» dropdown.
- [x] Add file types: documents, video, images, music, archives, disk images, executables and other.
- [x] Preserve size/age/sort filters and read-only Analyzer behavior.
- [x] Refresh the Safe C: Cleaner logo and Windows icon assets for better small-size recognition.

## Backlog — after stabilization

- [ ] **Developer Project Cleanup** — analyze `bin`, `obj`, `.gradle`, `node_modules`, `build` and project cache directories, then allow selective safe cleanup.
- [ ] **Locked Files** — when deletion is blocked, show the process holding the file instead of only a generic error.

## 0.5.1 — Compact Quick Clean UI ✅

- Compact list cards для Quick Clean.
- Info/warning details без постоянной второй строки.
- Sticky summary выбранных категорий и найденного объёма.
- Disabled clean для пустых категорий.

## 0.5.0 — UX, Persistence & Single Instance ✅

- [x] Compact info-button/flyout pattern for major pages.
- [x] Per-category info in Quick Clean with persistent warning text kept visible.
- [x] Single-instance launch behavior with activation of the existing tray/window instance.
- [x] Restore the latest Storage Intelligence disk snapshot on startup.
- [x] Persist and restore the latest Storage Analyzer result with timestamp/staleness hint.
- [x] Cap cleanup history to the newest 10 operations in the SQLite database itself.
- [x] Rework the Safety page to explain real guardrails instead of generic copy.

## 0.4.0 — Developer Storage + Windows Integration ✅

- [x] Docker storage analysis.
- [x] Docker build-cache cleanup through `docker builder prune -f`.
- [x] Dangling image cleanup through `docker image prune -f`.
- [x] Stopped-container cleanup through `docker container prune -f`.
- [x] Docker volumes are never pruned by Safe C: Cleaner.
- [x] WSL distribution/VHDX analysis without deleting distributions.
- [x] npm cache analysis and official CLI cleanup.
- [x] NuGet cache analysis and official `dotnet nuget locals` cleanup.
- [x] Android SDK storage analysis for system-images/platforms/build-tools/emulator.
- [x] Explicitly selected Android SDK package uninstall through `sdkmanager --uninstall`.
- [x] Windows autostart toggle.
- [x] Background launch with `--background`.
- [x] Native system-tray icon with Open / Scan / Exit.
- [x] Optional close-to-tray behavior.

## 0.4.1 — Storage Analyzer ✅

- [x] Top 50 / Top 100 largest files and folders.
- [x] Search by name/path.
- [x] Size filters: 500 MB / 1 GB / 5 GB / 10 GB.
- [x] File categories: disk images, archives, installers, video, backups.
- [x] Old-file filters: 180 / 365 days.
- [x] Sort by size/date/name.
- [x] Cancel, approximate progress and ETA.
- [x] Explorer actions plus Storage Intelligence drill-down for folders.
- [x] Read-only: no delete action in Analyzer.

## 0.4.0.2 — UI Fix ✅

- [x] Hide the NavigationView pane scrollbar near Settings/About without disabling page/list scrolling.

## Future — Cleanup Recommendations

Planned recommendation classes: `SAFE` / `REVIEW` / `SYSTEM`, with explanations. Storage Scanner still does not auto-delete findings.

## Completed foundation

- 0.3.3 — System Cleanup Expansion ✅
- 0.3.2 — Usability & Settings ✅
- 0.3.1.2 — Mini Patch 2 ✅
- 0.3.1 — Scanner Polish & Storage UX+ ✅
- 0.3.1-mini — UI Polish Shell ✅
- 0.3.0 — Storage Intelligence ✅
- 0.2.3 — Storage UX ✅
- 0.2.2 — Cleanup UX ✅

## Explicitly excluded

Do not implement without a separate decision:

- Registry Cleaner;
- defragmentation as a Safe C: Cleaner cleanup function;
- RAM “optimization”;
- automatic deletion of Storage Scanner results;
- aggressive manual cleanup of protected Windows directories;
- automatic Docker volume deletion;
- automatic WSL distribution deletion.

`DISM /ResetBase` is not part of normal Deep Clean.

## 0.4.0.1 — Stabilization Patch ✅

- UI geometry fix для About/Settings/Developer Storage;
- navigation scrollbar polish;
- progress/ETA layout fix;
- robust tool discovery для Docker/WSL/npm/dotnet;
- Developer Storage error-state hardening.
