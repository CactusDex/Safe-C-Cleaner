@echo off
setlocal EnableExtensions
chcp 65001 >nul
cd /d "%~dp0"
if not exist ".git" (echo [ERROR] .git not found. & pause & exit /b 1)
where git >nul 2>nul || (echo [ERROR] Git not found. & pause & exit /b 1)
git remote -v
git remote get-url origin | findstr /i /c:"CactusDex/Safe-C-Cleaner" >nul || (echo [ERROR] Unexpected Git remote. & pause & exit /b 1)
echo.
echo === CHANGES ===
git status --short
echo.
echo [IMPORTANT] Review modified and deleted files above.
echo [IMPORTANT] Check secrets in every file before staging.
choice /m "Stage ALL displayed changes, including deletions"
if errorlevel 2 (echo Cancelled. & pause & exit /b 0)
git add -A
if errorlevel 1 goto fail
git diff --cached --check
if errorlevel 1 goto fail
echo.
echo === STAGED FILES ===
git diff --cached --name-status
echo.
echo Review staged changes for keys, passwords, tokens, private data and generated artifacts.
choice /m "Commit and push exactly these staged changes"
if errorlevel 2 (git reset & echo Cancelled. & pause & exit /b 0)
set /p "MESSAGE=Commit message: "
if not defined MESSAGE (echo Commit message required. & pause & exit /b 1)
git commit -m "%MESSAGE%"
if errorlevel 1 goto fail
for /f "delims=" %%B in ('git branch --show-current') do set "BRANCH=%%B"
if not defined BRANCH goto fail
git push -u origin "%BRANCH%"
if errorlevel 1 goto fail
echo.
echo Success: https://github.com/CactusDex/Safe-C-Cleaner
goto done
:fail
echo [ERROR] Operation failed. Review Git output. No force push was attempted.
:done
pause
