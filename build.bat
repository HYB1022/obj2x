@echo off
rem Builds obj2x.exe from src\*.cs with the C# compiler that ships with Windows (.NET Framework 4).
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo csc.exe was not found. Install the .NET Framework 4.x developer pack or use the prebuilt obj2x.exe.
  pause
  exit /b 1
)
"%CSC%" /nologo /target:winexe /optimize+ /out:obj2x.exe /win32icon:obj2x.ico /reference:System.Windows.Forms.dll /reference:System.Drawing.dll src\*.cs
if errorlevel 1 (
  echo Build failed.
  pause
  exit /b 1
)
echo Build OK: obj2x.exe
