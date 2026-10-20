#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Inserts idempotent devseed commerce + SaaS rows for admin transaction browse/search tests.
#>
param(
    [string] $ConnectionString = ""
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

function Get-ConnPart([string]$key) {
    foreach ($segment in ($ConnectionString -split ';')) {
        if ($segment -match "^\s*$key\s*=\s*(.+)\s*$") {
            return $Matches[1].Trim()
        }
    }
    return $null
}

$server = Get-ConnPart "Server"
$database = Get-ConnPart "Database"
if (-not $server -or -not $database) {
    throw "Could not parse Server/Database from connection string."
}

$sqlPath = Join-Path $root "db/scripts/admin-transactions-dev-seed.sql"
Write-Host "Seeding admin transaction samples on $database @ $server ..."
sqlcmd -S $server -d $database -E -i $sqlPath -b
Write-Host "Done."
