# Windows application build

The project is configured as an **unpackaged, self-contained WinUI 3 desktop application**.

## Fastest way

On Windows 10/11 with the .NET 10 SDK installed:

1. Extract the source archive.
2. Double-click `BUILD-WINDOWS.cmd`.
3. When the build finishes, open `artifacts\SafeCCleaner-win-x64-portable`.
4. Launch `SafeCCleaner.exe` normally by double-clicking it.

The published application carries the .NET runtime and Windows App SDK runtime with it, so the target computer does **not** need Visual Studio or a separate .NET/Windows App SDK installation.

`SafeCCleaner.Elevated.exe` must remain next to the main executable. The main process runs as a normal user; the helper is launched through UAC only for approved privileged cleanup operations.

## Supported targets

- `win-x64` — normal Intel/AMD Windows PCs (default)
- `win-arm64` — Windows on ARM

To build ARM64 manually:

```powershell
.\scripts\build-release.ps1 -Runtime win-arm64
```

## Why the release is a folder

The recommended MVP release is a self-contained **portable folder**, rather than forcing every WinUI dependency into one binary. This is simpler to diagnose and keeps the separate privilege boundary for the UAC helper explicit.

## 0.4.0 background mode / tray

`SafeCCleaner.exe --background` starts the app hidden in the Windows notification area. The Settings toggle **Запускать вместе с Windows** writes that command to the current-user Run key. The tray icon exposes Open / Scan / Exit. With **Закрывать в системный трей** enabled, the window close button hides the window while the process remains running.

## Installer 0.5.0+

Для обычной установки Windows используйте один скрипт из корня проекта:

```text
BUILD-INSTALLER.cmd
```

Скрипт:

- закрывает работающий Safe C: Cleaner, чтобы release-файлы не были заблокированы;
- собирает self-contained `win-x64` release;
- при необходимости находит/устанавливает Inno Setup 6;
- создаёт установщик `SafeCCleaner-Setup-<Version>.exe`;
- ставит приложение в `C:\Program Files\SafeCCleaner\`;
- создаёт ярлыки рабочего стола и меню Пуск;
- использует стабильный Inno Setup `AppId`, поэтому новый Setup обновляет предыдущую установленную версию;
- ограничивает установку Windows build 19041 и новее (`MinVersion=10.0.19041`).

Результат:

```text
artifacts\installer\output\
```
