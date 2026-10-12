$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
function VerifyExit { param([string]$What) if ($LASTEXITCODE -ne 0) { throw "$What failed ($LASTEXITCODE)." } }
if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw 'Git is not installed.' }
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'GitHub CLI (gh) is required to publish a release.' }
$remote = (& git remote get-url origin).Trim(); VerifyExit 'git remote'
if ($remote -notmatch '(?i)CactusDex/Safe-C-Cleaner(?:\.git)?$') { throw "Unexpected origin: $remote" }
$branch = (& git branch --show-current).Trim(); VerifyExit 'git branch'
if ($branch -ne 'main') { throw 'Release must be made from main. Merge and review changes first.' }
$changes = @(& git status --porcelain); VerifyExit 'git status'
if ($changes.Count -gt 0) { throw 'Worktree is not clean. Commit and push changes first.' }
& git fetch origin main --tags; VerifyExit 'git fetch'
$local = (& git rev-parse HEAD).Trim(); VerifyExit 'git rev-parse'
$remoteHead = (& git rev-parse origin/main).Trim(); VerifyExit 'git rev-parse remote'
if ($local -ne $remoteHead) { throw 'Local main differs from origin/main. Synchronize safely first.' }
[xml]$project = Get-Content -LiteralPath (Join-Path $root 'src/SafeCCleaner.App/SafeCCleaner.App.csproj') -Raw
$version = [string]$project.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Project version must be Major.Minor.Patch.' }
$tag = "v$version"
& git rev-parse --verify "refs/tags/$tag" 2>$null | Out-Null
if ($LASTEXITCODE -eq 0) { throw "Tag already exists: $tag" }
& gh release view $tag --repo CactusDex/Safe-C-Cleaner 2>$null | Out-Null
if ($LASTEXITCODE -eq 0) { throw "Release already exists: $tag" }
Write-Host "Preparing Safe C: Cleaner $tag (commit $local)" -ForegroundColor Cyan
$confirm = Read-Host 'Build installer now? Type BUILD'
if ($confirm -cne 'BUILD') { throw 'Cancelled.' }
$buildStarted = Get-Date
& cmd.exe /c 'call BUILD-INSTALLER.cmd __RUN__'
VerifyExit 'BUILD-INSTALLER.cmd'
$setup = Join-Path $root "artifacts/installer/output/SafeCCleaner-Setup-$version.exe"
if (-not (Test-Path -LiteralPath $setup)) {
  $found = @(Get-ChildItem (Join-Path $root 'artifacts/installer/output') -Filter '*.exe' -File -ErrorAction SilentlyContinue)
  if ($found.Count -ne 1) { throw 'Expected exactly one installer executable.' }
  $setup = $found[0].FullName
}
if ((Get-Item -LiteralPath $setup).LastWriteTime -lt $buildStarted) { throw 'Installer is stale: builder did not produce a new output.' }
$hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $setup).Hash
Write-Host "Installer: $setup"
Write-Host "SHA256: $hash"
Write-Host 'Install and test this setup on Windows before publishing.' -ForegroundColor Yellow
$confirm = Read-Host 'Type PUBLISH after manual installer and regression tests'
if ($confirm -cne 'PUBLISH') { Write-Host 'No tag or release created.'; exit 0 }
& git tag -a $tag -m "Safe C: Cleaner $tag"; VerifyExit 'git tag'
& git push origin $tag; VerifyExit 'git push tag'
$notes = Join-Path $root 'Info/RELEASE-NOTES-0.6.0.md'
if (Test-Path $notes) {
  & gh release create $tag $setup --repo CactusDex/Safe-C-Cleaner --title "Safe C: Cleaner $tag" --notes-file $notes --verify-tag
} else {
  & gh release create $tag $setup --repo CactusDex/Safe-C-Cleaner --title "Safe C: Cleaner $tag" --generate-notes --verify-tag
}
VerifyExit 'gh release create'
Write-Host "Release published: https://github.com/CactusDex/Safe-C-Cleaner/releases/tag/$tag" -ForegroundColor Green
