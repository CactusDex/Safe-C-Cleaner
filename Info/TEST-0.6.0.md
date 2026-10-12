# Safe C: Cleaner v0.6.0 - Windows verification checklist

1. Run BUILD-WINDOWS.cmd. Confirm clean Release compilation.
2. Run BUILD-INSTALLER.cmd. Confirm generated EXE and same Inno AppId as 0.5.2.
3. Upgrade 0.5.2 in place; verify settings, history, SQLite and tray.
4. Run quick scan, deep clean read-only previews, Storage Intelligence, Storage Analyzer, Developer Storage.
5. Test long paths and category names; status line must not resize layout.
6. Test cancellation during scans and restart without stale UI progress.
7. Check About page: 0.6.0, manual update check against official GitHub Releases.
8. Check offline/timeout response; Safe C should continue working.
9. Check 24h automatic check and both settings toggles across restart.
10. Verify no updater downloads or automatically executes EXE.
11. Benchmark analyzers on same disk before and after, collect elapsed time, peak RAM and CPU; no speedup claimed without measurements.
12. Verify PathGuard, whitelist, Elevated Helper and all confirmations.
13. Check GITHUB-PUSH.cmd on a test branch, GITHUB-RELEASE.cmd without publishing; do not publish until QA passes.

## Fix 3 regression

- During long-running analyzer operation, no directory path is displayed in the progress card; verify fixed heights and percentage/file-count/ETA stability.
- About: no CPU row or logo card; update card is centered under device table.
- Settings: log-folder and copy-diagnostics actions available.
- Safety menu item is gone, but guardrails, confirmations and Elevated Helper remain unchanged.
- Download button opens the canonical GitHub latest release through default HTTPS handler. If Windows reports missing app, inspect Settings > Apps > Default apps > HTTP/HTTPS associations.
