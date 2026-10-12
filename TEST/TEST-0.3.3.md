# TEST — Safe C: Cleaner 0.3.3 System Cleanup Expansion

## Build / startup

1. Run `BUILD-AND-RUN-WINDOWS.cmd`.
2. Verify About shows `0.3.3 (System Cleanup Expansion)` and author `Cactus Dex`.
3. Verify no real-name or obsolete author spelling remains.

## Quick Clean system caches

1. Run a scan.
2. Verify separate rows for DirectX Shader Cache, GPU Shader Cache, Thumbnail Cache, WER and crash dumps.
3. GPU Shader Cache must only target user-local NVIDIA/AMD cache folders.
4. SAFE categories may be selected automatically; app/system crash dumps remain ADVANCED and not auto-selected.

## Windows Update Cleanup

1. Open Deep Clean and press `Очистить обновления`.
2. Confirm one UAC prompt.
3. Confirm the app reports completion/failure without touching `SoftwareDistribution` or `WinSxS` directly.
4. After completion, inspect `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VolumeCaches`: temporary `StateFlags9133` values must be restored/removed.
5. Verify unrelated Disk Cleanup categories were not executed.

## Existing system operations

1. Delivery Optimization continues to use `Delete-DeliveryOptimizationCache`.
2. Component Store continues to use DISM `/StartComponentCleanup` and never `/ResetBase`.
3. Previous Windows installation cleanup remains explicit/ADVANCED.
4. Hibernation remains explicit and reversible.

## Safety regression

- Documents/Desktop/Downloads must not be cleanup roots.
- No arbitrary path can be passed to elevated helper.
- Storage Scanner remains read-only.
- Deferred cleanup modules remain absent.
