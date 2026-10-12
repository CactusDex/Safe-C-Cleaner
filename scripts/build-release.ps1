param(
    [ValidateSet("win-x64", "win-arm64")]
    [string]$Runtime = "win-x64",
    [switch]$Launch
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Artifacts = Join-Path $Root "artifacts"
$HelperOut = Join-Path $Artifacts "helper-$Runtime"
$AppOut = Join-Path $Artifacts "SafeCCleaner-$Runtime-portable"
$ZipOut = Join-Path $Artifacts "SafeCCleaner-$Runtime-portable.zip"

# Works in both Windows PowerShell 5.1 and PowerShell 7+.
$IsWindowsHost = ($env:OS -eq "Windows_NT")
$IsWindowsVariable = Get-Variable -Name IsWindows -ErrorAction SilentlyContinue
if ($null -ne $IsWindowsVariable) {
    $IsWindowsHost = $IsWindowsHost -or [bool]$IsWindowsVariable.Value
}

if (-not $IsWindowsHost) {
    throw "Release build must be executed on Windows. WinUI 3 uses Windows build tooling."
}

Write-Host ("Windows detected. PowerShell {0}" -f $PSVersionTable.PSVersion) -ForegroundColor DarkGray

# Prefer the native x64 dotnet installation explicitly. This avoids a common
# Windows PATH issue where C:\Program Files (x86)\dotnet\dotnet.exe wins
# even though the x64 SDK is correctly installed under Program Files.
$dotnetCandidates = @()
if ($env:ProgramW6432) { $dotnetCandidates += (Join-Path $env:ProgramW6432 "dotnet\dotnet.exe") }
if ($env:ProgramFiles)  { $dotnetCandidates += (Join-Path $env:ProgramFiles  "dotnet\dotnet.exe") }
if ($env:DOTNET_ROOT)   { $dotnetCandidates += (Join-Path $env:DOTNET_ROOT   "dotnet.exe") }

$pathDotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($null -ne $pathDotnet) { $dotnetCandidates += $pathDotnet.Source }

$Dotnet = $dotnetCandidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -Unique | Select-Object -First 1
if (-not $Dotnet) {
    Write-Host ""
    Write-Host "ERROR: dotnet.exe was not found." -ForegroundColor Red
    Write-Host "Run INSTALL-DOTNET-SDK.cmd, then reopen this folder and run BUILD-WINDOWS.cmd again." -ForegroundColor Yellow
    exit 10
}

Write-Host "Using dotnet: $Dotnet" -ForegroundColor DarkGray

# dotnet.exe can exist while no SDK is installed. Check the SDK list explicitly
# using the exact executable selected above (do not rely on PATH).
$sdkList = @(& $Dotnet --list-sdks 2>$null)
if (($LASTEXITCODE -ne 0) -or ($sdkList.Count -eq 0)) {
    Write-Host ""
    Write-Host "ERROR: .NET SDK is not installed (only the .NET host/runtime may be present)." -ForegroundColor Red
    Write-Host "Run INSTALL-DOTNET-SDK.cmd, then run BUILD-WINDOWS.cmd again." -ForegroundColor Yellow
    exit 10
}

$net10Sdks = @($sdkList | Where-Object { $_ -match '^10\.' })
if ($net10Sdks.Count -eq 0) {
    Write-Host ""
    Write-Host "Installed SDKs:" -ForegroundColor Yellow
    $sdkList | ForEach-Object { Write-Host "  $_" }
    Write-Host ""
    Write-Host "ERROR: this project targets .NET 10, but no .NET 10 SDK is installed." -ForegroundColor Red
    Write-Host "Run INSTALL-DOTNET-SDK.cmd, then run BUILD-WINDOWS.cmd again." -ForegroundColor Yellow
    exit 10
}

$versionOutput = @(& $Dotnet --version 2>$null)
if (($LASTEXITCODE -ne 0) -or ($versionOutput.Count -eq 0)) {
    throw "dotnet is present, but the active SDK version could not be determined."
}
$version = ($versionOutput | Select-Object -First 1).Trim()
Write-Host ".NET SDK detected: $version" -ForegroundColor DarkGray

if (-not $version.StartsWith("10.")) {
    Write-Warning "The active SDK is $version. A .NET 10 SDK is installed, but SDK resolution selected another version."
}

# Remove stale compiled XAML/MSBuild output. WinUI stores generated XAML/XBF
# under obj/bin, so a clean rebuild is important after resource dictionary fixes.
Write-Host "[0/4] Cleaning previous build/XAML output..." -ForegroundColor Cyan
Get-ChildItem (Join-Path $Root "src") -Directory | ForEach-Object {
    Remove-Item (Join-Path $_.FullName "bin") -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $_.FullName "obj") -Recurse -Force -ErrorAction SilentlyContinue
}

Remove-Item $HelperOut -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $AppOut -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $ZipOut -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $HelperOut | Out-Null
New-Item -ItemType Directory -Force -Path $AppOut | Out-Null

Write-Host "[1/4] Restoring packages..." -ForegroundColor Cyan
& $Dotnet restore "$Root\SafeCCleaner.sln" -r $Runtime
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed." }

Write-Host "[2/4] Publishing UAC helper..." -ForegroundColor Cyan
& $Dotnet publish "$Root\src\SafeCCleaner.Elevated\SafeCCleaner.Elevated.csproj" `
    -c Release -r $Runtime --self-contained true `
    -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false `
    -o $HelperOut
if ($LASTEXITCODE -ne 0) { throw "Elevated helper publish failed." }

Write-Host "[3/4] Publishing portable WinUI app..." -ForegroundColor Cyan
& $Dotnet publish "$Root\src\SafeCCleaner.App\SafeCCleaner.App.csproj" `
    -c Release -r $Runtime --self-contained true `
    -p:WindowsAppSDKSelfContained=true `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=true `
    -p:DebugType=None -p:DebugSymbols=false `
    -o $AppOut
if ($LASTEXITCODE -ne 0) { throw "WinUI app publish failed." }

# Windows App SDK / .NET 10 unpackaged publish workaround verification.
# MainWindow.xaml is compiled into the app PRI. A current dotnet publish bug can
# omit that PRI from publish output even though the build generated it.
$AppPri = Join-Path $AppOut "SafeCCleaner.pri"
if (-not (Test-Path $AppPri)) {
    Write-Warning "SafeCCleaner.pri was not added to publish output. Trying guarded fallback copy..."

    $priCandidates = @(Get-ChildItem (Join-Path $Root "src\SafeCCleaner.App\bin") `
        -Filter "SafeCCleaner.pri" -File -Recurse -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending)

    if ($priCandidates.Count -gt 0) {
        Copy-Item $priCandidates[0].FullName $AppPri -Force
        Write-Host "PRI fallback copied: $($priCandidates[0].FullName)" -ForegroundColor Yellow
    }
}

if (-not (Test-Path $AppPri)) {
    throw "Publish output is missing SafeCCleaner.pri. WinUI XAML cannot start without the application PRI."
}

$priInfo = Get-Item $AppPri
Write-Host ("Verified app resource PRI: {0} ({1} bytes)" -f $priInfo.Name, $priInfo.Length) -ForegroundColor Green

Copy-Item "$HelperOut\SafeCCleaner.Elevated.exe" "$AppOut\SafeCCleaner.Elevated.exe" -Force

@"
SAFE C: CLEANER - PORTABLE

Run: SafeCCleaner.exe

This is a self-contained Windows build. The target PC does not need Visual Studio or a separately installed .NET runtime.
Keep SafeCCleaner.Elevated.exe in the same folder: it is used only for operations that require UAC elevation.

Version: 0.4.1.3 Branding Patch
Runtime: $Runtime
"@ | Set-Content -Encoding UTF8 (Join-Path $AppOut "START HERE.txt")

Write-Host "[4/4] Creating portable ZIP..." -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $AppOut "*") -DestinationPath $ZipOut -CompressionLevel Optimal

Write-Host ""
Write-Host "READY" -ForegroundColor Green
Write-Host "Application: $AppOut\SafeCCleaner.exe"
Write-Host "Portable ZIP: $ZipOut"

if ($Launch) {
    Start-Process (Join-Path $AppOut "SafeCCleaner.exe")
}
