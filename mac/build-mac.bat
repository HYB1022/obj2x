@echo off
rem Builds the macOS version (Avalonia, .NET 8) on Windows and packs BusDepot.app into zips:
rem   publish\BusDepot-mac-arm64.zip (Apple silicon) and publish\BusDepot-mac-x64.zip (Intel).
rem Needs the .NET 8 SDK and Python. Usage: mac\build-mac.bat [version]
setlocal
cd /d "%~dp0"
rem Version: the argument, or (auto) AppInfo.Version read by make_app.py
set "VER=%~1"
if "%VER%"=="" set "VER=auto"
for %%r in (osx-arm64 osx-x64) do (
  dotnet publish BusDepot.Mac.csproj -c Release -r %%r --self-contained true -p:PublishSingleFile=false -o publish\%%r -v q -nologo
  if errorlevel 1 ( echo Publish failed: %%r & exit /b 1 )
)
python make_app.py publish\osx-arm64 publish\BusDepot-mac-arm64.zip %VER%
python make_app.py publish\osx-x64 publish\BusDepot-mac-x64.zip %VER%
echo Build OK: publish\BusDepot-mac-arm64.zip, publish\BusDepot-mac-x64.zip 
