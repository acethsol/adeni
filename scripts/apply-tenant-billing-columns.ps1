#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Idempotent tenant billing columns on an existing adeni database (sqlcmd).

  Prefer ./scripts/publish-db.ps1 when the full dacpac model should win.
  Use this when you only need billing columns on a DB that already matches an older model.

.PARAMETER ConnectionString
  Optional. Defaults to ConnectionStrings:AdeniDb from appsettings.Development.json.
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
$user = Get-ConnPart "User Id"
if (-not $user) { $user = Get-ConnPart "User ID" }
$password = Get-ConnPart "Password"

if (-not $server -or -not $database) {
    throw "Could not parse Server/Database from connection string."
}

$sqlPath = Join-Path $root "db/scripts/tenant-billing-columns.sql"
if (-not (Test-Path $sqlPath)) {
    throw "Script not found: $sqlPath"
}

Write-Host "Applying tenant billing columns to $database on $server ..."
$args = @("-S", $server, "-d", $database, "-C", "-i", $sqlPath)
if ($user -and $password) {
    $args = @("-S", $server, "-U", $user, "-P", $password, "-d", $database, "-C", "-i", $sqlPath)
}

& sqlcmd @args
if ($LASTEXITCODE -ne 0) {
    throw "sqlcmd failed with exit code $LASTEXITCODE"
}

Write-Host "tenant-billing-columns.sql applied (idempotent)."
