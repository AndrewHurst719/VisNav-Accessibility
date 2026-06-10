<#
  Publishes a SELF-CONTAINED Windows build of VisNav (the end user needs no .NET install)
  and zips it for a GitHub Release.

  Output:
    publish/VisNav/VisNav.exe   (one self-contained executable + HOW TO RUN.txt)
    dist/VisNav-win-x64.zip     (the zip to attach to a release)

  Usage:  ./publish.ps1
#>
param([string]$Runtime = 'win-x64')

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$proj = Join-Path $root 'src/VisNav.App/VisNav.App.csproj'
$outDir = Join-Path $root 'publish/VisNav'
$distDir = Join-Path $root 'dist'
$zip = Join-Path $distDir "VisNav-$Runtime.zip"

if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }

Write-Host "Publishing self-contained $Runtime build..." -ForegroundColor Cyan
dotnet publish $proj -c Release -r $Runtime --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -o $outDir

# A plain-English readme that ships inside the zip.
$howto = @"
VisNav Accessibility - how to run
=================================

1. Double-click  VisNav.exe

2. The first time, Windows may show a blue "Windows protected your PC" box
   (because the app isn't signed). Click "More info", then "Run anyway".

3. VisNav opens a Settings window and puts a small crosshair icon in your
   system tray (bottom-right of the screen, maybe under the ^ arrow).

Hotkeys (all changeable in Settings):
   Magnifier on/off ........ Ctrl + Shift + F   (then hold Shift + scroll to zoom)
   Read a region ........... Ctrl + Shift + Q   (drag a box; it is read aloud)
   Scan & hover to read .... Ctrl + Shift + R
   Pause / resume speech ... Ctrl + Shift + P
   Stop speech ............. Ctrl + Shift + X

To quit: right-click the tray icon and choose Exit.

No installation needed. Settings are saved in %AppData%\VisNav. Delete the
folder to remove everything.
"@
Set-Content -Path (Join-Path $outDir 'HOW TO RUN.txt') -Value $howto -Encoding utf8

New-Item -ItemType Directory -Force -Path $distDir | Out-Null
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $outDir '*') -DestinationPath $zip

$sizeMb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "Published: $outDir" -ForegroundColor Green
Write-Host "Zip:       $zip  ($sizeMb MB)" -ForegroundColor Green
