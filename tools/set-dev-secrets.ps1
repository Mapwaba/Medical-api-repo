# Sets the local development secrets for every backend service, using dotnet user-secrets
# (stored in your user profile, never in the repo). Run it once after cloning, or again to change
# the local token key — everyone signed in locally is then signed out.
#
#   .\tools\set-dev-secrets.ps1
#   .\tools\set-dev-secrets.ps1 -ConnectionString "Host=localhost;Port=5433;Database=landadoc_db;Username=postgres;Password=..."
#
# -JwtSecret     the token key shared by all services; a new random one is made if left out
# -ConnectionString  the Postgres connection, set for every service that uses one; left alone if out
#
# Servers don't use this: they read Jwt__Secret and ConnectionStrings__Conx from the environment.
param(
    [string]$JwtSecret,
    [string]$ConnectionString
)

$ErrorActionPreference = "Stop"
$services = "Admin", "Appointment", "Availability", "Document", "Identity", "Notification", "Payment", "Review", "Search"
$root = Join-Path $PSScriptRoot "..\src\Services"

if (-not $JwtSecret) {
    $bytes = New-Object byte[] 32
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $JwtSecret = [Convert]::ToBase64String($bytes)
}

foreach ($service in $services) {
    $dir = Join-Path $root $service
    dotnet user-secrets set "Jwt:Secret" $JwtSecret --project $dir | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Couldn't set Jwt:Secret for $service" }

    # Only services whose appsettings.json declares a Conx connection use Postgres
    $usesDb = (Get-Content (Join-Path $dir "appsettings.json") -Raw) -match '"Conx"'
    if ($ConnectionString -and $usesDb) {
        dotnet user-secrets set "ConnectionStrings:Conx" $ConnectionString --project $dir | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Couldn't set ConnectionStrings:Conx for $service" }
    }
    Write-Host "$service`: secrets set"
}
