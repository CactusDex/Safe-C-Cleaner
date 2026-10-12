@echo off
setlocal
cd /d "%~dp0"
title Safe C Cleaner - Build and Run
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\build-release.ps1" -Runtime win-x64 -Launch
if errorlevel 1 (
  echo.
  echo BUILD FAILED. Read the error above.
  pause
  exit /b 1
)
