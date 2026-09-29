@echo off
chcp 65001 >nul
title KingR9 Tools - Single-file Build
cd /d "%~dp0"

where dotnet >nul 2>&1
if errorlevel 1 (
  echo [X] ไม่พบ .NET 8 SDK
  echo     ติดตั้งจาก https://dotnet.microsoft.com/download/dotnet/8.0
  exit /b 1
)

echo Publishing single-file build...
dotnet publish KingR9Tools.csproj -c Release -r win-x64 --self-contained true ^
  /p:PublishSingleFile=true ^
  /p:IncludeNativeLibrariesForSelfExtract=true ^
  /p:DebugType=None /p:DebugSymbols=false ^
  -o "%~dp0Release\SingleFile"
if errorlevel 1 exit /b 1

del /q "%~dp0Release\SingleFile\*.xml" 2>nul
echo Build complete: %~dp0Release\SingleFile\KingR9Tools.exe
echo Send only KingR9Tools.exe. Sign releases with a trusted code-signing certificate before distribution.

