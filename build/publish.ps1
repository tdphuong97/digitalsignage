# Publishes the version built in dist\ as a GitHub release; every installed Signage Vien Ba finds it
# through the release's latest.json and offers it as an update.
#   .\build.ps1 -Publish            build, then publish
#   .\build\publish.ps1             publish what is already built (for example after a failed upload)
#   .\build\publish.ps1 -DryRun     write dist\latest.json and show what would be published, change nothing online
# Before publishing: bump <Version> in src\SignageVienBa.App\SignageVienBa.App.csproj, add a "## <version>"
# section to CHANGELOG.md, build, commit and push. The release tag is made from the pushed commit.
param([switch]$DryRun)
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot)
$version = (Select-Xml -Path src\SignageVienBa.App\SignageVienBa.App.csproj -XPath '//Version').Node.InnerText
if (-not $version) { throw 'No <Version> in src\SignageVienBa.App\SignageVienBa.App.csproj.' }
$setup = "dist\SignageVienBa-Setup-$version.exe"
if (-not (Test-Path $setup)) { throw "$setup is missing: run .\build.ps1 first." }

$repo = [regex]::Match((Get-Content src\SignageVienBa.Core\Updater.cs -Raw), 'const string Repo = "([^"]+)"').Groups[1].Value
if (-not $repo -or $repo -like 'OWNER/*') { throw 'Set Updater.Repo in src\SignageVienBa.Core\Updater.cs to the GitHub repository first.' }
$gh = (Get-Command gh -ErrorAction SilentlyContinue).Source
if (-not $gh) { $gh = "$env:ProgramFiles\GitHub CLI\gh.exe" }
if (-not (Test-Path $gh)) { throw 'GitHub CLI is missing: winget install GitHub.cli, then gh auth login' }

# The notes are this version's section of CHANGELOG.md, one change per line.
$section = [regex]::Match((Get-Content CHANGELOG.md -Raw -Encoding utf8), '(?ms)^## ' + [regex]::Escape($version) + '\s*$(.*?)(?=^## |\z)').Groups[1].Value
$notes = (($section -split "`r?`n") | Where-Object { $_.Trim() }) -join "`n"
if (-not $notes) { throw "CHANGELOG.md has no section for $version." }

# ---------- what gets tagged must be what was committed and pushed ----------
# (Windows PowerShell 5.1 turns a native tool's stderr into a terminating error under 'Stop', and git/gh
#  write their "not found" answers there, so these checks run under 'Continue'.)
$ErrorActionPreference = 'Continue'
$problems = @()
$head = $null
$git = Get-Command git -ErrorAction SilentlyContinue
if (-not $git) { $problems += 'git is missing.' }
elseif (-not (Test-Path .git)) { $problems += 'This folder is not a git repository yet.' }
else {
    $dirty = @(& git status --porcelain)
    if ($dirty.Count) { $problems += "Uncommitted changes:`n  " + ($dirty -join "`n  ") }
    & git fetch --quiet origin 2>$null
    $head = (& git rev-parse HEAD).Trim()
    $branch = (& git rev-parse --abbrev-ref HEAD).Trim()
    $remote = (& git rev-parse "origin/$branch" 2>$null)
    if ($LASTEXITCODE -or -not $remote) { $problems += "Branch $branch is not on origin: git push -u origin $branch" }
    elseif ($remote.Trim() -ne $head) { $problems += "Branch $branch has commits that are not pushed (or is behind origin): git push" }
}
& $gh release view "v$version" --repo $repo *> $null
if ($LASTEXITCODE -eq 0) { $problems += "Release v$version already exists on $repo. Bump <Version> in the csproj for a new release." }

$ErrorActionPreference = 'Stop'

if ($problems.Count -and -not $DryRun) { throw ("Not publishing:`n- " + ($problems -join "`n- ")) }

function Get-AssetInfo($file) {
    [ordered]@{
        url    = "https://github.com/$repo/releases/download/v$version/$(Split-Path $file -Leaf)"
        size   = (Get-Item $file).Length
        sha256 = (Get-FileHash $file -Algorithm SHA256).Hash
    }
}
$latest = [ordered]@{
    version = $version
    date    = (Get-Date -Format 'yyyy-MM-dd')
    notes   = $notes
    page    = "https://github.com/$repo/releases/tag/v$version"
    windows = Get-AssetInfo $setup
}
[IO.File]::WriteAllText((Join-Path (Get-Location) 'dist\latest.json'), ($latest | ConvertTo-Json -Depth 4), (New-Object Text.UTF8Encoding $false))

$body = @"
## Có gì mới
$notes

## Cài đặt
Lần đầu: tải **SignageVienBa-Setup-$version.exe** ở mục Assets bên dưới rồi chạy.
Đã cài phần mềm: không cần tải, phần mềm tự báo bản mới, bấm **Cập nhật ngay** là xong.

## Kiểm tra file
SHA-256 của SignageVienBa-Setup-${version}.exe:
``$($latest.windows.sha256)``
"@
$title = "Signage Viên Ba $version"

if ($DryRun) {
    Write-Host 'Dry run: nothing was published.'
    if ($problems.Count) { Write-Host ("A real publish would stop here:`n- " + ($problems -join "`n- ")) }
    Write-Host "Would run: gh release create v$version $setup dist\latest.json --repo $repo --title `"$title`" --notes <body below>"
    Write-Host '---- dist\latest.json ----'
    Get-Content dist\latest.json -Encoding utf8 | Write-Host
    Write-Host '---- release notes ----'
    Write-Host $body
    return
}

& $gh release create "v$version" $setup 'dist\latest.json' --repo $repo --target $head --title $title --notes $body
if ($LASTEXITCODE) { throw 'Publishing the release failed.' }
"Published https://github.com/$repo/releases/tag/v$version"
