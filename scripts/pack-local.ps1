#!/usr/bin/env pwsh
# Packs QuroGrid libraries into the local NuGet feed consumed by QuroERP.
# Usage: pwsh scripts/pack-local.ps1 -Version 0.3.0-alpha.2 [-Feed /WRK/_nuget]
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$Feed = $(if ($env:QURO_NUGET_FEED) { $env:QURO_NUGET_FEED } else { "/WRK/_nuget" })
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$projects = @(
    "src/Quro.Grid.Data/Quro.Grid.Data.csproj",
    "src/Quro.Grid.Definition/Quro.Grid.Definition.csproj",
    "src/Quro.Grid/Quro.Grid.csproj",
    "src/Quro.UI/Quro.UI.csproj",
    "src/Quro.Grid.DefLint/Quro.Grid.DefLint.csproj"
)
# Quro.Grid.Definition is planned (I-02); pack only the projects that exist.
$projects = @($projects | Where-Object { Test-Path (Join-Path $root $_) })

New-Item -ItemType Directory -Force -Path $Feed | Out-Null

# Refuse to overwrite an existing version: consumers cache packages by version.
foreach ($p in $projects) {
    $id = [IO.Path]::GetFileNameWithoutExtension($p)
    $existing = Join-Path $Feed ("{0}.{1}.nupkg" -f $id, $Version)
    if (Test-Path $existing) { throw "Version $Version of $id already exists in $Feed. Bump the version." }
}

dotnet test (Join-Path $root "QuroGrid.slnx") -c Release
if ($LASTEXITCODE -ne 0) { throw "Tests failed. Nothing was packed." }

foreach ($p in $projects) {
    dotnet pack (Join-Path $root $p) -c Release -o $Feed -p:Version=$Version -p:ContinuousIntegrationBuild=true
    if ($LASTEXITCODE -ne 0) { throw "Pack failed: $p" }
}

Write-Host "Packed to $Feed :"
Get-ChildItem $Feed -Filter "Quro.Grid*.$Version.nupkg" | ForEach-Object { Write-Host "  $($_.Name)" }
