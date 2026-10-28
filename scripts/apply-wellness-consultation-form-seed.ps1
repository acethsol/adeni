#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Seeds the Body Sculpt wellness consultation intake form (published) for a dev wellness tenant.
.DESCRIPTION
  Idempotent. Inserts intake.form_templates + published v1, fields, choices, and service bindings.
  Default tenant: ikoyi-massage-studio (Ikoyi Massage Studio). Override with -TenantSlug.
  Requires sqlcmd and DevelopmentDataSeeder base data (run API once in Development).
#>
param(
    [string] $ConnectionString = "",
    [string] $TenantSlug = "ikoyi-massage-studio"
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

$sqlPath = Join-Path $root "db/scripts/seed-wellness-consultation-form.sql"
$args = @("-S", $server, "-d", $database, "-i", $sqlPath, "-b", "-I", "-C", "-W", "-v", "TenantSlug=$TenantSlug")
if ($user) { $args += @("-U", $user, "-P", $password) } else { $args += "-E" }

Write-Host "Seeding wellness consultation intake for slug '$TenantSlug' on $database @ $server ..."
& sqlcmd @args
if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed with exit code $LASTEXITCODE" }
Write-Host "Done. Portal: Services -> Consultation forms, or service detail -> Consultation form tab."
