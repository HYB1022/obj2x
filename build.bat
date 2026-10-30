@echo off
rem Builds obj2x.exe (WPF, .NET Framework 4) from src\Core and src\App with the C# compiler that ships with Windows.
rem Usage: build.bat [output exe path]   (default: obj2x.exe next to this file)
setlocal EnableDelayedExpansion
cd /d "%~dp0"
set "FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
if not exist "%FW%\csc.exe" set "FW=%WINDIR%\Microsoft.NET\Framework\v4.0.30319"
if not exist "%FW%\csc.exe" (
  echo csc.exe was not found. Install the .NET Framework 4.x or use the prebuilt obj2x.exe.
  pause
  exit /b 1
)
set "CSC=%FW%\csc.exe"
set "WPF=%FW%\WPF"
set "OUT=%~1"
if "%OUT%"=="" set "OUT=obj2x.exe"
set "REFS=/r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll"

rem 1) Core (logic) must build on its own, without WPF/WinForms.
"%CSC%" /nologo /target:library /out:"%TEMP%\obj2x_core_check.dll" %REFS% src\Core\*.cs
if errorlevel 1 (
  echo Core build failed.
  pause
  exit /b 1
)

rem 2) App: XAML views are embedded as resources named Obj2x.Views.^<file^>.xaml and loaded at runtime.
set "RES="
for %%f in (src\App\Views\*.xaml) do set "RES=!RES! /resource:%%f,Obj2x.Views.%%~nxf"
rem    Built-in languages: lang\<code>.json -> resource Obj2x.Lang.<code>.json (users can add/override with a lang folder next to the exe).
for %%f in (lang\*.json) do (
  set "LF=%%~nf"
  if not "!LF:~0,1!"=="_" set "RES=!RES! /resource:%%f,Obj2x.Lang.%%~nxf"
)
"%CSC%" /nologo /target:winexe /optimize+ /nowarn:618 /out:"%OUT%" /win32icon:obj2x.ico %REFS% ^
  /r:System.Xaml.dll /r:"%WPF%\WindowsBase.dll" /r:"%WPF%\PresentationCore.dll" /r:"%WPF%\PresentationFramework.dll" ^
  !RES! src\Core\*.cs src\App\*.cs
if errorlevel 1 (
  echo Build failed.
  pause
  exit /b 1
)
rem 3) The bundled font (fonts\) must sit next to the exe.
for %%I in ("%OUT%") do set "OUTDIR=%%~dpI"
if /I not "%OUTDIR%"=="%~dp0" xcopy /y /q /i "fonts" "%OUTDIR%fonts" >nul
echo Build OK: %OUT%
