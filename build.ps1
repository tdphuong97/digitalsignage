# Builds Signage Vien Ba: ffmpeg copy, tests, the app with its user guide, and the installer.
#   .\build.ps1                 full build, installer in dist\
#   .\build.ps1 -SkipTests      skip the unit tests
#   .\build.ps1 -SkipInstaller  stop after dist\app
#   .\build.ps1 -Publish        also publish a GitHub release (build\publish.ps1); installed copies offer it as an update
# Needs Python 3 and the guidebook builder (GUIDEBOOK_PY, default ~\.claude\skills\guidebook\scripts\guidebook.py)
# for HuongDan.html, the user guide the app opens from its "Huong dan" button.
# E2E tests against a local Worker are separate: tests\e2e\start-local-worker.ps1, then
#   $env:SIGNAGE_E2E_URL='http://127.0.0.1:8787'; $env:SIGNAGE_E2E_KEY='test-key'; dotnet test tests\SignageVienBa.Tests
param([switch]$SkipTests, [switch]$SkipInstaller, [switch]$Publish)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot

# ---------- ffmpeg (shared build, reused from VDM) ----------
$ffDir = Join-Path $PSScriptRoot 'deps\ffmpeg'
if (-not (Test-Path (Join-Path $ffDir 'ffmpeg.exe'))) {
    $candidates = @('C:\Claude\CaNhan\IDM\deps\tools') +
        @(Get-ChildItem 'C:\Claude\CaNhan\IDM\deps\cache\ffmpeg\*\bin' -Directory -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName })
    $src = $candidates | Where-Object {
        (Test-Path (Join-Path $_ 'ffmpeg.exe')) -and (Test-Path (Join-Path $_ 'ffprobe.exe'))
    } | Select-Object -First 1
    if (-not $src) { throw 'ffmpeg not found: put ffmpeg.exe, ffprobe.exe and the DLLs of a shared build into deps\ffmpeg' }
    New-Item -ItemType Directory -Force $ffDir | Out-Null
    Get-ChildItem $src -File | Where-Object {
        $_.Name -eq 'ffmpeg.exe' -or $_.Name -eq 'ffprobe.exe' -or
        $_.Name -match '^(avcodec|avformat|avfilter|avutil|avdevice|swscale|swresample|postproc)-\d+\.dll$'
    } | Copy-Item -Destination $ffDir
    Write-Host "ffmpeg copied from $src"
}
# (collect all output first: Select-Object -First would stop ffmpeg early and leave a non-zero exit code)
$ffOut = @(& (Join-Path $ffDir 'ffmpeg.exe') -hide_banner -version)
if ($LASTEXITCODE) { throw "ffmpeg.exe in $ffDir does not run" }
$ffVersion = $ffOut[0]
& (Join-Path $ffDir 'ffprobe.exe') -hide_banner -version | Out-Null
if ($LASTEXITCODE) { throw "ffprobe.exe in $ffDir does not run" }
Write-Host "ffmpeg OK: $ffVersion"

# ---------- tests (unit + real ffmpeg; E2E needs a local Worker, see above) ----------
if (-not $SkipTests) {
    $env:SIGNAGE_FFMPEG_DIR = $ffDir
    dotnet test tests\SignageVienBa.Tests -c Release --filter "Category!=E2E"
    if ($LASTEXITCODE) { throw 'Tests failed.' }
}

# ---------- app (self-contained single file + ffmpeg folder) ----------
$app = Join-Path $PSScriptRoot 'dist\app'
if (Test-Path $app) { Remove-Item -Recurse -Force $app }
dotnet publish src\SignageVienBa.App -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o $app
if ($LASTEXITCODE) { throw 'Publish failed.' }
Get-ChildItem $app -Recurse -Include *.pdb, *.xml | Remove-Item
Copy-Item $ffDir (Join-Path $app 'ffmpeg') -Recurse
if (-not (Test-Path (Join-Path $app 'SignageVienBa.exe'))) { throw 'dist\app\SignageVienBa.exe is missing after publish.' }

# ---------- user guide: docs\ without the IT pages (build\make-guide.py) ----------
if (-not (Get-Command python -ErrorAction SilentlyContinue)) { throw 'Python 3 is missing (needed to build the user guide).' }
$guidebook = if ($env:GUIDEBOOK_PY) { $env:GUIDEBOOK_PY } else { Join-Path $HOME '.claude\skills\guidebook\scripts\guidebook.py' }
python build\make-guide.py $guidebook (Join-Path $app 'HuongDan.html')
if ($LASTEXITCODE) { throw 'User guide build failed (build\make-guide.py); set GUIDEBOOK_PY if guidebook.py is elsewhere.' }
"App: {0:N0} MB in {1}" -f ((Get-ChildItem $app -Recurse | Measure-Object Length -Sum).Sum / 1MB), $app

if ($SkipInstaller) { return }

# ---------- installer ----------
$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 is missing: winget install JRSoftware.InnoSetup' }

$version = (Select-Xml -Path src\SignageVienBa.App\SignageVienBa.App.csproj -XPath '//Version').Node.InnerText
if (-not $version) { $version = '1.0.0' }
$env:SIGNAGE_VERSION = $version
& $iscc /Q build\SignageVienBa.iss
if ($LASTEXITCODE) { throw 'Installer build failed.' }
$setup = "dist\SignageVienBa-Setup-$version.exe"
if (-not (Test-Path $setup)) { throw "$setup was not created." }

# keep the newest installer plus the two previous versions
$old = Get-ChildItem dist -Filter 'SignageVienBa-Setup-*.exe' | Where-Object {
    $_.BaseName -match '^SignageVienBa-Setup-(\d+(\.\d+){1,3})$'
} | Sort-Object { [version]($_.BaseName -replace '^SignageVienBa-Setup-', '') } -Descending | Select-Object -Skip 3
foreach ($f in $old) { Write-Host "Removing old build $($f.Name)"; Remove-Item -LiteralPath $f.FullName }

Get-Item $setup | Format-Table Name, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } } -AutoSize

if ($Publish) { & .\build\publish.ps1 }
