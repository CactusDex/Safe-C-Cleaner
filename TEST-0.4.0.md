# Safe C: Cleaner 0.4.0 — Test checklist

## Build / startup

1. Run `BUILD-AND-RUN-WINDOWS.cmd`.
2. Confirm About shows `0.4.0 (Developer Storage)` and author `Cactus Dex`.
3. Confirm the tray icon appears in Windows hidden icons while the app is running.

## Tray / autostart

1. Enable **Запускать вместе с Windows**.
2. Confirm `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` contains `SafeCCleaner` with `--background`.
3. Restart/sign in: app should start hidden in the tray.
4. Left-click tray icon -> window opens.
5. Right-click -> Open / Scan / Exit.
6. With close-to-tray enabled, press X: window hides but process remains.
7. Tray Exit must fully terminate the app and remove the icon.

## Developer Storage

- Open **Developer Storage** and press **Обновить**. Missing tools must be reported as unavailable without crashing.
- Docker: if Docker is available, verify `docker system df` information appears. Test build cache / dangling images / stopped containers individually. Confirm no `docker volume prune` command exists in the project.
- WSL: verify distro count and VHDX size where available. No distro-delete action should exist.
- npm: verify cache path/size and cleanup via `npm cache clean --force`.
- NuGet: verify local paths/sizes and cleanup via `dotnet nuget locals all --clear`.
- Android SDK: verify component size summary, installed package list, Open SDK, and explicit selected-package uninstall through `sdkmanager`. No automatic SDK deletion should exist.

## Regression

- Quick Clean and Deep Clean still work.
- Storage Intelligence still works.
- Minimum window size remains 1240x850.
- Existing PathGuard/SafeDeletion behavior is unchanged.
