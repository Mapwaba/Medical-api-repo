<#
.SYNOPSIS
  Stops all LandaDoc dotnet services started by run-all.ps1 (by port),
  and optionally the docker infra too.

.PARAMETER IncludeInfra
  Also run `docker compose down` to stop postgres/mssql/redis/rabbitmq/minio.

.EXAMPLE
  .\stop-all.ps1
  .\stop-all.ps1 -IncludeInfra
#>
param(
    [switch]$IncludeInfra
)

$ErrorActionPreference = "Continue"
$repoRoot = $PSScriptRoot

$ports = 5138, 5001, 5088, 5114, 5286, 5115, 5078, 5198, 5253, 5141, 5299, 5003, 5500

foreach ($port in $ports) {
    $conns = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
    foreach ($conn in $conns) {
        $procId = $conn.OwningProcess
        $proc = Get-Process -Id $procId -ErrorAction SilentlyContinue
        if ($proc) {
            Write-Host "Stopping port $port (pid $procId, $($proc.ProcessName))..." -ForegroundColor Yellow
            Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
        }
    }
}

if ($IncludeInfra) {
    Write-Host "Stopping docker infra..." -ForegroundColor Cyan
    Push-Location $repoRoot
    docker compose down
    Pop-Location
}

Write-Host "Done." -ForegroundColor Cyan
