<#
  Build and launch VisNav Accessibility.

  Usage:
    ./run.ps1            # Debug build
    ./run.ps1 -Release   # Release build

  Requires the .NET 8 SDK on PATH (https://dotnet.microsoft.com/download/dotnet/8.0).
  VisNav runs in the system tray; only one instance runs at a time.
#>
param([switch]$Release)

$ErrorActionPreference = 'Stop'
$config = if ($Release) { 'Release' } else { 'Debug' }
$proj = Join-Path $PSScriptRoot 'src/VisNav.App/VisNav.App.csproj'

Write-Host "Building and launching VisNav Accessibility ($config)..." -ForegroundColor Cyan
dotnet run --project $proj -c $config
