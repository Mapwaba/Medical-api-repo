<#
.SYNOPSIS
  Starts docker infra + all LandaDoc backend services for local development.

.DESCRIPTION
  Brings up postgres/mssql/redis/rabbitmq/minio via docker compose, then
  launches each backend service in its own PowerShell window (so you can see
  its console output live) while also teeing that output to a log file under
  .run-logs\ for later inspection.

.PARAMETER IncludeGatewayAndFrontends
  Also start the Gateway and the two Blazor frontends. Off by default since
  the Gateway currently has no routes wired up.

.EXAMPLE
  .\run-all.ps1
  .\run-all.ps1 -IncludeGatewayAndFrontends
#>
param(
    [switch]$IncludeGatewayAndFrontends
)

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot

$services = @(
    @{ Name = "Identity";     Path = "src\Services\Identity\LandaDoc.Identity.csproj";         Port = 5138 }
    @{ Name = "Appointment";  Path = "src\Services\Appointment\LandaDoc.Appointment.csproj";    Port = 5001 }
    @{ Name = "Availability"; Path = "src\Services\Availability\LandaDoc.Availability.csproj";  Port = 5088 }
    @{ Name = "Payment";      Path = "src\Services\Payment\LandaDoc.Payment.csproj";            Port = 5114 }
    @{ Name = "Notification"; Path = "src\Services\Notification\LandaDoc.Notification.csproj";  Port = 5286 }
    @{ Name = "Admin";        Path = "src\Services\Admin\LandaDoc.Admin.csproj";                Port = 5115 }
    @{ Name = "Search";       Path = "src\Services\Search\LandaDoc.Search.csproj";              Port = 5078 }
    @{ Name = "Document";     Path = "src\Services\Document\LandaDoc.Document.csproj";          Port = 5198 }
    @{ Name = "Review";       Path = "src\Services\Review\LandaDoc.Review.csproj";              Port = 5253 }
)

if ($IncludeGatewayAndFrontends) {
    $services += @{ Name = "Gateway"; Path = "src\LandaDoc.Gateway\LandaDoc.Gateway.csproj"; Port = 5141 }
    $services += @{ Name = "Patient"; Path = "src\Frontend\Patient\LandaDoc.Patient.csproj"; Port = 5299 }
    $services += @{ Name = "Doctor";  Path = "src\Frontend\Doctor\LandaDoc.Doctor.csproj";   Port = 5003 }
    $services += @{ Name = "AdminApp"; Path = "src\Frontend\Admin\LandaDoc.AdminApp.csproj"; Port = 5500 }
}

Write-Host "Starting docker infra (postgres, mssql, redis, rabbitmq, minio)..." -ForegroundColor Cyan
Push-Location $repoRoot
docker compose up -d
Pop-Location

# Build once, up front. The per-service windows below launch via `dotnet run` seconds apart;
# several of them reference shared projects (LandaDoc.Shared, LandaDoc.Frontend.Shared), and
# without this, concurrent `dotnet run` builds race to write the same obj/ dll and fail with
# CS2012 "cannot open ... for writing". Building here first means each service's own
# `dotnet run` finds everything already up to date and just starts.
Write-Host "Building solution..." -ForegroundColor Cyan
dotnet build "$repoRoot\LandaDoc.sln" | Out-Host
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed - fix the errors above before starting services." -ForegroundColor Red
    exit 1
}

Write-Host "Waiting 15s for infra to settle..." -ForegroundColor Cyan
Start-Sleep -Seconds 15

$logDir = Join-Path $repoRoot ".run-logs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

foreach ($svc in $services) {
    $projectPath = Join-Path $repoRoot $svc.Path
    $logPath = Join-Path $logDir "$($svc.Name).log"
    $cmd = "Set-Location '$repoRoot'; `$env:ASPNETCORE_ENVIRONMENT='Development'; " +
           "Write-Host '=== $($svc.Name) (port $($svc.Port)) ===' -ForegroundColor Cyan; " +
           "dotnet run --project '$projectPath' 2>&1 | Tee-Object -FilePath '$logPath'"
    Write-Host "Starting $($svc.Name) on port $($svc.Port)..." -ForegroundColor Green
    Start-Process powershell -ArgumentList "-NoExit", "-Command", $cmd
    Start-Sleep -Seconds 2
}

Write-Host ""
Write-Host "All services launching in separate windows. Logs also written to $logDir" -ForegroundColor Cyan
Write-Host "Close a window (or Ctrl+C inside it) to stop that service, or run .\stop-all.ps1 to stop them all." -ForegroundColor Yellow
