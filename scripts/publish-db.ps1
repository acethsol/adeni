#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Build Adeni.Database (.dacpac) and publish with SqlPackage (pre/post deploy included).

.PARAMETER ConnectionString
  Target SQL Server connection string. Defaults to appsettings.Development.json AdeniDb.

.PARAMETER CreateNewDatabase
  Create the database if it does not exist.
#>
param(
    [string] $ConnectionString = "",
    [switch] $CreateNewDatabase
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

Write-Host "Restoring local tools (SqlPackage)..."
dotnet tool restore | Out-Host

$proj = "db/Adeni.Database/Adeni.Database.sqlproj"
Write-Host "Building $proj ..."
dotnet build $proj -c Release | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Database project build failed." }

$dacpac = Join-Path $root "db/Adeni.Database/bin/Release/Adeni.Database.dacpac"
if (-not (Test-Path $dacpac)) {
    $dacpac = Get-ChildItem (Join-Path $root "db/Adeni.Database/bin") -Recurse -Filter "*.dacpac" |
        Select-Object -First 1 -ExpandProperty FullName
}
if (-not $dacpac -or -not (Test-Path $dacpac)) {
    throw "dacpac not found under db/Adeni.Database/bin"
}

$migrationScript = Join-Path $root "db/Adeni.Database/PreDeployment/RenamePaymentsSchemaToFinance.sql"
if (Test-Path $migrationScript) {
    $pairs = @{}
    foreach ($part in $ConnectionString -split ';') {
        if ($part -match '^\s*(.+?)=(.*)$') {
            $pairs[$matches[1].Trim()] = $matches[2].Trim()
        }
    }
    $server = $pairs['Server']
    $database = $pairs['Database']
    if ([string]::IsNullOrWhiteSpace($server) -or [string]::IsNullOrWhiteSpace($database)) {
        throw "Could not parse Server/Database from connection string for pre-publish migration."
    }
    $sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
    if (-not $sqlcmd) {
        throw "sqlcmd is required for pre-publish schema migration. Install SQL Server command-line tools or run RenamePaymentsSchemaToFinance.sql manually."
    }
    Write-Host "Pre-publish migration (payments -> finance if needed)..."
    $sqlcmdArgs = @("-S", $server, "-d", $database, "-C", "-b", "-i", $migrationScript)
    if ($pairs.ContainsKey('User Id') -and $pairs.ContainsKey('Password')) {
        $sqlcmdArgs += @("-U", $pairs['User Id'], "-P", $pairs['Password'])
    }
    else {
        $sqlcmdArgs += "-E"
    }
    & sqlcmd @sqlcmdArgs | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Pre-publish migration failed." }
}

$create = if ($CreateNewDatabase) { "True" } else { "False" }
Write-Host "Publishing $dacpac ..."
dotnet tool run sqlpackage `
    /Action:Publish `
    /SourceFile:"$dacpac" `
    /TargetConnectionString:"$ConnectionString" `
    /p:CreateNewDatabase=$create `
    /p:BlockOnPossibleDataLoss=True

if ($LASTEXITCODE -ne 0) { throw "SqlPackage publish failed." }
Write-Host "Publish complete."
