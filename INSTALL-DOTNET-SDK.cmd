@echo off
setlocal
cd /d "%~dp0"
title Safe C Cleaner - Install .NET 10 SDK

echo ======================================================
echo   Safe C: Cleaner - install build prerequisite
echo ======================================================
echo.

echo Checking for winget...
where winget.exe >nul 2>&1
if errorlevel 1 (
  echo.
  echo ERROR: winget was not found.
  echo Install the .NET 10 SDK x64 manually from:
  echo https://dotnet.microsoft.com/download/dotnet/10.0
  echo.
  pause
  exit /b 1
)

echo Installing Microsoft .NET 10 SDK...
echo Windows may ask for administrator permission.
echo.
winget install --id Microsoft.DotNet.SDK.10 --source winget --accept-package-agreements --accept-source-agreements
if errorlevel 1 (
  echo.
  echo INSTALL FAILED. Read the winget message above.
  pause
  exit /b 1
)

echo.
echo ======================================================
echo   .NET 10 SDK installation finished.
echo ======================================================
echo.
echo IMPORTANT: close this window, then run BUILD-WINDOWS.cmd again.
echo If dotnet still reports no SDK, sign out/in or reboot Windows once.
echo.
pause
