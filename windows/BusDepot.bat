@echo off
rem Bus Depot을 실행한다 (BusDepot.exe 옆에 두는 파일. 예전 obj2x.bat도 이것을 부른다)
cd /d "%~dp0"
if exist BusDepot.exe start "" "BusDepot.exe"
