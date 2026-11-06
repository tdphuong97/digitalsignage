# Stops the local Worker started by start-local-worker.ps1: only wrangler's node/workerd/esbuild processes that run
# from %LOCALAPPDATA%\Temp\signage-worker-e2e, plus the PowerShell window running start-local-worker.ps1.
# Shells that merely mention the folder name in their command line are left alone.
$workerNames = 'node.exe', 'workerd.exe', 'esbuild.exe'
$shellNames = 'powershell.exe', 'pwsh.exe'

$procs = Get-CimInstance Win32_Process | Where-Object {
    $cmd = [string]$_.CommandLine
    $exe = [string]$_.ExecutablePath
    (($workerNames -contains $_.Name) -and ($cmd -like '*signage-worker-e2e*' -or $exe -like '*signage-worker-e2e*')) -or
    (($shellNames -contains $_.Name) -and $cmd -match '-File\s+"?[^"]*start-local-worker\.ps1')
} | Where-Object { $_.ProcessId -ne $PID }

if (-not $procs) { Write-Host 'Local Worker is not running.'; return }
foreach ($p in $procs) {
    Write-Host ("Stopping {0} (PID {1})" -f $p.Name, $p.ProcessId)
    Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue
}
