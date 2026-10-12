@echo off
setlocal
cd /d "%~dp0"
title Safe C Cleaner - Diagnostic Run

set "APP=%~dp0artifacts\SafeCCleaner-win-x64-portable\SafeCCleaner.exe"
set "LOG=%LOCALAPPDATA%\SafeCCleaner\startup.log"

if not exist "%APP%" (
  echo SafeCCleaner.exe was not found.
  echo First run BUILD-WINDOWS.cmd and wait for BUILD SUCCESS/READY.
  echo.
  echo Expected file:
  echo %APP%
  pause
  exit /b 2
)

if exist "%LOG%" del /q "%LOG%" >nul 2>&1

echo Starting:
echo %APP%
echo.
start "" /wait "%APP%"
set "EXITCODE=%ERRORLEVEL%"

echo.
echo Application exit code: %EXITCODE%
if exist "%LOG%" (
  echo Opening startup log...
  start "" notepad.exe "%LOG%"
) else (
  echo No startup.log was created.
  echo If the app still did not open, check Windows Event Viewer ^> Windows Logs ^> Application.
)

pause
