@echo off
setlocal EnableExtensions

rem ============================================================
rem Safe C: Cleaner - Installer Builder
rem Author: Cactus Dex
rem ============================================================

rem Keep the console open even if CMD hits an unexpected parse/runtime error.
if /I "%~1"=="__RUN__" goto MAIN
start "Safe C: Cleaner - Installer Builder" cmd.exe /k ""%~f0" __RUN__"
exit /b

:MAIN
chcp 65001 >nul
title Safe C: Cleaner - Installer Builder

set "ROOT=%~dp0"
cd /d "%ROOT%"

echo.
echo ============================================================
echo   Safe C: Cleaner - Installer Builder
echo ============================================================
echo.

rem ------------------------------------------------------------
rem Validate project
rem ------------------------------------------------------------
if not exist "%ROOT%SafeCCleaner.sln" goto ERR_NO_SLN
if not exist "%ROOT%scripts\build-release.ps1" goto ERR_NO_BUILD
if not exist "%ROOT%src\SafeCCleaner.App\SafeCCleaner.App.csproj" goto ERR_NO_CSPROJ
if not exist "%ROOT%src\SafeCCleaner.App\Assets\SafeCCleaner.ico" goto ERR_NO_ICON

rem ------------------------------------------------------------
rem Read version safely (no FOR/F command nesting)
rem ------------------------------------------------------------
set "APPVER="
set "VERFILE=%TEMP%\safeccleaner-version-%RANDOM%.txt"

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$x=[xml](Get-Content -LiteralPath '%ROOT%src\SafeCCleaner.App\SafeCCleaner.App.csproj' -Raw); $n=$x.SelectSingleNode('//Version'); if($null -ne $n){ Set-Content -LiteralPath '%VERFILE%' -Value $n.InnerText.Trim() -Encoding ASCII }"

if exist "%VERFILE%" set /p APPVER=<"%VERFILE%"
if exist "%VERFILE%" del /q "%VERFILE%" >nul 2>nul

if not defined APPVER goto ERR_VERSION

echo [INFO] App version: %APPVER%
echo.

rem ------------------------------------------------------------
rem Find Inno Setup
rem ------------------------------------------------------------
call :FIND_INNO
if defined ISCC goto HAVE_INNO

echo [INFO] Inno Setup 6 not found.
echo [INFO] Trying installation through winget...
echo.

where winget.exe >nul 2>nul
if errorlevel 1 goto ERR_NO_WINGET

winget install --id JRSoftware.InnoSetup -e --source winget --accept-source-agreements --accept-package-agreements
if errorlevel 1 goto ERR_INNO_INSTALL

timeout /t 2 /nobreak >nul
call :FIND_INNO
if not defined ISCC goto ERR_INNO_NOT_FOUND

:HAVE_INNO
echo [INFO] Inno Setup compiler:
echo        %ISCC%
echo.

rem ------------------------------------------------------------
rem Stop running Safe C: Cleaner instances before publishing
rem ------------------------------------------------------------
echo [0/3] Checking for running Safe C: Cleaner instances...
echo.

tasklist /FI "IMAGENAME eq SafeCCleaner.exe" 2>nul | find /I "SafeCCleaner.exe" >nul
if not errorlevel 1 (
    echo [INFO] Safe C: Cleaner is running in the window or tray.
    echo [INFO] Closing it so release files can be replaced...
    taskkill /F /IM SafeCCleaner.exe >nul 2>nul
)

taskkill /F /IM SafeCCleaner.Elevated.exe >nul 2>nul

set /a WAITCOUNT=0

:WAIT_FOR_APP_EXIT
tasklist /FI "IMAGENAME eq SafeCCleaner.exe" 2>nul | find /I "SafeCCleaner.exe" >nul
if errorlevel 1 goto APP_STOPPED

set /a WAITCOUNT+=1
if !WAITCOUNT! GEQ 10 goto ERR_APP_STILL_RUNNING

timeout /t 1 /nobreak >nul
goto WAIT_FOR_APP_EXIT

:APP_STOPPED
rem Let Windows release module/file handles.
timeout /t 2 /nobreak >nul

set "APP_OUT=%ROOT%artifacts\SafeCCleaner-win-x64-portable"

if exist "%APP_OUT%" (
    echo [INFO] Removing previous portable build...
    rmdir /s /q "%APP_OUT%" 2>nul
)

if exist "%APP_OUT%" (
    echo [WARN] Output is still locked. Waiting 3 seconds...
    timeout /t 3 /nobreak >nul
    rmdir /s /q "%APP_OUT%" 2>nul
)

if exist "%APP_OUT%" goto ERR_OUTPUT_LOCKED

rem ------------------------------------------------------------
rem Build current Safe C: Cleaner release
rem ------------------------------------------------------------
echo.
echo [1/3] Building Safe C: Cleaner self-contained win-x64 release...
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%scripts\build-release.ps1" -Runtime win-x64
if errorlevel 1 goto ERR_RELEASE_BUILD

if not exist "%APP_OUT%\SafeCCleaner.exe" goto ERR_APP_NOT_FOUND
if not exist "%APP_OUT%\SafeCCleaner.Elevated.exe" goto ERR_HELPER_NOT_FOUND

rem ------------------------------------------------------------
rem Prepare installer workspace
rem ------------------------------------------------------------
echo.
echo [2/3] Generating Inno Setup configuration...

set "INSTALLER_DIR=%ROOT%artifacts\installer"
set "OUTPUT_DIR=%INSTALLER_DIR%\output"
set "ISS=%INSTALLER_DIR%\SafeCCleaner.generated.iss"

if exist "%INSTALLER_DIR%" rmdir /s /q "%INSTALLER_DIR%"
mkdir "%INSTALLER_DIR%" >nul 2>nul
mkdir "%OUTPUT_DIR%" >nul 2>nul

rem Generate .iss without a parenthesized ECHO block.
> "%ISS%" echo #define MyAppName "Safe C: Cleaner"
>>"%ISS%" echo #define MyAppVersion "%APPVER%"
>>"%ISS%" echo #define MyAppPublisher "Cactus Dex"
>>"%ISS%" echo #define MyAppExeName "SafeCCleaner.exe"
>>"%ISS%" echo.
>>"%ISS%" echo [Setup]
>>"%ISS%" echo AppId={{B2FB8A31-DB88-4E57-A8A3-2574AC049D10}
>>"%ISS%" echo AppName={#MyAppName}
>>"%ISS%" echo AppVersion={#MyAppVersion}
>>"%ISS%" echo AppVerName={#MyAppName} {#MyAppVersion}
>>"%ISS%" echo AppPublisher={#MyAppPublisher}
>>"%ISS%" echo VersionInfoVersion={#MyAppVersion}
>>"%ISS%" echo VersionInfoCompany={#MyAppPublisher}
>>"%ISS%" echo VersionInfoDescription=Safe C: Cleaner Installer
>>"%ISS%" echo VersionInfoProductName={#MyAppName}
>>"%ISS%" echo DefaultDirName={autopf}\SafeCCleaner
>>"%ISS%" echo DefaultGroupName=Safe C Cleaner
>>"%ISS%" echo DisableProgramGroupPage=yes
>>"%ISS%" echo OutputDir=%OUTPUT_DIR%
>>"%ISS%" echo OutputBaseFilename=SafeCCleaner-Setup-{#MyAppVersion}
>>"%ISS%" echo SetupIconFile=%ROOT%src\SafeCCleaner.App\Assets\SafeCCleaner.ico
>>"%ISS%" echo UninstallDisplayIcon={app}\{#MyAppExeName}
>>"%ISS%" echo Compression=lzma2
>>"%ISS%" echo SolidCompression=yes
>>"%ISS%" echo WizardStyle=modern
>>"%ISS%" echo PrivilegesRequired=admin
>>"%ISS%" echo ArchitecturesAllowed=x64compatible
>>"%ISS%" echo ArchitecturesInstallIn64BitMode=x64compatible
>>"%ISS%" echo MinVersion=10.0.19041
>>"%ISS%" echo CloseApplications=yes
>>"%ISS%" echo RestartApplications=no
>>"%ISS%" echo.
>>"%ISS%" echo [Languages]
>>"%ISS%" echo Name: "english"; MessagesFile: "compiler:Default.isl"
>>"%ISS%" echo Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
>>"%ISS%" echo.
>>"%ISS%" echo [Files]
>>"%ISS%" echo Source: "%APP_OUT%\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
>>"%ISS%" echo.
>>"%ISS%" echo [Icons]
>>"%ISS%" echo Name: "{autoprograms}\Safe C Cleaner"; Filename: "{app}\SafeCCleaner.exe"; WorkingDir: "{app}"
>>"%ISS%" echo Name: "{autodesktop}\Safe C Cleaner"; Filename: "{app}\SafeCCleaner.exe"; WorkingDir: "{app}"
>>"%ISS%" echo.
>>"%ISS%" echo [Run]
>>"%ISS%" echo Filename: "{app}\SafeCCleaner.exe"; Description: "Запустить Safe C: Cleaner"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

if not exist "%ISS%" goto ERR_ISS

rem ------------------------------------------------------------
rem Compile installer
rem ------------------------------------------------------------
echo.
echo [3/3] Building Setup.exe...
echo.

"%ISCC%" "%ISS%"
if errorlevel 1 goto ERR_INNO_COMPILE

set "SETUP_EXE=%OUTPUT_DIR%\SafeCCleaner-Setup-%APPVER%.exe"
set "SHA_FILE=%OUTPUT_DIR%\SafeCCleaner-Setup-%APPVER%.sha256.txt"

if not exist "%SETUP_EXE%" goto ERR_SETUP_NOT_FOUND

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$f='%SETUP_EXE%'; $h=(Get-FileHash -Algorithm SHA256 -LiteralPath $f).Hash.ToLower(); Set-Content -LiteralPath '%SHA_FILE%' -Encoding ASCII -Value ($h + '  ' + [IO.Path]::GetFileName($f)); Write-Host ('SHA-256: ' + $h)"

echo.
echo ============================================================
echo   READY
echo ============================================================
echo.
echo Installer:
echo   %SETUP_EXE%
echo.
echo SHA-256:
echo   %SHA_FILE%
echo.
echo Installation will create:
echo   [OK] C:\Program Files\Safe C Cleaner
echo   [OK] Desktop shortcut
echo   [OK] Start Menu shortcut
echo   [OK] Windows uninstall entry
echo   [OK] Launch option after setup
echo.
echo Opening output folder...
start "" explorer.exe "%OUTPUT_DIR%"
echo.
echo Build finished successfully.
goto END


rem ============================================================
rem Inno Setup locator
rem ============================================================
:FIND_INNO
set "ISCC="

if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if defined ISCC goto :eof

if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if defined ISCC goto :eof

if exist "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if defined ISCC goto :eof

if exist "%LOCALAPPDATA%\Inno Setup 6\ISCC.exe" set "ISCC=%LOCALAPPDATA%\Inno Setup 6\ISCC.exe"
goto :eof


rem ============================================================
rem Errors
rem ============================================================
:ERR_VERSION
echo [ERROR] Could not read the application version from SafeCCleaner.App.csproj.
echo Check the ^<Version^> element before building the installer.
goto FAIL

:ERR_NO_SLN
echo [ERROR] SafeCCleaner.sln not found.
echo Put this CMD in the project root next to SafeCCleaner.sln.
goto FAIL

:ERR_NO_BUILD
echo [ERROR] scripts\build-release.ps1 not found.
goto FAIL

:ERR_NO_CSPROJ
echo [ERROR] SafeCCleaner.App.csproj not found.
goto FAIL

:ERR_NO_ICON
echo [ERROR] SafeCCleaner.ico not found.
echo Expected:
echo %ROOT%src\SafeCCleaner.App\Assets\SafeCCleaner.ico
goto FAIL

:ERR_NO_WINGET
echo [ERROR] winget.exe is not available.
echo Inno Setup 6 is required.
goto FAIL

:ERR_INNO_INSTALL
echo [ERROR] winget failed to install Inno Setup 6.
goto FAIL

:ERR_INNO_NOT_FOUND
echo [ERROR] Inno Setup was installed, but ISCC.exe was not found.
echo Checked:
echo   %ProgramFiles(x86)%\Inno Setup 6\ISCC.exe
echo   %ProgramFiles%\Inno Setup 6\ISCC.exe
echo   %LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe
echo.
echo Open Inno Setup once, close it, and run this builder again.
goto FAIL

:ERR_APP_STILL_RUNNING
echo [ERROR] SafeCCleaner.exe is still running after 10 seconds.
echo End SafeCCleaner.exe in Task Manager and run this builder again.
goto FAIL

:ERR_OUTPUT_LOCKED
echo [ERROR] Previous portable output is still locked:
echo %APP_OUT%
echo.
echo Close Safe C: Cleaner completely and run this builder again.
goto FAIL

:ERR_RELEASE_BUILD
echo [ERROR] Safe C: Cleaner release build failed.
goto FAIL

:ERR_APP_NOT_FOUND
echo [ERROR] Published SafeCCleaner.exe was not found:
echo %APP_OUT%\SafeCCleaner.exe
goto FAIL

:ERR_HELPER_NOT_FOUND
echo [ERROR] Published SafeCCleaner.Elevated.exe was not found:
echo %APP_OUT%\SafeCCleaner.Elevated.exe
goto FAIL

:ERR_ISS
echo [ERROR] Failed to generate Inno Setup configuration.
goto FAIL

:ERR_INNO_COMPILE
echo [ERROR] Inno Setup compilation failed.
goto FAIL

:ERR_SETUP_NOT_FOUND
echo [ERROR] Inno Setup finished, but Setup.exe was not found:
echo %SETUP_EXE%
goto FAIL

:FAIL
echo.
echo ============================================================
echo   BUILD FAILED
echo ============================================================
echo.
echo The window will stay open so you can copy the error.
goto END

:END
echo.
echo You can close this window when finished.
echo.
exit /b
