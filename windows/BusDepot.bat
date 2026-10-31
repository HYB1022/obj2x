@echo off
cd /d "%~dp0"
if not exist BusDepot.exe call build.bat
if exist BusDepot.exe start "" "BusDepot.exe"
