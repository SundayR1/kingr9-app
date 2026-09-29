# KingR9 Tools

A Windows x64 WPF utility for system and network tuning, with a WebView2 interface.

## Build

- Windows x64
- .NET 8 SDK
- Internet access for NuGet restore

Run `BUILD.bat`. It creates the portable app folder at `Release/KingR9` and packages it as `Release/KingR9_v1.0.1.zip`.

## Download

Download `KingR9_v1.0.1.zip` from the [v1.0.1 release](https://github.com/SundayR1/kingr9-app/releases/tag/v1.0.1). Extract the full archive and run `KingR9Tools.exe` from the extracted folder. Keep the other files alongside it.

## Important

Some features modify Windows services, registry values, network settings, or boot configuration and require administrator privileges. Review each action before applying it and keep a backup or restore point available.
