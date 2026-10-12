@echo off
setlocal EnableExtensions
chcp 65001 >nul
cd /d "%~dp0"
where powershell.exe >nul 2>nul || (echo PowerShell missing. & pause & exit /b 1)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\github-release.ps1"
if errorlevel 1 echo [ERROR] Release preparation did not complete.
pause
