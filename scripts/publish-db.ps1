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
