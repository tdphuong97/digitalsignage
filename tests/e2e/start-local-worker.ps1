# Runs the real worker/src/worker.js on this machine (wrangler dev, local R2 simulation) for E2E tests.
# Never touches the production Worker or bucket.
#
#   .\start-local-worker.ps1            # foreground; Ctrl+C to stop
#   .\start-local-worker.ps1 -Reset     # also wipe the local R2 data first
#   .\stop-local-worker.ps1             # stop it from another window / after a background start
#
# Data lives in %LOCALAPPDATA%\Temp\signage-worker-e2e.
# Then: SIGNAGE_E2E_URL=http://127.0.0.1:8787  SIGNAGE_E2E_KEY=test-key
param(
    [int]$Port = 8787,
    [string]$ApiKey = 'test-key',
    [switch]$Reset
)

$workerSrc = Resolve-Path (Join-Path $PSScriptRoot '..\..\..\worker')
# %TEMP% can be an 8.3 short path (C:\Users\PHUONG~1.TRA\...) that the PowerShell file provider
# rejects ("An object at the specified path ... does not exist"); %LOCALAPPDATA%\Temp is the same folder, long form.
$tempRoot = Join-Path $env:LOCALAPPDATA 'Temp'
if (-not (Test-Path -LiteralPath $tempRoot)) { $tempRoot = $env:TEMP }
$dir = Join-Path $tempRoot 'signage-worker-e2e'

if ($Reset -and (Test-Path (Join-Path $dir '.wrangler'))) {
    Remove-Item -Recurse -Force (Join-Path $dir '.wrangler')
    Write-Host 'Local R2 data wiped.'
}

New-Item -ItemType Directory -Force (Join-Path $dir 'src') | Out-Null
Copy-Item (Join-Path $workerSrc 'src\worker.js') (Join-Path $dir 'src\worker.js') -Force
Copy-Item (Join-Path $workerSrc 'wrangler.toml') $dir -Force
Copy-Item (Join-Path $workerSrc 'index.html') $dir -Force

Set-Location -LiteralPath $dir
if ((Get-Location).ProviderPath -ne (Get-Item -LiteralPath $dir).FullName) { Write-Host "Could not enter $dir"; exit 1 }
$env:WRANGLER_SEND_METRICS = 'false'

if (-not (Test-Path (Join-Path $dir 'node_modules\wrangler'))) {
    Set-Content -Path (Join-Path $dir 'package.json') -Value '{ "name": "signage-worker-e2e", "private": true }' -Encoding ASCII
    Write-Host 'Installing wrangler@4 (first run only)...'
    & npm install --no-audit --no-fund --loglevel=error wrangler@4
    if ($LASTEXITCODE) { Write-Host 'npm install failed.'; exit 1 }
}

Set-Content -Path (Join-Path $dir '.dev.vars') -Value "API_KEY=$ApiKey" -Encoding ASCII

& npx wrangler r2 object put signage-vienba/index.html --file index.html --content-type text/html --local
if ($LASTEXITCODE) { Write-Host 'Could not put index.html into the local R2 bucket.'; exit 1 }

Write-Host "Local Worker: http://127.0.0.1:$Port  (API key: $ApiKey)"
& npx wrangler dev --local --port $Port --ip 127.0.0.1
