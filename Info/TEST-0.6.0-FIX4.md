# v0.6.0 Candidate Fix 4 - Browser fallback validation

- Open «О программе», click «Открыть последний релиз».
- When HTTPS association works: Windows opens the default browser.
- When HTTPS association is broken: the app tries the executable of the Windows-selected HTTPS browser; if unavailable, it tries installed browsers at known safe paths.
- When no browser launches: show a diagnostic line underneath the buttons, without overwriting GitHub release status.
- Confirm link always points to `https://github.com/CactusDex/Safe-C-Cleaner/releases/latest`.
- Confirm no registry settings or Windows associations are changed.
- Confirm release API check still works and does not block the UI.
- Verify links from other applications separately: browser fallback is local to Safe C.
- Build through BUILD-WINDOWS.cmd and BUILD-INSTALLER.cmd on Windows. Test installer upgrade from v0.5.2 before release.

## Status
- Source/XAML checked on non-Windows machine; native WinUI build, installer execution and browser behavior require Windows validation.
