@echo off
rem Windows와 맥을 한 번에 빌드하고 점검한다: python build.py release → python build.py test
rem 결과: dist\BusDepot.zip, dist\BusDepot-mac-arm64.zip, dist\BusDepot-mac-x64.zip, dist\selftest_win.txt
chcp 65001 >nul
set PYTHONIOENCODING=utf-8
cd /d "%~dp0"

where dotnet >nul 2>nul || (echo [FAIL] .NET 8 SDK가 없습니다 & goto fail)
where python >nul 2>nul || (echo [FAIL] Python 3이 없습니다 & goto fail)

echo === 빌드 (Windows + 맥 arm64/x64) ===
python build.py release || goto fail

echo.
echo === 점검 ===
python build.py test || goto fail

echo.
echo 모두 통과했습니다.
pause
exit /b 0

:fail
echo.
echo 실패했습니다. 위의 메시지를 확인하세요.
pause
exit /b 1
