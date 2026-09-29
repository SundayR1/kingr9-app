@echo off
chcp 65001 >nul
title KingR9 Tools - Portable Build
cd /d "%~dp0"

set "DOTNET=%LOCALAPPDATA%\Programs\dotnet\dotnet.exe"
if not exist "%DOTNET%" set "DOTNET=dotnet"
"%DOTNET%" --version >nul 2>&1
if errorlevel 1 (
  echo [X] ไม่พบ .NET 8 SDK
  echo     ติดตั้งจาก https://dotnet.microsoft.com/download/dotnet/8.0
  exit /b 1
)

echo Publishing portable folder...
"%DOTNET%" publish KingR9Tools.csproj -c Release -r win-x64 --self-contained true ^
  /p:PublishSingleFile=false ^
  /p:IncludeNativeLibrariesForSelfExtract=false ^
  /p:DebugType=None /p:DebugSymbols=false ^
  -o "%~dp0Release\KingR9"
if errorlevel 1 exit /b 1

powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%~dp0Release\KingR9\*' -DestinationPath '%~dp0Release\KingR9_Portable.zip' -CompressionLevel Optimal -Force"
if errorlevel 1 exit /b 1

echo Build complete: %~dp0Release\KingR9
echo Portable archive: %~dp0Release\KingR9_Portable.zip

