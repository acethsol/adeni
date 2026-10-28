#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Seeds (or removes with -Cleanup) sizeable Lekki Cuts demo data for every active branch:
  ~30 staff per branch, customers, bookings, reviews, messages, queue tickets, calendar blocks,
  plus a published wellness consultation intake form (Barbering, Braids, Locs).
.DESCRIPTION
  Idempotent. Uses the Development connection string (SQL login) unless -ConnectionString is passed.
  Requires sqlcmd on PATH and the API to have run once in Development (base tenant + branches).
  Run ./scripts/publish-db.ps1 first when the intake schema is missing.
#>
param(
    [string] $ConnectionString = "",
    [switch] $Cleanup
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $root

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $settingsPath = Join-Path $root "src/Adeni.Api/appsettings.Development.json"
    $json = Get-Content $settingsPath -Raw | ConvertFrom-Json
    $ConnectionString = $json.ConnectionStrings.AdeniDb
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    throw "Connection string is empty. Pass -ConnectionString or set ConnectionStrings:AdeniDb."
}

function Get-ConnPart([string[]]$keys) {
    foreach ($segment in ($ConnectionString -split ';')) {
        foreach ($key in $keys) {
            if ($segment -match "^\s*$key\s*=\s*(.+)\s*$") {
                return $Matches[1].Trim()
            }
        }
    }
    return $null
}

$server = Get-ConnPart @("Server", "Data Source")
$database = Get-ConnPart @("Database", "Initial Catalog")
$user = Get-ConnPart @("User Id", "UID")
$password = Get-ConnPart @("Password", "PWD")
if (-not $server -or -not $database) {
    throw "Could not parse Server/Database from connection string."
}

$sqlFiles = if ($Cleanup) {
    @(
        "seed-lekki-cuts-intake-consultation-cleanup.sql",
        "seed-lekki-cuts-demo-cleanup.sql"
    )
} else {
    @(
        "seed-lekki-cuts-demo.sql",
        "seed-lekki-cuts-intake-consultation.sql"
    )
}

function Invoke-SeedSql([string] $fileName) {
    $sqlPath = Join-Path $root "db/scripts/$fileName"
    # -I = QUOTED_IDENTIFIER ON (filtered indexes); -C trusts the dev container certificate
    $args = @("-S", $server, "-d", $database, "-i", $sqlPath, "-b", "-I", "-C", "-W")
    if ($user) { $args += @("-U", $user, "-P", $password) } else { $args += "-E" }
    Write-Host "Running $fileName on $database @ $server ..."
    & sqlcmd @args
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed on $fileName with exit code $LASTEXITCODE" }
}

foreach ($fileName in $sqlFiles) {
    Invoke-SeedSql $fileName
}

Write-Host "Done. Restart the API only if you need to clear Redis-cached profile/discovery data."
