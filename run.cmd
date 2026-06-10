@echo off
REM Build and launch VisNav Accessibility (Debug). Double-click or run from a terminal.
REM Requires the .NET 8 SDK on PATH.
dotnet run --project "%~dp0src\VisNav.App\VisNav.App.csproj" -c Debug
