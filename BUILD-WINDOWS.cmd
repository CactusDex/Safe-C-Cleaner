@echo off
setlocal
cd /d "%~dp0"
title Safe C Cleaner - Windows Build

echo ======================================================
echo   Safe C: Cleaner - build portable Windows app
echo ======================================================
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\build-release.ps1" -Runtime win-x64
set "BUILD_EXIT=%ERRORLEVEL%"

if "%BUILD_EXIT%"=="10" (
  echo.
  echo ------------------------------------------------------
  echo .NET 10 SDK is missing.
  echo Run INSTALL-DOTNET-SDK.cmd first, then run this file again.
  echo ------------------------------------------------------
  echo.
  pause
  exit /b 10
)

if not "%BUILD_EXIT%"=="0" (
  echo.
  echo BUILD FAILED. Read the error above.
  pause
  exit /b %BUILD_EXIT%
)

echo.
echo Build finished successfully.
echo Opening output folder...
explorer.exe "%~dp0artifacts\SafeCCleaner-win-x64-portable"
pause
