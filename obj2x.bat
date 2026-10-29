@echo off
cd /d "%~dp0"
if not exist obj2x.exe call build.bat
if exist obj2x.exe start "" "obj2x.exe"
